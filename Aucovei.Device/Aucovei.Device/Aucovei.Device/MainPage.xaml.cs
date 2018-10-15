using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Aucovei.Device.Configuration;
using Aucovei.Device.Devices;
using Aucovei.Device.Services;
using Aucovei.Device.Web;
using Microsoft.IoT.Lightning.Providers;
using UnitsNet;
using Windows.Devices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Aucovei.Device
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int SLAVEADDRESS = 0x40;
        private const int TriggerPin = 16;
        private const int LedPin = 26;
        private const int EchoPin = 12;
        private I2cDevice arduino; // Used to Connect to Arduino
        private DisplayManager displayManager;
        private int distanceCounter = 10;
        private HttpServer httpServer;
        private bool isArduinoSlaveSetup;
        private bool isRfcommSetup;
        private bool isSystemInitialized;
        private PanTiltServo panTiltServo;
        private RfcommServiceProvider rfcommProvider;
        private StreamSocket socket;
        private StreamSocketListener socketListener;
        private readonly DispatcherTimer arduinoDataReadTimer = new DispatcherTimer();
        private HCSR04 ultrasonicsensor;
        private readonly DispatcherTimer ultrasonictimer = new DispatcherTimer();
        private bool wasObstacleDetected;
        private DataWriter writer;
        private double speedInmPerSecond = 0;
        private PlaybackService playbackService;
        private GpioController gpio;
        private GpioPin cameraLedPin;


        public MainPage()
        {
            this.InitializeSystem();
        }

        public async void InitializeSystem()
        {
            try
            {
                this.InitializeComponent();
                if (LightningProvider.IsLightningEnabled)
                {
                    LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
                }
                else
                {
                    //var msg = new MessageDialog("Error: Lightning not enabled");
                    //await msg.ShowAsync();
                }

                this.panTiltServo = new PanTiltServo();
                this.InitializeHCSR04();
                this.playbackService = new PlaybackService();
            }
            catch (Exception ex)
            {
                this.isSystemInitialized = false;

                var msg = new MessageDialog(ex.Message);
                await msg.ShowAsync(); // this will show error message(if Any)
            }
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                this.gpio = await GpioController.GetDefaultAsync();
                if (this.gpio == null)
                {
                    throw new IOException("GPIO interface not found");
                }
              
                this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.BootUp, true);
                this.displayManager = new DisplayManager();
                await this.displayManager.Init();
                this.displayManager.AppendImage(DisplayImages.Logo_24_32, 48, 1);
                await Task.Delay(500);
                this.displayManager.ClearDisplay();

                this.displayManager.AppendText("Initializing system...", 0, 0);
                this.displayManager.AppendText(">Connecting slave...", 5, 1);
                await this.InitializeArduinoSlave();
                while (!this.isArduinoSlaveSetup)
                {
                    this.displayManager.AppendText(">Searching...", 10, 2);
                    await Task.Delay(2000);
                }

                this.displayManager.AppendText(">>Done...", 10, 2);
                this.displayManager.AppendText(">Starting Rfcomm Svc...", 5, 3);

                this.InitializeRfcommServer();
                while (!this.isRfcommSetup)
                {
                    await Task.Delay(2000);
                }

                this.displayManager.AppendText(">Started Rfcomm Svc...", 5, 3);

                await this.panTiltServo.Center();
                await this.InitializeVideoService();

                this.displayManager.ClearDisplay();
                this.displayManager.AppendText("Initialization complete!", 0, 0);
                await Task.Delay(1000);
                this.displayManager.ClearDisplay();

                this.DisplayNetworkInfo();

                this.displayManager.AppendImage(DisplayImages.BluetoothDisconnected, 0, 1);
            }
            catch (Exception ex)
            {
                this.isSystemInitialized = false;

                var msg = new MessageDialog(ex.Message);
                await msg.ShowAsync(); // this will show error message(if Any)
                this.displayManager.ClearDisplay();
                this.displayManager.AppendImage(DisplayImages.Error, 0, 0);
                this.displayManager.AppendText("Initialization error!", 15, 0);
            }
            finally
            {
                this.playbackService.StopSoundPlay();
            }
        }

        private void DisplayNetworkInfo()
        {
            var ip = this.GetIPAddress();
            if (ip != null)
            {
                this.displayManager.AppendImage(DisplayImages.WifiConnected, 0, 0);
                this.displayManager.AppendText("  " + ip, 15, 0);
            }
            else
            {
                this.displayManager.AppendImage(DisplayImages.WifiConnected, 0, 0);
                this.displayManager.AppendText("  Not connected!", 15, 0);
            }
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            /* Cleanup */
            this.arduino?.Dispose();
            this.displayManager?.Dispose();
            this.cameraLedPin?.Dispose();
        }

        private async Task InitializeArduinoSlave()
        {
            var settings = new I2cConnectionSettings(SLAVEADDRESS); // Slave Address of Arduino Uno 
            settings.BusSpeed = I2cBusSpeed.FastMode; // this bus has 400Khz speed
            //var controller = await I2cController.GetDefaultAsync();
            //this.arduino = controller.GetDevice(settings);

            // ALTERNATE WAY FOR USING I2C. BUT DOSENT WORK WITH DMDD
            //Use the I2CBus device selector to create an advanced query syntax string
            const string I2CControllerName = "I2C1";
            string aqs = I2cDevice.GetDeviceSelector(I2CControllerName);
            //Use the Windows.Devices.Enumeration.DeviceInformation class to create a collection using the advanced query syntax string
            DeviceInformationCollection dis = await DeviceInformation.FindAllAsync(aqs);
            //Instantiate the the I2C device using the device id of the I2CBus and the I2CConnectionSettings
            this.arduino = await I2cDevice.FromIdAsync(dis[0].Id, settings);

            this.arduinoDataReadTimer.Tick += this.ArduinoDataReadTimer_Tick; // We will create an event handler 
            this.arduinoDataReadTimer.Interval = new TimeSpan(0, 0, 0, 0, 100); // Timer_Tick is executed every 500 milli second
            this.arduinoDataReadTimer.Start();
        }

        private void InitializeHCSR04()
        {
            this.ultrasonicsensor = new HCSR04(TriggerPin, EchoPin, 1);
            this.ultrasonictimer.Tick += this.Ultrasonictimer_Tick;
            this.ultrasonictimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        }

        private async Task InitializeVideoService()
        {
            var camera = new Camera();
            var mediaFrameFormats = await camera.GetMediaFrameFormatsAsync();
            ConfigurationFile.SetSupportedVideoFrameFormats(mediaFrameFormats);
            var videoSetting = await ConfigurationFile.Read(mediaFrameFormats);

            //await camera.Initialize(videoSetting);
            //camera.Start();

            this.httpServer = new HttpServer(camera, videoSetting);
            //this.httpServer.Start();
        }

        private async void ArduinoDataReadTimer_Tick(object sender, object e)
        {
            var response = new byte[10];

            try
            {
                this.arduino.Read(response); // this funtion will read data from Arduino 
                this.isArduinoSlaveSetup = true;
            }
            catch (Exception p)
            {
                this.isArduinoSlaveSetup = false;
                this.displayManager.AppendImage(DisplayImages.Error, 0, 1);
                this.displayManager.AppendText("Slave error ...", 15, 1);
                var msg = new MessageDialog(p.Message);
                await msg.ShowAsync(); // this will show error message(if Any)
            }

            var datArray = Encoding.ASCII.GetString(response, 0, 10).ToCharArray(); // Converte  Byte to Char
            var data = new string(datArray);
            data = data.TrimEnd('?');
            if (!string.IsNullOrEmpty(data))
            {
                this.ConvertToMeterPerSecond(data);
            }
        }

        private void ConvertToMeterPerSecond(string data)
        {
            const double wheelradiusMeters = 0.0315;
            if (int.TryParse(data, out var rps))
            {
                var speed = wheelradiusMeters * (2 * Math.PI) * (rps);
                this.speedInmPerSecond = Math.Round(speed, 2);
                var speedString = string.Concat(this.speedInmPerSecond.ToString(CultureInfo.InvariantCulture), " m/s");
                //var task = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                //() => { this.Console.Text = speedString; });
            }
        }

        private async void Ultrasonictimer_Tick(object sender, object e)
        {
            try
            {
                var distance = this.ultrasonicsensor.GetDistance(new Length(2));
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        if (distance.Centimeters > 0.0 &&
                            distance.Centimeters < 80.0)
                        {
                            // Stop timer
                            this.ultrasonictimer.Stop();
                            this.Console.Text = "OBSTACLE " + distance;

                            await this.ExecuteCommandAsync(Commands.DriveStop);
                            await this.ExecuteCommandAsync(Commands.SpeedSlow);

                            this.wasObstacleDetected = true;

                            // Reverse the rover till a safe distance.
                            while (this.distanceCounter > 0)
                            {
                                if (this.distanceCounter > 5)
                                {
                                    this.Console.Text = "REVERSE " + this.distanceCounter;
                                    await this.ExecuteCommandAsync(Commands.DriveReverse);
                                }
                                else if (this.distanceCounter >= 3 && this.distanceCounter <= 5)
                                {
                                    this.Console.Text = "REVERSE Left" + this.distanceCounter;
                                    await this.ExecuteCommandAsync(Commands.DriveReverseLeft);
                                }
                                else
                                {
                                    this.Console.Text = "REVERSE " + this.distanceCounter;
                                    await this.ExecuteCommandAsync(Commands.DriveReverse);
                                }

                                this.distanceCounter--;
                                await Task.Delay(TimeSpan.FromMilliseconds(50));
                            }

                            await this.ExecuteCommandAsync(Commands.DriveForward);

                            // Restart timer
                            this.ultrasonictimer.Start();
                        }
                        else if (this.wasObstacleDetected &&
                                 (distance.Centimeters < 0.1 ||
                                  distance.Centimeters > 100.00))
                        {
                            // Reset variables and rover parameters
                            this.distanceCounter = 10;
                            this.wasObstacleDetected = false;
                            this.Console.Text = "RESUME " + distance;
                            await this.ExecuteCommandAsync(Commands.SpeedNormal);
                            await this.ExecuteCommandAsync(Commands.DriveForward);
                        }
                        else
                        {
                            this.Console.Text = "NORMAL " + distance;
                        }
                    });
            }
            catch (Exception ex)
            {
                if (!this.ultrasonictimer.IsEnabled)
                {
                    this.ultrasonictimer.Start();
                }
            }
        }

        /// <summary>
        ///     Initializes the server using RfcommServiceProvider to advertise the Chat Service UUID and start listening
        ///     for incoming connections.
        /// </summary>
        private async void InitializeRfcommServer()
        {
            try
            {
                this.rfcommProvider =
                    await RfcommServiceProvider.CreateAsync(
                        RfcommServiceId.FromUuid(Constants.RfcommDeviceServiceUuid));
                this.isRfcommSetup = true;
            }
            // Catch exception HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE).
            catch (Exception ex) when ((uint)ex.HResult == 0x800710DF)
            {
                this.isRfcommSetup = false;
                Debug.Write("Make sure your Bluetooth Radio is on: " + ex.Message);
                this.displayManager.AppendImage(DisplayImages.Error, 0, 3);
                this.displayManager.AppendText("Bluetooth off!!", 20, 3);
                return;
            }

            // Create a listener for this service and start listening
            this.socketListener = new StreamSocketListener();
            this.socketListener.ConnectionReceived += this.OnConnectionReceived;
            var rfcomm = this.rfcommProvider.ServiceId.AsString();
            await this.socketListener.BindServiceNameAsync(this.rfcommProvider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

            // Set the SDP attributes and start Bluetooth advertising
            this.InitializeServiceSdpAttributes(this.rfcommProvider);

            try
            {
                this.rfcommProvider.StartAdvertising(this.socketListener, true);
            }
            catch (Exception e)
            {
                Debug.Write(e);
                return;
            }

            Debug.Write("Listening for incoming connections");
        }

        /// <summary>
        ///     Creates the SDP record that will be revealed to the Client device when pairing occurs.
        /// </summary>
        /// <param name="rfcommProvider">The RfcommServiceProvider that is being used to initialize the server</param>
        private void InitializeServiceSdpAttributes(RfcommServiceProvider rfcommProvider)
        {
            var sdpWriter = new DataWriter();

            // Write the Service Name Attribute.
            sdpWriter.WriteByte(Constants.SdpServiceNameAttributeType);

            // The length of the UTF-8 encoded Service Name SDP Attribute.
            sdpWriter.WriteByte((byte)Constants.SdpServiceName.Length);

            // The UTF-8 encoded Service Name value.
            sdpWriter.UnicodeEncoding = UnicodeEncoding.Utf8;
            sdpWriter.WriteString(Constants.SdpServiceName);

            // Set the SDP Attribute on the RFCOMM Service Provider.
            rfcommProvider.SdpRawAttributes.Add(Constants.SdpServiceNameAttributeId, sdpWriter.DetachBuffer());
        }

        private async void SendMessage(string message)
        {
            // Make sure that the connection is still up and there is a message to send
            if (this.socket != null)
            {
                this.writer.WriteInt32(message.Length);
                this.writer.WriteString(message);

                await this.writer.StoreAsync();
            }
            else
            {
                Debug.Write(
                    "No clients connected, please wait for a client to connect before attempting to send a message");
            }
        }

        private async void Disconnect()
        {
            this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.Disconnected);

            this.displayManager.ClearRow(1);
            this.displayManager.AppendImage(DisplayImages.BluetoothDisconnected, 0, 1);

            if (this.writer != null)
            {
                this.writer.DetachStream();
                this.writer = null;
            }

            if (this.socket != null)
            {
                this.socket.Dispose();
                this.socket = null;
            }

            var server = this.httpServer;
            if (server != null)
            {
                await server?.Stop();
            }

            var roverSpeed = Encoding.Unicode.GetBytes(Commands.DriveStopValue);
            this.arduino.Write(roverSpeed);

            this.displayManager.ClearRow(2);
            this.displayManager.ClearRow(3);
            Debug.Write("Disconected");
        }


        /// <summary>
        ///     Invoked when the socket listener accepts an incoming Bluetooth connection.
        /// </summary>
        /// <param name="sender">The socket listener that accepted the connection.</param>
        /// <param name="args">The connection accept parameters, which contain the connected socket.</param>
        private async void OnConnectionReceived(
            StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("Connection received");
            try
            {
                this.socket = args.Socket;
            }
            catch (Exception e)
            {
                Debug.Write(e);
                this.Disconnect();
                return;
            }

            // Note - this is the supported way to get a Bluetooth device from a given socket
            var remoteDevice = await BluetoothDevice.FromHostNameAsync(this.socket.Information.RemoteHostName);

            this.writer = new DataWriter(this.socket.OutputStream);
            var reader = new DataReader(this.socket.InputStream);
            var remoteDisconnection = false;

            this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.Default);
            Debug.Write("Connected to Client: " + remoteDevice.Name);

            //SendMessage("Connected!");
            this.SendMessage("hostip:" + this.GetIPAddress());
            await Task.Delay(500);
            this.SendMessage("Ready!");

            await this.ExecuteCommandAsync(Commands.SpeedNormal);

            this.displayManager.ClearRow(1);
            this.displayManager.AppendImage(DisplayImages.BluetoothFind2, 0, 1);
            this.displayManager.AppendText(" " + remoteDevice.Name, 20, 1);

            // Infinite read buffer loop
            while (true)
            {
                try
                {
                    // Based on the protocol we've defined, the first uint is the size of the message
                    var readLength = await reader.LoadAsync(sizeof(uint));

                    // Check if the size of the data is expected (otherwise the remote has already terminated the connection)
                    if (readLength < sizeof(uint))
                    {
                        remoteDisconnection = true;
                        break;
                    }

                    var currentLength = reader.ReadUInt32();
                    if (currentLength == 0)
                    {
                        return;
                    }

                    // Load the rest of the message since you already know the length of the data expected.
                    readLength = await reader.LoadAsync(currentLength);

                    // Check if the size of the data is expected (otherwise the remote has already terminated the connection)
                    if (readLength < currentLength)
                    {
                        remoteDisconnection = true;
                        break;
                    }

                    var message = reader.ReadString(currentLength);
                    if (this.wasObstacleDetected &&
                        string.Equals(message, Commands.DriveForward, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => { this.Console.Text += Environment.NewLine + message; });

                    await this.ExecuteCommandAsync(message);

                    Debug.Write("Received: " + message);
                }
                // Catch exception HRESULT_FROM_WIN32(ERROR_OPERATION_ABORTED).
                catch (Exception ex) when ((uint)ex.HResult == 0x800703E3)
                {
                    Debug.Write("Client Disconnected Successfully");
                    break;
                }
            }

            reader.DetachStream();
            if (remoteDisconnection)
            {
                this.Disconnect();
                Debug.Write("Client disconnected");
            }
        }

        private async Task ExecuteCommandAsync(string commandText)
        {
            var turnSpeed = Encoding.Unicode.GetBytes(Commands.SpeedSlowValue);
            var normalSpeed = Encoding.Unicode.GetBytes(Commands.SpeedNormalValue);

            switch (commandText)
            {
                case Commands.DriveForward:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.DriveForwardValue);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.TopArrow, 0, 2);
                        this.displayManager.AppendText("Drive", 17, 2);
                        this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveReverse:
                    {
                        this.playbackService.ChangeMediaSource(PlaybackService.SoundFiles.CensorBeep);
                        this.playbackService.PlaySound();

                        var result = Encoding.Unicode.GetBytes(Commands.DriveReverseValue);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.BottomArrow, 0, 2);
                        this.displayManager.AppendText("Drive", 17, 2);
                        this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveLeft:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.DriveLeftValue);
                        this.arduino.Write(turnSpeed);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.LeftArrow, 0, 2);
                        this.displayManager.AppendText("Drive", 17, 2);
                        this.UpdateDisplayWithSpeed();
                        this.arduino.Write(normalSpeed);

                        break;
                    }
                case Commands.DriveRight:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.DriveRightValue);
                        this.arduino.Write(turnSpeed);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.RightArrow, 0, 2);
                        this.displayManager.AppendText("Drive", 17, 2);
                        this.UpdateDisplayWithSpeed();
                        this.arduino.Write(normalSpeed);

                        break;
                    }
                case Commands.DriveStop:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.DriveStopValue);
                        this.arduino.Write(result);
                        this.displayManager.ClearRow(2);
                        this.displayManager.AppendImage(DisplayImages.Stop, 0, 2);
                        this.displayManager.AppendText("Stop", 16, 2);

                        break;
                    }
                case Commands.DriveReverseLeft:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.DriveReverseLeftValue);
                        this.arduino.Write(turnSpeed);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.LeftArrow, 0, 2);
                        this.displayManager.AppendText("Drive", 17, 2);
                        this.UpdateDisplayWithSpeed();
                        this.arduino.Write(normalSpeed);

                        break;
                    }
                case Commands.DriveReverseRight:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.DriveReverseRightValue);
                        this.arduino.Write(turnSpeed);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.RightArrow, 0, 2);
                        this.displayManager.AppendText("Drive", 17, 2);
                        this.UpdateDisplayWithSpeed();
                        this.arduino.Write(normalSpeed);

                        break;
                    }
                case Commands.DriveAutoModeOn:
                    {
                        await this.playbackService.SynthesizeTextAsync("Autonomous mode on!");

                        var result = Encoding.Unicode.GetBytes(Commands.DriveForwardValue);
                        this.arduino.Write(result);
                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            () => { this.ultrasonictimer.Start(); });
                        this.displayManager.AppendImage(DisplayImages.Logo_16_16, 110, 0);

                        break;
                    }
                case Commands.DriveAutoModeOff:
                    {
                        await this.playbackService.SynthesizeTextAsync("Autonomous mode off!");
                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            () => { this.ultrasonictimer.Stop(); });
                        this.DisplayNetworkInfo();
                        this.arduino.Write(normalSpeed);
                        var result = Encoding.Unicode.GetBytes(Commands.DriveStopValue);
                        this.arduino.Write(result);

                        break;
                    }
                case Commands.SpeedNormal:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.SpeedNormalValue);
                        this.arduino.Write(result);

                        break;
                    }
                case Commands.SpeedSlow:
                    {
                        var result = Encoding.Unicode.GetBytes(Commands.SpeedSlowValue);
                        this.arduino.Write(result);

                        break;
                    }
                case Commands.CameraOn:
                    {
                        var server = this.httpServer;
                        if (server != null)
                        {
                            await server?.Start();
                        }

                        this.SendMessage("CAMON");
                        this.displayManager.AppendImage(DisplayImages.Camera, 100, 1);

                        break;
                    }
                case Commands.TiltUp:
                    {
                        await this.panTiltServo.ExecuteCommand(Commands.TiltUpValue);
                        break;
                    }
                case Commands.TiltDown:
                    {
                        await this.panTiltServo.ExecuteCommand(Commands.TiltDownValue);
                        break;
                    }
                case Commands.PanLeft:
                    {
                        await this.panTiltServo.ExecuteCommand(Commands.PanLeftValue);
                        break;
                    }
                case Commands.PanRight:
                    {
                        await this.panTiltServo.ExecuteCommand(Commands.PanRightValue);
                        break;
                    }
                case Commands.PanTiltCenter:
                    {
                        await this.panTiltServo.ExecuteCommand(Commands.PanTiltCenterValue);
                        break;
                    }
                case Commands.CameraOff:
                    {
                        var server = this.httpServer;
                        if (server != null)
                        {
                            await server?.Stop();
                        }
                        this.SendMessage("CAMOFF");
                        this.displayManager.ClearCharacterRange(1, 100, 128);

                        break;
                    }
                case Commands.Horn:
                    {
                        this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.Horn);

                        break;
                    }
                case Commands.CameraLedOn:
                    {
                        this.cameraLedPin.Write(GpioPinValue.High);

                        break;
                    }
                case Commands.CameraLedOff:
                    {
                        this.cameraLedPin.Write(GpioPinValue.Low);

                        break;
                    }
                default:
                    {
                        await this.playbackService.SynthesizeTextAsync(commandText);

                        break;
                    }
            }
        }

        private void UpdateDisplayWithSpeed()
        {
            var task = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                 () =>
                 {
                     var speedString = string.Concat(this.speedInmPerSecond.ToString(CultureInfo.InvariantCulture), " m/s");
                     this.displayManager.AppendText(speedString, 80, 2);
                 });
        }

        private IPAddress GetIPAddress()
        {
            var IpAddress = new List<string>();
            var Hosts = NetworkInformation.GetHostNames().ToList();
            foreach (var Host in Hosts)
            {
                var IP = Host.DisplayName;
                IpAddress.Add(IP);
            }

            var address = IPAddress.Parse(IpAddress.Last());
            return address;
        }

        private void Clear_OnClick(object sender, RoutedEventArgs e)
        {
            this.Console.Text = "";
        }

        private void Clear_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            this.Console.Text = "";
        }
    }
}