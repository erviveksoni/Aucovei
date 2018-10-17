using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Aucovei.Device.Configuration;
using Aucovei.Device.Devices;
using Aucovei.Device.Services;
using Aucovei.Device.Web;
using UnitsNet;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Aucovei.Device
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private I2cDevice arduino; // Used to Connect to Arduino
        private DisplayManager displayManager;
        private int distanceCounter = 10;
        private HttpServer httpServer;
        private bool isArduinoSlaveSetup;
        private bool isRfcommSetup;
        private bool isSystemInitialized;
        private bool isAutodriveTimerActive;
        private bool isVoiceModeActive = false;
        private PanTiltServo panTiltServo;
        private RfcommServiceProvider rfcommProvider;
        private StreamSocket socket;
        private StreamSocketListener socketListener;
        private readonly DispatcherTimer arduinoDataReadTimer = new DispatcherTimer();
        private HCSR04 ultrasonicsensor;
        private DispatcherTimer ultrasonictimer;
        private bool wasObstacleDetected;
        private DataWriter writer;
        private double speedInmPerSecond = 0;
        private PlaybackService playbackService;
        private GpioController gpio;
        private GpioPin cameraLedPin;
        VoiceCommandController voiceController;
        DispatcherTimer voiceCommandHideTimer;

        public MainPage()
        {
            this.InitializeSystem();
        }

        public async void InitializeSystem()
        {
            try
            {
                this.InitializeComponent();

                /*
                if (LightningProvider.IsLightningEnabled)
                {
                    LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
                }
                else
                {
                    var msg = new MessageDialog("Error: Lightning not enabled");
                    await msg.ShowAsync();
                }
                */

                this.panTiltServo = new PanTiltServo();

                this.InitializeVoiceCommands();

                this.playbackService = new PlaybackService();
            }
            catch (Exception ex)
            {
                this.isSystemInitialized = false;

                var msg = new MessageDialog(ex.Message);
                await msg.ShowAsync(); // this will show error message(if Any)
            }
        }

        private void InitializeVoiceCommands()
        {
            this.voiceCommandHideTimer = new DispatcherTimer();
            this.voiceCommandHideTimer.Interval = TimeSpan.FromSeconds(5);
            this.voiceCommandHideTimer.Tick += this.VoiceCommandHideTimer_Tick;

            this.voiceController = new VoiceCommandController();
            this.voiceController.ResponseReceived += this.VoiceController_ResponseReceived;
            this.voiceController.CommandReceived += this.VoiceController_CommandReceived;
            this.voiceController.StateChanged += this.VoiceController_StateChanged;
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

                this.ultrasonicsensor = new HCSR04(Constants.TriggerPin, Constants.EchoPin, 1);
                this.cameraLedPin = this.gpio.OpenPin(Constants.LedPin);
                this.cameraLedPin.Write(GpioPinValue.Low);
                this.cameraLedPin.SetDriveMode(GpioPinDriveMode.Output);

                this.Speak("Initializing system");
                this.displayManager = new DisplayManager();
                await this.displayManager.Init();
                this.displayManager.AppendImage(DisplayImages.Logo_24_32, 48, 1);
                await Task.Delay(500);
                this.displayManager.ClearDisplay();

                this.displayManager.AppendText("Initializing system...", 0, 0);
                this.displayManager.AppendText(">Connecting slave...", 5, 1);
                await this.InitializeI2CSlaveAsync();
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
                this.voiceController.Initialize();

                this.displayManager.ClearDisplay();
                this.displayManager.AppendText("Initialization complete!", 0, 0);
                await Task.Delay(1000);
                this.displayManager.ClearDisplay();

                this.DisplayNetworkInfo();

                this.displayManager.AppendImage(DisplayImages.BluetoothDisconnected, 0, 1);

                this.Speak("Initialization complete");
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
        }

        private async Task InitializeI2CSlaveAsync()
        {
            var settings = new I2cConnectionSettings(Constants.SLAVEADDRESS); // Slave Address of Arduino Uno 
            settings.BusSpeed = I2cBusSpeed.FastMode; // this bus has 400Khz speed
                                                      //var controller = await I2cController.GetDefaultAsync();
                                                      //this.arduino = controller.GetDevice(settings);

            // ALTERNATE WAY FOR USING I2C. BUT DOSENT WORK WITH DMDD
            //Use the I2CBus device selector to create an advanced query syntax string

            string aqs = I2cDevice.GetDeviceSelector(Constants.I2CControllerName);
            //Use the Windows.Devices.Enumeration.DeviceInformation class to create a collection using the advanced query syntax string
            DeviceInformationCollection dis = await DeviceInformation.FindAllAsync(aqs);
            //Instantiate the the I2C device using the device id of the I2CBus and the I2CConnectionSettings
            this.arduino = await I2cDevice.FromIdAsync(dis[0].Id, settings);

            this.arduinoDataReadTimer.Tick += this.I2CReadTimer_Tick; // We will create an event handler 
            this.arduinoDataReadTimer.Interval = new TimeSpan(0, 0, 0, 0, 100); // Timer_Tick is executed every 500 milli second
            this.arduinoDataReadTimer.Start();
        }

        private void InitializeDistanceSensor()
        {
            if (this.ultrasonictimer != null &&
                this.ultrasonictimer.IsEnabled)
            {
                this.ultrasonictimer.Stop();
            }

            this.isAutodriveTimerActive = true;
            this.ultrasonictimer = new DispatcherTimer();
            this.ultrasonictimer.Tick += this.Ultrasonictimer_Tick;
            this.ultrasonictimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        }

        private async Task InitializeVideoService()
        {
            var camera = new Camera();
            var mediaFrameFormats = await camera.GetMediaFrameFormatsAsync();
            ConfigurationFile.SetSupportedVideoFrameFormats(mediaFrameFormats);
            var videoSetting = await ConfigurationFile.Read(mediaFrameFormats);
            this.httpServer = new HttpServer(camera, videoSetting);
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

        private async void I2CReadTimer_Tick(object sender, object e)
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
                var result = Helper.Helpers.ConvertRPSToMeterPerSecond(data);
                // this.WriteToOutputTextBlock(result.ToString());
            }
        }

        private async void Ultrasonictimer_Tick(object sender, object e)
        {
            try
            {
                // for additional safety against timer misbehaviour
                if (!this.isAutodriveTimerActive)
                {
                    return;
                }

                var distance = this.ultrasonicsensor.GetDistance(new Length(2));

                if (distance.Centimeters > 0.0 &&
                    distance.Centimeters < 80.0)
                {
                    // Stop timer
                    this.ultrasonictimer.Stop();
                    this.WriteToOutputTextBlock("OBSTACLE " + distance);

                    await this.ExecuteCommandAsync(Commands.DriveStop);
                    await this.ExecuteCommandAsync(Commands.SpeedSlow);

                    this.wasObstacleDetected = true;

                    // Reverse the rover till a safe distance.
                    while (this.distanceCounter > 0)
                    {
                        if (this.distanceCounter > 5)
                        {
                            this.WriteToOutputTextBlock("REVERSE " + this.distanceCounter);
                            await this.ExecuteCommandAsync(Commands.DriveReverse);
                        }
                        else if (this.distanceCounter >= 3 && this.distanceCounter <= 5)
                        {
                            this.WriteToOutputTextBlock("REVERSE Left" + this.distanceCounter);
                            await this.ExecuteCommandAsync(Commands.DriveReverseLeft);
                        }
                        else
                        {
                            this.WriteToOutputTextBlock("REVERSE " + this.distanceCounter);
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
                    this.WriteToOutputTextBlock("RESUME " + distance);
                    await this.ExecuteCommandAsync(Commands.SpeedNormal);
                    await this.ExecuteCommandAsync(Commands.DriveForward);
                }
                else
                {
                    this.WriteToOutputTextBlock("NORMAL " + distance);
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex);

                throw;
            }
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

        /// <summary>
        ///     Invoked when the socket listener accepts an incoming Bluetooth connection.
        /// </summary>
        /// <param name="sender">The socket listener that accepted the connection.</param>
        /// <param name="args">The connection accept parameters, which contain the connected socket.</param>
        private async void OnConnectionReceived(
            StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("Connection received");

            if (this.isVoiceModeActive)
            {
                Debug.Write("Voice Mode Active!");
                this.SendMessage("Disconnecting device. Aucovei voice mode active.");
                return;
            }

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
            this.SendMessage("hostip:" + Helper.Helpers.GetIPAddress());
            await Task.Delay(500);
            this.SendMessage("Ready!");

            await this.ExecuteCommandAsync(Commands.SpeedNormal);

            this.displayManager.ClearRow(1);
            this.displayManager.AppendImage(DisplayImages.BluetoothFind2, 0, 1);
            this.displayManager.AppendText(" " + remoteDevice.Name, 20, 1);

            try
            {
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
                        this.WriteToOutputTextBlock(message);

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
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
                throw;
            }

            reader.DetachStream();
            if (remoteDisconnection)
            {
                this.Disconnect();
                Debug.Write("Client disconnected");
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
                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () =>
                            {
                                this.InitializeDistanceSensor();
                                this.ultrasonictimer.Start();
                            });

                        this.arduino.Write(turnSpeed);
                        var result = Encoding.Unicode.GetBytes(Commands.DriveForwardValue);
                        this.arduino.Write(result);
                        this.displayManager.AppendImage(DisplayImages.Logo_16_16, 110, 0);
                        this.Speak("Autonomous mode on!");

                        break;
                    }
                case Commands.DriveAutoModeOff:
                    {
                        this.isAutodriveTimerActive = false;

                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () =>
                            {
                                this.ultrasonictimer.Stop();
                            });

                        this.DisplayNetworkInfo();
                        this.arduino.Write(normalSpeed);
                        var result = Encoding.Unicode.GetBytes(Commands.DriveStopValue);
                        this.arduino.Write(result);
                        this.Speak("Autonomous mode off!");

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
                        this.Speak(commandText);

                        break;
                    }
            }
        }

        private void DisplayNetworkInfo()
        {
            var ip = Helper.Helpers.GetIPAddress();
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

        private void UpdateDisplayWithSpeed()
        {
            var task = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                 () =>
                 {
                     var speedString = string.Concat(this.speedInmPerSecond.ToString(CultureInfo.InvariantCulture), " m/s");
                     this.displayManager.AppendText(speedString, 80, 2);
                 });
        }

        /// <summary>
        /// Speaks text using text-to-speech
        /// </summary>
        /// <param name="text">Text to speak</param>
        private async void Speak(string text)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await this.playbackService.SynthesizeTextAsync(text);
            });
        }

        /// <summary>
        /// Writes text to the output textblock
        /// </summary>
        /// <param name="text"></param>
        private async void WriteToOutputTextBlock(string text)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.Console.Text = this.Console.Text + ((this.Console.Text == "") ? "" : (Environment.NewLine + Environment.NewLine)) + text;
                this.scrollViewer.UpdateLayout();
                this.scrollViewer.ChangeView(0, double.MaxValue, 1.0f);
            });
        }

        /// <summary>
        /// Writes the text and emoji icon to the voice command screen
        /// </summary>
        /// <param name="text"></param>
        /// <param name="emoji"></param>
        /// <param name="timeout"></param>
        private async void WriteToCommandTextBlock(string text, string emoji = "😃", long timeout = 3)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.emojiTextBlock.Text = emoji;
                this.voiceCommandTextBlock.Text = text;

                this.voiceCommandGrid.Visibility = Visibility.Visible;
                this.controllerGrid.Visibility = Visibility.Collapsed;

                this.voiceCommandHideTimer.Interval = TimeSpan.FromSeconds(timeout);
                this.voiceCommandHideTimer.Start();
            });
        }

        private void VoiceController_StateChanged(object sender, VoiceCommandControllerEventArgs e)
        {
            try
            {
                var state = (VoiceControllerState)e.Data;
                switch (state)
                {
                    case VoiceControllerState.ListenCommand:
                        this.WriteToOutputTextBlock("Listening for command... 🎤");
                        break;
                    case VoiceControllerState.ListenTrigger:
                        this.WriteToOutputTextBlock("Listening for trigger... 🎤");
                        break;
                    case VoiceControllerState.ProcessCommand:
                        this.WriteToOutputTextBlock("Processing command... 🕺");
                        break;
                    case VoiceControllerState.ProcessTrigger:
                        this.WriteToOutputTextBlock("Processing trigger... 🕺");
                        break;
                    case VoiceControllerState.Idle:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void VoiceCommandHideTimer_Tick(object sender, object e)
        {
            this.ShowVoiceCommandPanel(false);
        }

        /// <summary>
        /// Shows/hides the voice command panel
        /// </summary>
        /// <param name="show"></param>
        private async void ShowVoiceCommandPanel(bool show = true)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (!show)
                {
                    this.voiceCommandGrid.Visibility = Visibility.Collapsed;
                    this.controllerGrid.Visibility = Visibility.Visible;
                    this.voiceCommandHideTimer.Stop();
                }
                else
                {
                    this.voiceCommandGrid.Visibility = Visibility.Visible;
                    this.controllerGrid.Visibility = Visibility.Collapsed;
                }
                await Task.Delay(2);
            });
        }

        private async void VoiceController_CommandReceived(object sender, VoiceCommandControllerEventArgs e)
        {
            string response = "Sorry, I didn't get that.";
            Commands.ToggleCommandState toggleCommandState;
            Commands.DrivingDirection drivingDirection;
            Commands.CameraDirection camerairection;


            try
            {
                var voiceCommand = (VoiceCommand)e.Data;

                switch (voiceCommand.CommandType)
                {
                    case VoiceCommandType.Navigate:
                        //int dest = (int)voiceCommand.Data;
                        //if (controller.Navigation.DoesRoomExist(dest))
                        //{
                        //    // Telemetry.SendReport(Telemetry.MessageType.VoiceCommand, "Successful Command.");

                        //    response = "Navigating to " + dest + "...";
                        //    Speak(response);
                        //    WriteToCommandTextBlock(response, "👌", 1);
                        //    await controller.NavigateToDestination(dest);
                        //}
                        //else
                        {
                            //response = "Sorry, I don't know where " + dest + " is.";
                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "🤷‍♂️");
                        }

                        break;
                    case VoiceCommandType.AutoDrive:
                        response = "Switching autonomous driving mode ";
                        if (this.isVoiceModeActive)
                        {
                            response = "Command failed. Voice mode is active!";
                            response = response + "...";
                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "❗️");

                            break;
                        }

                        if (Enum.TryParse(voiceCommand.Data, true, out toggleCommandState))
                        {
                            response = response + toggleCommandState.ToString().ToLowerInvariant() + "...";
                            if (toggleCommandState == Commands.ToggleCommandState.On)
                            {
                                await this.ExecuteCommandAsync(Commands.DriveAutoModeOn);
                            }
                            else
                            {
                                await this.ExecuteCommandAsync(Commands.DriveAutoModeOff);
                            }

                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "🚗", 1);
                        }
                        break;
                    case VoiceCommandType.VoiceMode:
                        response = "Voice driving mode ";
                        if (Enum.TryParse(voiceCommand.Data, true, out toggleCommandState))
                        {
                            response = response + toggleCommandState.ToString().ToLowerInvariant() + "...";
                            this.isVoiceModeActive = toggleCommandState == Commands.ToggleCommandState.On;
                            if (this.isVoiceModeActive)
                            {
                                await this.ExecuteCommandAsync(Commands.SpeedSlow);
                            }
                            else if (this.isVoiceModeActive)
                            {
                                await this.ExecuteCommandAsync(Commands.SpeedNormal);
                            }

                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "🗣", 1);
                        }

                        break;
                    case VoiceCommandType.Camera:
                        response = "Turning camera ";
                        if (Enum.TryParse(voiceCommand.Data, true, out toggleCommandState))
                        {
                            response = response + toggleCommandState.ToString().ToLowerInvariant() + "...";
                            if (toggleCommandState == Commands.ToggleCommandState.On)
                            {
                                await this.ExecuteCommandAsync(Commands.CameraOn);
                            }
                            else
                            {
                                await this.ExecuteCommandAsync(Commands.CameraOff);
                            }

                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "📸", 1);
                        }
                        break;
                    case VoiceCommandType.Lights:
                        response = "Turning lights ";
                        if (Enum.TryParse(voiceCommand.Data, true, out toggleCommandState))
                        {
                            response = response + toggleCommandState.ToString().ToLowerInvariant() + "...";
                            if (toggleCommandState == Commands.ToggleCommandState.On)
                            {
                                await this.ExecuteCommandAsync(Commands.CameraLedOn);
                            }
                            else
                            {
                                await this.ExecuteCommandAsync(Commands.CameraLedOff);
                            }

                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "💡", 1);
                        }
                        break;
                    case VoiceCommandType.Tilt:
                    case VoiceCommandType.Pan:
                        response = "Moving camera ";
                        if (Enum.TryParse(voiceCommand.Data, true, out camerairection))
                        {
                            response = response + camerairection.ToString().ToLowerInvariant() + "...";
                            this.Speak(response);
                            await this.ExecuteCommandAsync(Helper.Helpers.MapCameraDirectionToCommand(camerairection));
                            this.WriteToCommandTextBlock(response, "📸", 1);
                        }

                        break;
                    case VoiceCommandType.Move:
                    case VoiceCommandType.Turn:
                        if (!this.isVoiceModeActive)
                        {
                            response = "Voice mode not active!";
                            response = response + "...";
                            this.Speak(response);
                            this.WriteToCommandTextBlock(response, "❗️");

                            break;
                        }

                        response = "Moving ";
                        if (Enum.TryParse(voiceCommand.Data, true, out drivingDirection))
                        {
                            response = response + drivingDirection.ToString().ToLowerInvariant() + "...";
                            this.Speak(response);
                            await this.ExecuteCommandAsync(Helper.Helpers.MapDrivingDirectionToCommand(drivingDirection));
                            this.WriteToCommandTextBlock(response, "🏃‍", 1);
                        }
                        break;
                    case VoiceCommandType.Stop:
                        response = "Stopping...";
                        this.Speak(response);
                        this.WriteToCommandTextBlock(response, "🛑", 1);
                        this.WriteToOutputTextBlock("Stopping...");
                        break;
                    default:
                        //response = "Sorry, I didn't get that." + Environment.NewLine + "Try \"Go to room 2011\"";
                        response = "Sorry, I didn't get that." + Environment.NewLine + "Try again!";
                        this.Speak(response);
                        this.WriteToCommandTextBlock(response, "🤷‍♂️");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void VoiceController_ResponseReceived(object sender, VoiceCommandControllerEventArgs e)
        {
            string text = e.Data as string;
            if (!string.IsNullOrEmpty(text) && text != "...")
            {
                switch (text)
                {
                    case "TriggerSuccess":
                        this.WriteToCommandTextBlock("Please say a command...", "🤖", 10000);
                        break;
                    case "TimeoutExceeded":
                        this.ShowVoiceCommandPanel(false);
                        break;
                    default:
                        this.WriteToCommandTextBlock(text, "🤖");
                        break;
                }
            }
        }

        private void closeVoiceCommandButton_Click(object sender, RoutedEventArgs e)
        {
            this.ShowVoiceCommandPanel(false);
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            /* Cleanup */
            this.arduino?.Dispose();
            this.displayManager?.Dispose();
            this.cameraLedPin?.Dispose();
        }
    }
}