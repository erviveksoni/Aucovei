using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Aucovei.Device.Azure;
using Aucovei.Device.Compass;
using Aucovei.Device.Configuration;
using Aucovei.Device.Devices;
using Aucovei.Device.Gps;
using Aucovei.Device.RfcommService;
using Aucovei.Device.Services;
using Aucovei.Device.Web;
using Windows.Devices.Gpio;
using Windows.Networking.Connectivity;
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
        private HttpServer httpServer;
        private bool isArduinoSlaveSetup;
        private bool isRfcommSetup;
        private bool isSystemInitialized;
        private bool isAutodriveTimerActive;
        private bool isVoiceModeActive = false;
        private PanTiltServo panTiltServo;
        private RfcommServiceManager rfcommProvider;
        private HCSR04 ultrasonicsensor;
        private DispatcherTimer compasstimer;
        private double speedInmPerSecond = 0;
        private PlaybackService playbackService;
        private VoiceCommandController voiceController;
        private DispatcherTimer voiceCommandHideTimer;
        private GpsInformation gpsInformation;
        private GpsInformation.GpsStatus currentGpsStatus;
        private HMC5883L compass;
        private CommandProcessor.CommandProcessor commandProcessor;
        private double temperature;

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
                var gpio = await GpioController.GetDefaultAsync();
                if (gpio == null)
                {
                    throw new IOException("GPIO interface not found");
                }

                this.UpdateControlModeonUi(null, null);

                this.WriteToOutputTextBlock("Setting up distance sensor...");
                this.ultrasonicsensor = new HCSR04(Constants.TriggerPin, Constants.EchoPin);

                this.Speak("Initializing system");
                this.displayManager = new DisplayManager();
                await this.displayManager.Init();
                this.displayManager.AppendImage(DisplayImages.Logo_24_32, 48, 1);
                await Task.Delay(500);
                this.displayManager.ClearDisplay();

                this.displayManager.AppendText("Initializing system...", 0, 0);

                this.WriteToOutputTextBlock("Initializing Gps...");
                this.InitializeGps();

                this.WriteToOutputTextBlock("Initializing Compass...");
                this.compass = new HMC5883L(MeasurementMode.Continuous);
                await this.compass.InitializeAsync();
                this.compasstimer = new DispatcherTimer();
                this.compasstimer.Interval = TimeSpan.FromMilliseconds(500);
                this.compasstimer.Tick += this.Compasstimer_Tick;
                this.compasstimer.Start();

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

                this.WriteToOutputTextBlock("Initializing video service...");
                await this.InitializeVideoService();

                this.WriteToOutputTextBlock("Initializing command processor...");
                this.commandProcessor = new CommandProcessor.CommandProcessor(
                    this.arduino,
                    this.displayManager,
                    this.playbackService,
                    this.httpServer,
                    this.panTiltServo,
                    this.ultrasonicsensor);
                this.commandProcessor.NotifyUIEventHandler += this.NotifyUIEventHandler;
                await this.commandProcessor.InitializeAsync();

                this.WriteToOutputTextBlock("Initializing Rfcomm Service...");
                this.displayManager.AppendText(">Starting Rfcomm Svc...", 5, 3);
                this.InitializeRfcommServerAsync();
                while (!this.isRfcommSetup)
                {
                    await Task.Delay(2000);
                }
                this.displayManager.AppendText(">Started Rfcomm Svc...", 5, 3);

                this.WriteToOutputTextBlock("Initializing waypoint navigator...");
                WayPointNavigator.WayPointNavigator wayPointNavigator = new WayPointNavigator.WayPointNavigator(this.gpsInformation, this.compass, this.commandProcessor);
                wayPointNavigator.NotifyUIEventHandler += this.NotifyUIEventHandler;

                this.WriteToOutputTextBlock("Initializing cloud device connection...");

                var cloudDataProcessor = new CloudDataProcessor(this.commandProcessor, wayPointNavigator);
                cloudDataProcessor.NotifyUIEventHandler += this.NotifyUIEventHandler;
                await cloudDataProcessor.InitializeAsync();

                this.displayManager.ClearDisplay();
                this.displayManager.AppendText("Initialization complete!", 0, 0);
                await Task.Delay(1000);
                this.displayManager.ClearDisplay();

                await this.panTiltServo.Center();

                this.DisplayNetworkInfo();
                this.displayManager.AppendImage(DisplayImages.BluetoothDisconnected, 0, 1);

                cloudDataProcessor.IsTelemetryActive = true;

                this.InitializeVoiceCommands();
                this.WriteToOutputTextBlock("Initializing voice commands...");
                this.voiceController.Initialize();

                NetworkInformation.NetworkStatusChanged += this.NetworkInformation_NetworkStatusChanged;

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
            this.temperature = Helper.Helpers.ReadTemperature(e.Data.ToString());
            this.UpdateDisplayWithSpeed();
        }

        private void InitializeGps()
        {
            this.gpsInformation = new GpsInformation(Constants.GpsBaudRate);
            this.currentGpsStatus = GpsInformation.GpsStatus.None;
            this.gpsInformation.StateChangedEventHandler += this.GpsInformation_StateChangedEventHandler;
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
                this.rfcommProvider = new RfcommServiceManager(this.playbackService, this.displayManager, this.commandProcessor, this.httpServer);
                await this.rfcommProvider.InitializeRfcommServer();
                this.rfcommProvider.NotifyUIEventHandler += this.NotifyUIEventHandler;
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

        private async void NetworkInformation_NetworkStatusChanged(object sender)
        {
            var internetprofile = NetworkInformation.GetInternetConnectionProfile();
            if (internetprofile == null)
            {
                await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedStop);

                await this.commandProcessor.ExecuteCommandAsync(Commands.DriveStop);

                this.WriteToOutputTextBlock("No WIFI network available...");

                this.DisplayNetworkInfo();
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
                     this.TemperatureInfoValue.Text = this.temperature.ToString(CultureInfo.InvariantCulture);
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
                                await this.commandProcessor.ExecuteCommandAsync(Commands.DriveAutoModeOn);
                            }
                            else
                            {
                                await this.commandProcessor.ExecuteCommandAsync(Commands.DriveAutoModeOff);
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

                                await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedSlow);

                                this.UpdateControlModeonUi("voice", "Voice");
                            }
                            else
                            {
                                // Stop vehicle and reset speed
                                await this.commandProcessor.ExecuteCommandAsync(Commands.DriveStop);
                                await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);
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
                                await this.commandProcessor.ExecuteCommandAsync(Commands.CameraOn);
                            }
                            else
                            {
                                await this.commandProcessor.ExecuteCommandAsync(Commands.CameraOff);
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
                                await this.commandProcessor.ExecuteCommandAsync(Commands.CameraLedOn);
                            }
                            else
                            {
                                await this.commandProcessor.ExecuteCommandAsync(Commands.CameraLedOff);
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
                            await this.commandProcessor.ExecuteCommandAsync(Helper.Helpers.MapCameraDirectionToCommand(camerairection));
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
                            await this.commandProcessor.ExecuteCommandAsync(Helper.Helpers.MapDrivingDirectionToCommand(drivingDirection));
                            this.WriteToCommandTextBlock(response, "🏃‍", 5);
                        }
                        break;
                    case VoiceCommandType.Stop:
                        response = "Stopping...";
                        this.Speak(response);
                        await this.commandProcessor.ExecuteCommandAsync(Commands.DriveStop);
                        this.WriteToCommandTextBlock(response, "🛑", 1);
                        this.WriteToOutputTextBlock("Stopping...");
                        break;
                    case VoiceCommandType.Intro:
                        DateTime make = new DateTime(2018, 8, 1);
                        int monthsApart = 12 * (DateTime.UtcNow.Year - make.Year) + DateTime.UtcNow.Month - make.Month;
                        var agemonths = Math.Abs(monthsApart);

                        response = $"Hello! I'am aucovee,a connected car. I love to drive.";
                        this.Speak(response);
                        this.WriteToCommandTextBlock("Hello...", "👋🏽", 10);
                        this.WriteToOutputTextBlock("Hello...");

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

        private void NotifyUIEventHandler(object sender, NotifyUIEventArgs e)
        {
            switch (e.NotificationType)
            {
                default:
                case NotificationType.Console:
                    this.WriteToOutputTextBlock(e.Data);
                    return;
                case NotificationType.ControlMode:
                    this.UpdateControlModeonUi(e.Name, e.Data.ToString());
                    return;
                case NotificationType.ButtonState:

                    var state = (Commands.ToggleCommandState)Enum.Parse(typeof(Commands.ToggleCommandState), e.Data, true);
                    this.UpdateUiButtonStates(e.Name, state);
                    return;
            }
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
                else if (string.Equals(mode, "navigation", StringComparison.OrdinalIgnoreCase))
                {
                    this.ControlerModeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/navigation.png", UriKind.RelativeOrAbsolute));
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

        private void MainPage_Unloaded(object sender, object args)
        {
            /* Cleanup */
            this.arduino?.Dispose();
            this.rfcommProvider?.Dispose();
            this.displayManager?.Dispose();
            this.gpsInformation?.Dispose();
            this.compass?.Dispose();
            this.commandProcessor?.Dispose();
        }
    }
}