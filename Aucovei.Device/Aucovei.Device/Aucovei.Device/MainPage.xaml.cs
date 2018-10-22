using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Aucovei.Device.Compass;
using Aucovei.Device.Configuration;
using Aucovei.Device.Devices;
using Aucovei.Device.Gps;
using Aucovei.Device.Helper;
using Aucovei.Device.RfcommService;
using Aucovei.Device.Services;
using Aucovei.Device.WayPointNavigator;
using Aucovei.Device.Web;

using Windows.Devices.Bluetooth;
using Windows.Devices.Gpio;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Aucovei.Device
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Arduino.Arduino arduino;
        private DisplayManager displayManager;
        private int distanceCounter = 10;
        private HttpServer httpServer;
        private bool isArduinoSlaveSetup;
        private bool isRfcommSetup;
        private bool isSystemInitialized;
        private bool isAutodriveTimerActive;
        private bool isVoiceModeActive = false;
        private PanTiltServo panTiltServo;
        private RfcommServiceManager rfcommProvider;
        private StreamSocket socket;
        private HCSR04 ultrasonicsensor;
        private DispatcherTimer ultrasonictimer;
        private DispatcherTimer compasstimer;
        private bool wasObstacleDetected;
        private DataWriter writer;
        private double speedInmPerSecond = 0;
        private PlaybackService playbackService;
        private GpioController gpio;
        private GpioPin cameraLedPin;
        private VoiceCommandController voiceController;
        private DispatcherTimer voiceCommandHideTimer;
        private GpsInformation gpsInformation;
        private PositionInfo currentGpsPosition;
        private GpsInformation.GpsStatus currentGpsStatus;
        private HMC5883L compass;

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

                this.WriteToOutputTextBlock("Error: " + ex.Message);
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

                this.UpdateControlModeonUi(null, null);

                this.WriteToOutputTextBlock("Setting up distance sensor...");
                this.ultrasonicsensor = new HCSR04(Constants.TriggerPin, Constants.EchoPin);

                this.WriteToOutputTextBlock("Setting up camera...");
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

                await this.InitializeArduinoAsync();
                while (!this.isArduinoSlaveSetup)
                {
                    this.WriteToOutputTextBlock("Searching I2C device...");
                    this.displayManager.AppendText(">Searching...", 10, 2);
                    await Task.Delay(2000);
                }

                this.WriteToOutputTextBlock("Connected to I2C device...");
                this.displayManager.AppendText(">>Done...", 10, 2);

                this.WriteToOutputTextBlock("Initializing Rfcomm Service...");
                this.displayManager.AppendText(">Starting Rfcomm Svc...", 5, 3);

                this.InitializeRfcommServerAsync();
                while (!this.isRfcommSetup)
                {
                    await Task.Delay(2000);
                }

                this.displayManager.AppendText(">Started Rfcomm Svc...", 5, 3);

                await this.panTiltServo.Center();

                this.WriteToOutputTextBlock("Initializing Gps...");
                this.InitializeGps();

                this.WriteToOutputTextBlock("Initializing Compass...");
                this.compass = new HMC5883L(MeasurementMode.Continuous);
                await this.compass.InitializeAsync();
                this.compasstimer = new DispatcherTimer();
                this.compasstimer.Interval = TimeSpan.FromMilliseconds(500);
                this.compasstimer.Tick += this.Compasstimer_Tick;
                this.compasstimer.Start();

                this.WriteToOutputTextBlock("Initializing video Service...");
                await this.InitializeVideoService();

                this.WriteToOutputTextBlock("Initializing voice commands...");
                this.voiceController.Initialize();

                this.displayManager.ClearDisplay();
                this.displayManager.AppendText("Initialization complete!", 0, 0);
                await Task.Delay(1000);
                this.displayManager.ClearDisplay();

                this.DisplayNetworkInfo();

                this.displayManager.AppendImage(DisplayImages.BluetoothDisconnected, 0, 1);

                this.WriteToOutputTextBlock("Initialization complete...");
                this.Speak("Initialization complete");
            }
            catch (Exception ex)
            {
                this.WriteToOutputTextBlock("Error: " + ex.Message);
                this.isSystemInitialized = false;

                var msg = new MessageDialog(ex.Message);
                await msg.ShowAsync(); // this will show error message(if Any)
                this.displayManager.ClearDisplay();
                this.displayManager.AppendImage(DisplayImages.Error, 0, 0);
                this.displayManager.AppendText("Initialization error!", 15, 0);
            }
        }

        private async Task InitializeArduinoAsync()
        {
            this.WriteToOutputTextBlock("Initializing I2C Slave...");
            this.arduino = new Arduino.Arduino();
            await this.arduino.InitializeAsync();
            this.arduino.I2CDataReceived += this.Arduino_I2CDataReceived;
            this.arduino.StateChangedEventHandler += this.Arduino_StateChangedEventHandler;
        }

        private void InitializeVoiceCommands()
        {
            this.WriteToOutputTextBlock("Initializing system...");

            this.voiceCommandHideTimer = new DispatcherTimer();
            this.voiceCommandHideTimer.Interval = TimeSpan.FromSeconds(5);
            this.voiceCommandHideTimer.Tick += this.VoiceCommandHideTimer_Tick;

            this.voiceController = new VoiceCommandController();
            this.voiceController.ResponseReceived += this.VoiceController_ResponseReceived;
            this.voiceController.CommandReceived += this.VoiceController_CommandReceived;
            this.voiceController.StateChanged += this.VoiceController_StateChanged;
        }

        private async void Arduino_StateChangedEventHandler(object sender, Arduino.Arduino.StateChangedEventArgs e)
        {
            if (e.State == Arduino.Arduino.ConnectionStates.Connected)
            {
                this.isArduinoSlaveSetup = true;
            }
            else if (e.State == Arduino.Arduino.ConnectionStates.Error)
            {
                this.displayManager.AppendImage(DisplayImages.Error, 0, 1);
                this.displayManager.AppendText("Slave error ...", 15, 1);
                var msg = new MessageDialog("Error occured in connecting with slave arduino");
                await msg.ShowAsync();
            }
        }

        private void Arduino_I2CDataReceived(object sender, Arduino.Arduino.I2CDataReceivedEventArgs e)
        {
            this.speedInmPerSecond = Helper.Helpers.ConvertRPSToMeterPerSecond(e.Data.ToString());
            this.UpdateDisplayWithSpeed();
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
            this.ultrasonictimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
        }

        private void InitializeGps()
        {
            this.gpsInformation = new GpsInformation(Constants.GpsBaudRate);
            this.currentGpsStatus = GpsInformation.GpsStatus.None;
            this.gpsInformation.StateChangedEventHandler += this.GpsInformation_StateChangedEventHandler;
            this.gpsInformation.DataReceivedEventHandler += this.GpsInformation_DataReceivedEventHandler;
        }

        private void GpsInformation_DataReceivedEventHandler(object sender, GpsInformation.GpsDataReceivedEventArgs e)
        {
            this.currentGpsPosition = e.positionInfo;
        }

        private void GpsInformation_StateChangedEventHandler(object sender, GpsInformation.StateChangedEventArgs e)
        {
            if (this.currentGpsStatus == GpsInformation.GpsStatus.Active &&
                e.State != GpsInformation.GpsStatus.Active)
            {
                this.WriteToOutputTextBlock("GPS connection lost...");
                // this.Speak("GPS connection lost...");
            }

            if (this.currentGpsStatus != e.State)
            {
                this.currentGpsStatus = e.State;
                this.WriteToOutputTextBlock("GPS Status: " + this.currentGpsStatus.ToString());
                this.UpdateUiButtonStates("gps",
                    this.currentGpsStatus == GpsInformation.GpsStatus.Active ?
                        Commands.ToggleCommandState.On :
                        Commands.ToggleCommandState.Off);
            }
        }

        private async Task InitializeVideoService()
        {
            var camera = new Camera();
            var mediaFrameFormats = await camera.GetMediaFrameFormatsAsync();
            ConfigurationFile.SetSupportedVideoFrameFormats(mediaFrameFormats);
            var videoSetting = await ConfigurationFile.Read(mediaFrameFormats);
            this.httpServer = new HttpServer(camera, videoSetting);
        }

        private async void InitializeRfcommServerAsync()
        {
            try
            {
                this.rfcommProvider = new RfcommServiceManager();
                await this.rfcommProvider.InitializeRfcommServer();
                this.rfcommProvider.ConnectionReceivedEventHandler += this.RfcommProvider_ConnectionReceivedEventHandler;
                this.isRfcommSetup = true;
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x800710DF)
            {
                this.isRfcommSetup = false;
                Debug.Write("Make sure your Bluetooth Radio is on: " + ex.Message);
                this.displayManager.AppendImage(DisplayImages.Error, 0, 3);
                this.displayManager.AppendText("Bluetooth off!!", 20, 3);
            }
        }

        private void Compasstimer_Tick(object sender, object e)
        {
            this.UpdateUiButtonStates("compass", Commands.ToggleCommandState.On);
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

                var distance = this.ultrasonicsensor.GetDistance(1000);
                if (distance.Centimeters < Constants.SafeDistanceCm)
                {
                    this.WriteToOutputTextBlock("OBSTACLE in " + distance.Centimeters + "  cm");

                    if (!this.wasObstacleDetected) // new obstacle. Reverse first!
                    {
                        this.wasObstacleDetected = true;
                        this.WriteToOutputTextBlock("Reversing left...");
                        await this.ExecuteCommandAsync(Commands.DriveReverseLeft);
                    }
                    else if (this.wasObstacleDetected) // we are still seing the obstacle to reverse left
                    {
                        this.WriteToOutputTextBlock("Reversing...");

                        await this.ExecuteCommandAsync(Commands.DriveReverse);
                    }
                }
                else if (this.wasObstacleDetected)
                {
                    this.wasObstacleDetected = false;
                    this.WriteToOutputTextBlock("Rover at safe distance...");
                    await this.ExecuteCommandAsync(Commands.DriveForward);
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
        private async void RfcommProvider_ConnectionReceivedEventHandler(
           StreamSocketListener sender,
           StreamSocketListenerConnectionReceivedEventArgs args)
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
            this.UpdateControlModeonUi("Bluetooth", remoteDevice.Name);

            this.writer = new DataWriter(this.socket.OutputStream);
            var reader = new DataReader(this.socket.InputStream);
            var remoteDisconnection = false;

            this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.Default);
            Debug.Write("Connected to Client: " + remoteDevice.Name);

            this.SendMessage("hostip:" + Helper.Helpers.GetIPAddress());
            await Task.Delay(500);
            this.SendMessage("Ready!");

            // Set speed to normal
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

                    // Check if the size of the displayName is expected (otherwise the remote has already terminated the connection)
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

                    // Load the rest of the message since you already know the length of the displayName expected.
                    readLength = await reader.LoadAsync(currentLength);

                    // Check if the size of the displayName is expected (otherwise the remote has already terminated the connection)
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
            this.UpdateControlModeonUi(null, null);

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

            await this.ExecuteCommandAsync(Commands.DriveStop);

            this.displayManager.ClearRow(1);
            this.displayManager.ClearRow(2);
            this.displayManager.ClearRow(3);
            Debug.Write("Disconected");
        }

        private async Task ExecuteCommandAsync(string commandText)
        {
            switch (commandText)
            {
                case Commands.DriveForward:
                    {
                        this.arduino.SendCommand(Commands.DriveForwardValue);
                        this.displayManager.AppendImage(DisplayImages.TopArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveReverse:
                    {
                        this.playbackService.ChangeMediaSource(PlaybackService.SoundFiles.CensorBeep);
                        this.playbackService.PlaySound();

                        this.arduino.SendCommand(Commands.DriveReverseValue);
                        this.displayManager.AppendImage(DisplayImages.BottomArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveLeft:
                    {
                        this.arduino.SendCommand(Commands.DriveLeftValue);
                        this.displayManager.AppendImage(DisplayImages.LeftArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveRight:
                    {
                        this.arduino.SendCommand(Commands.DriveRightValue);
                        this.displayManager.AppendImage(DisplayImages.RightArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveStop:
                    {
                        this.arduino.SendCommand(Commands.DriveStopValue);
                        this.displayManager.ClearRow(2);
                        this.displayManager.AppendImage(DisplayImages.Stop, 0, 3);
                        this.displayManager.AppendText("Stop", 19, 3);

                        break;
                    }
                case Commands.DriveReverseLeft:
                    {
                        this.arduino.SendCommand(Commands.DriveReverseLeftValue);
                        this.displayManager.AppendImage(DisplayImages.LeftArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveReverseRight:
                    {
                        this.arduino.SendCommand(Commands.DriveReverseRightValue);
                        this.displayManager.AppendImage(DisplayImages.RightArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        // this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveAutoModeOn:
                    {
                        // set speed to slow for auto mode
                        this.arduino.SendCommand(Commands.SpeedSlowValue);

                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () =>
                            {
                                this.InitializeDistanceSensor();
                                this.ultrasonictimer.Start();
                            });

                        this.arduino.SendCommand(Commands.DriveForwardValue);

                        this.displayManager.AppendImage(DisplayImages.Logo_16_16, 0, 1);
                        this.Speak("Autonomous mode on!");
                        this.UpdateControlModeonUi("Autonomous", "Autonomous");

                        break;
                    }
                case Commands.DriveAutoModeOff:
                    {
                        this.isAutodriveTimerActive = false;

                        await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () =>
                            {
                                this.ultrasonictimer?.Stop();
                            });

                        this.displayManager.ClearRow(1);
                        this.arduino.SendCommand(Commands.DriveStopValue);

                        // set speed normal when exiting auto mode
                        this.arduino.SendCommand(Commands.SpeedNormalValue);

                        this.Speak("Autonomous mode off!");
                        this.UpdateControlModeonUi(null, null);

                        break;
                    }
                case Commands.SpeedStop:
                    {
                        this.arduino.SendCommand(Commands.SpeedStopValue);

                        break;
                    }
                case Commands.SpeedNormal:
                    {
                        this.arduino.SendCommand(Commands.SpeedNormalValue);

                        break;
                    }
                case Commands.SpeedSlow:
                    {
                        this.arduino.SendCommand(Commands.SpeedSlowValue);

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
                        this.displayManager.AppendImage(DisplayImages.Camera, 0, 2);
                        this.UpdateUiButtonStates("camera", Commands.ToggleCommandState.On);

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
                        this.displayManager.ClearRow(2);
                        this.UpdateUiButtonStates("camera", Commands.ToggleCommandState.Off);

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
                case Commands.Horn:
                    {
                        this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.Horn);

                        break;
                    }
                case Commands.CameraLedOn:
                    {
                        this.cameraLedPin.Write(GpioPinValue.High);
                        this.UpdateUiButtonStates("lights", Commands.ToggleCommandState.On);

                        break;
                    }
                case Commands.CameraLedOff:
                    {
                        this.cameraLedPin.Write(GpioPinValue.Low);
                        this.UpdateUiButtonStates("lights", Commands.ToggleCommandState.Off);

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
                this.WiifiInfoValue.Text = ip.ToString();
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
                     var speedString = this.speedInmPerSecond.ToString(CultureInfo.InvariantCulture);
                     // this.displayManager.AppendText(speedString, 80, 3);
                     this.SpeedInfoValue.Text = speedString;
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

            try
            {
                Commands.ToggleCommandState toggleCommandState;
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
                                this.displayManager.ClearRow(1);
                                this.displayManager.ClearRow(3);
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
                                this.displayManager.AppendImage(DisplayImages.Mic, 0, 1);

                                await this.ExecuteCommandAsync(Commands.SpeedSlow);

                                this.UpdateControlModeonUi("voice", "Voice");
                            }
                            else
                            {
                                // Stop vehicle and reset speed
                                await this.ExecuteCommandAsync(Commands.DriveStop);
                                await this.ExecuteCommandAsync(Commands.SpeedNormal);
                                this.displayManager.ClearRow(1);
                                this.displayManager.ClearRow(3);
                                this.UpdateControlModeonUi(null, null);
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
                        if (Enum.TryParse(voiceCommand.Data, true, out Commands.CameraDirection camerairection))
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
                        if (Enum.TryParse(voiceCommand.Data, true, out Commands.DrivingDirection drivingDirection))
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
                        await this.ExecuteCommandAsync(Commands.DriveStop);
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

        private async void UpdateControlModeonUi(string mode, string displayName)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (string.Equals(mode, "autonomous", StringComparison.OrdinalIgnoreCase))
                {
                    this.ControlerModeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/aucovei.png", UriKind.RelativeOrAbsolute));
                    this.ControlerModeValue.Text = displayName;
                }
                else if (string.Equals(mode, "bluetooth", StringComparison.OrdinalIgnoreCase))
                {
                    this.ControlerModeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/bluetooth.png", UriKind.RelativeOrAbsolute));
                    this.ControlerModeValue.Text = displayName;
                }
                else if (string.Equals(mode, "voice", StringComparison.OrdinalIgnoreCase))
                {
                    this.ControlerModeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/microphone.png", UriKind.RelativeOrAbsolute));
                    this.ControlerModeValue.Text = displayName;
                }
                else
                {
                    this.ControlerModeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/parked.png", UriKind.RelativeOrAbsolute));
                    this.ControlerModeValue.Text = "Parked";
                }
            });
        }

        private async void UpdateUiButtonStates(string contol, Commands.ToggleCommandState state)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (string.Equals(contol, "camera", StringComparison.OrdinalIgnoreCase))
                {
                    this.CameraIndicator.Source = state == Commands.ToggleCommandState.On ? new BitmapImage(new Uri("ms-appx:///Assets/photocameraon.png", UriKind.RelativeOrAbsolute)) : new BitmapImage(new Uri("ms-appx:///Assets/photocameraoff.png", UriKind.RelativeOrAbsolute));
                }
                else if (string.Equals(contol, "lights", StringComparison.OrdinalIgnoreCase))
                {
                    this.CameraLight.Source = state == Commands.ToggleCommandState.On
                        ? new BitmapImage(new Uri("ms-appx:///Assets/lighton.png", UriKind.RelativeOrAbsolute))
                        : new BitmapImage(new Uri("ms-appx:///Assets/lightoff.png", UriKind.RelativeOrAbsolute));
                }
                else if (string.Equals(contol, "gps", StringComparison.OrdinalIgnoreCase))
                {
                    this.GpsModeIcon.Source = state == Commands.ToggleCommandState.On ?
                        new BitmapImage(new Uri("ms-appx:///Assets/locationon.png", UriKind.RelativeOrAbsolute)) :
                        new BitmapImage(new Uri("ms-appx:///Assets/locationoff.png", UriKind.RelativeOrAbsolute));

                    this.GpsModeValue.Text = state == Commands.ToggleCommandState.On
                        ? "Gps Active"
                        : "No Lock";
                }
                else if (string.Equals(contol, "compass", StringComparison.OrdinalIgnoreCase))
                {
                    double? d = this.compass?.GetHeadingInDegrees(this.compass?.ReadRaw());
                    this.CompassModeValue.Text = d.HasValue ? Math.Round(d.Value, 2).ToString(CultureInfo.InvariantCulture) + "°" : "-";
                }
            });
        }

        private async Task AcquireActiveGpsConnectionAsync()
        {
            while (this.currentGpsStatus != GpsInformation.GpsStatus.Active &&
                   !this.navigationCancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), this.navigationCancellationTokenSource.Token);
            }
        }

        private List<GeoCoordinate> wayPoints = new List<GeoCoordinate>()
        {
            new GeoCoordinate(17.455667,78.3009718),
            new GeoCoordinate(17.4556962,78.3012658)
        };

        private int nextWayPointIndex = -1;
        private GeoCoordinate nextWayPoint;
        private const double WAYPOINT_DIST_TOLERANCE_METERS = 5.0;
        private double HEADING_TOLERANCE = 5.0;
        private CancellationTokenSource navigationCancellationTokenSource;

        private async Task StartRoverAsync()
        {
            await this.AcquireActiveGpsConnectionAsync();

            // first waypoint
            this.nextWayPoint = this.GetNextWayPoint();
            if (this.nextWayPoint == null)
            {
                // you have reached
            }

            // initial direction
            await this.ExecuteCommandAsync(Commands.SpeedNormal);

            // get initial direction 
            this.GetDesiredTurnDirection();

            var tasks = new List<Task>()
            {
                this.MoveRoverAsync(),
                this.UpdateDesiredTurnDirectionAsync(),
                this.LoadNextWayPointAsync()
            };

            // execute asynchronously till its cancelled
            Task.WaitAny(tasks.ToArray(),
                this.navigationCancellationTokenSource.Token);

            await this.ExecuteCommandAsync(Commands.SpeedStop);
        }

        private async Task LoadNextWayPointAsync()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                await this.AcquireActiveGpsConnectionAsync();

                var distanceToTarget = this.CalculateDistanceToWayPointMeters();
                if (distanceToTarget > WAYPOINT_DIST_TOLERANCE_METERS)
                {
                    this.nextWayPoint = this.GetNextWayPoint();

                    // No next waypoint and we are enough close to the target
                    if (this.nextWayPoint == null)
                    {
                        this.navigationCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        private GeoCoordinate GetNextWayPoint()
        {
            this.nextWayPointIndex++;
            if (this.nextWayPointIndex == this.wayPoints.Count - 1)
            {
                return null;
            }

            return this.wayPoints[this.nextWayPointIndex];
        }

        private Commands.DrivingDirection turnDirection;
        private const int SAFE_DISTANCE = 72;
        private const int TURN_DISTANCE = 36;
        private const int STOP_DISTANCE = 12;

        private async Task MoveRoverAsync()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                await this.AcquireActiveGpsConnectionAsync();

                switch (this.turnDirection)
                {
                    case Commands.DrivingDirection.Forward:
                        await this.ExecuteCommandAsync(Commands.SpeedNormal);
                        await this.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                    case Commands.DrivingDirection.Reverse:
                        await this.ExecuteCommandAsync(Commands.SpeedNormal);
                        await this.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                    case Commands.DrivingDirection.Left:
                        await this.ExecuteCommandAsync(Commands.SpeedSlow);
                        await this.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                    case Commands.DrivingDirection.Right:
                        await this.ExecuteCommandAsync(Commands.SpeedSlow);
                        await this.ExecuteCommandAsync(Helpers.MapDrivingDirectionToCommand(this.turnDirection));
                        break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        private void GetDesiredTurnDirection()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                GeoCoordinate currentPosition = new GeoCoordinate(
                    this.currentGpsPosition.Latitude.Value,
                    this.currentGpsPosition.Longitude.Value);

                var targetHeading = WayPointHelper.DegreeBearing(currentPosition, this.nextWayPoint);

                // calculate where we need to turn to head to destination
                var headingError = targetHeading -
                                   this.compass.GetHeadingInDegrees(this.compass.ReadRaw());

                // adjust for compass wrap
                if (headingError < -180)
                {
                    headingError += 360;
                }

                if (headingError > 180)
                {
                    headingError -= 360;
                }

                // calculate which way to turn to intercept the targetHeading
                if (Math.Abs(headingError) <= this.HEADING_TOLERANCE)
                {
                    // if within tolerance, don't turn
                    this.turnDirection = Commands.DrivingDirection.Forward;
                }
                else if (Math.Abs(headingError) < 0)
                {
                    this.turnDirection = Commands.DrivingDirection.Left;
                }
                else if (Math.Abs(headingError) > 0)
                {
                    this.turnDirection = Commands.DrivingDirection.Right;
                }
                else
                {
                    this.turnDirection = Commands.DrivingDirection.Forward;
                }
            }
        }

        private async Task UpdateDesiredTurnDirectionAsync()
        {
            while (!this.navigationCancellationTokenSource.Token.IsCancellationRequested)
            {
                await this.AcquireActiveGpsConnectionAsync();

                this.GetDesiredTurnDirection();

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        //void moveAndAvoid()
        //{
        //    if (fwddistance >= SAFE_DISTANCE)       // no close objects in front of car
        //    {
        //        //Serial.println("SAFE_DISTANCE");

        //        if (this.turnDirection == straight)
        //        {
        //            speed = FAST_SPEED;
        //        }
        //        else
        //        {
        //            speed = TURN_SPEED;
        //        }

        //        setDriveSpeed(speed);
        //        setDirection(this.turnDirection);
        //    }
        //    else if (fwddistance > TURN_DISTANCE && fwddistance < SAFE_DISTANCE)    // not yet time to turn, but slow down
        //    {
        //        //Serial.println("BELOW SAFE_DISTANCE");

        //        if (this.turnDirection == straight)
        //        {
        //            speed = NORMAL_SPEED;
        //        }
        //        else
        //        {
        //            speed = TURN_SPEED;
        //            setDirection(this.turnDirection);
        //        }

        //        setDriveSpeed(speed);
        //    }
        //    else if (fwddistance < TURN_DISTANCE && fwddistance > STOP_DISTANCE)  // getting close, time to turn to avoid object
        //    {
        //        speed = SLOW_SPEED;
        //        setDriveSpeed(speed);

        //        //Serial.println("TURN_DISTANCE");
        //        switch (this.turnDirection)
        //        {
        //            case straight:  // going straight currently, so start new turn
        //                if (headingError <= 0)
        //                {
        //                    this.turnDirection = directions::left;
        //                }
        //                else
        //                {
        //                    this.turnDirection = directions::right;
        //                }

        //                setDirection(this.turnDirection);
        //                break;
        //            case left:
        //                setDirection(directions::right);// if already turning left, try right
        //                break;
        //            case right:
        //                setDirection(directions::left);// if already turning left, try elft
        //                break;
        //        } // end SWITCH
        //    }
        //    else if (fwddistance < STOP_DISTANCE)          // too close, stop and back up
        //    {
        //        //Serial.println("STOP_DISTANCE");
        //        setDirection(directions::stopcar); // stop
        //        setDriveSpeed(STOP_SPEED);
        //        delay(10);
        //        this.turnDirection = directions::straight;
        //        setDirection(directions::back); // drive reverse
        //        setDriveSpeed(NORMAL_SPEED);
        //        delay(200);
        //        while (fwddistance < TURN_DISTANCE)       // backup until we get safe clearance
        //        {
        //            //Serial.print("STOP_DISTANCE CALC: ");
        //            //Serial.print(sonarDistance);
        //            //Serial.println();
        //            //if (GPS.parse(GPS.lastNMEA()))
        //            currentHeading = getComapssHeading();    // get our current heading
        //                                                     //processGPSData();
        //            calcDesiredTurn();// calculate how we would optimatally turn, without regard to obstacles
        //            fwddistance = checkSonarManual();
        //            updateDisplay();
        //            delay(100);
        //        } // while (sonarDistance < TURN_DISTANCE)

        //        setDirection(directions::stopcar);        // stop backing up
        //    } // end of IF TOO CLOSE
        //}   // moveAndAvoid()

        private double CalculateDistanceToWayPointMeters()
        {
            GeoCoordinate currentPosition = new GeoCoordinate(
                this.currentGpsPosition.Latitude.Value,
                this.currentGpsPosition.Longitude.Value);

            var distanceToTarget = WayPointHelper.DistanceBetweenGeoCoordinate(
                currentPosition,
                this.nextWayPoint,
                WayPointHelper.DistanceType.Meters);

            return distanceToTarget;
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            /* Cleanup */
            this.arduino?.Dispose();
            this.rfcommProvider?.Dispose();
            this.displayManager?.Dispose();
            this.cameraLedPin?.Dispose();
            this.gpsInformation?.Dispose();
            this.compass?.Dispose();
        }
    }
}