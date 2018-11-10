using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aucovei.Device.Helper;
using Aucovei.Device.RfcommService;
using Aucovei.Device.Services;
using Aucovei.Device.Web;
using Newtonsoft.Json.Linq;
using Windows.Devices.Gpio;
using Windows.UI.Core;

namespace Aucovei.Device.CommandProcessor
{
    public class CommandProcessor : BaseService, IDisposable
    {
        private Arduino.Arduino arduino;
        private DisplayManager displayManager;
        private PlaybackService playbackService;
        private HttpServer httpServer;
        private PanTiltServo panTiltServo;
        private GpioPin cameraLedPin;
        private HCSR04 ultrasonicsensor;
        private bool wasObstacleDetected;
        private bool isCameraActive;
        private CancellationTokenSource ultrasonicTimerToken;

        public delegate void NotifyDataEventHandler(object sender, NotificationDataEventArgs e);

        public event NotifyDataEventHandler NotifyCallerEventHandler;

        public CommandProcessor(
            Arduino.Arduino arduino,
            DisplayManager displayManager,
            PlaybackService playbackService,
            HttpServer httpServer,
            PanTiltServo panTiltServo,
            HCSR04 ultrasonicsensor)
        {
            this.arduino = arduino;
            this.displayManager = displayManager;
            this.playbackService = playbackService;
            this.httpServer = httpServer;
            this.panTiltServo = panTiltServo;
            this.ultrasonicsensor = ultrasonicsensor;
            this.arduino.I2CDataReceived += this.Arduino_I2CDataReceived;
        }

        private void Arduino_I2CDataReceived(object sender, Arduino.Arduino.I2CDataReceivedEventArgs e)
        {
            try
            {
                var speedInmPerSecond = Helpers.ConvertRPSToMeterPerSecond(e.Data.ToString());
                var temperature = Helpers.ReadTemperature(e.Data.ToString());
                dynamic telemetry = new System.Dynamic.ExpandoObject();
                telemetry.Temperature = temperature;
                telemetry.IsCameraActive = this.isCameraActive;
                telemetry.RoverSpeed = speedInmPerSecond;
                telemetry.DeviceIp = this.isCameraActive ? Helpers.GetIPAddress()?.ToString() : null;

                this.OnNotifyDataEventHandler("AZURE", JObject.FromObject(telemetry));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public async Task InitializeAsync()
        {
            var gpio = await GpioController.GetDefaultAsync();
            this.cameraLedPin = gpio.OpenPin(Constants.LedPin);
            this.cameraLedPin.Write(GpioPinValue.Low);
            this.cameraLedPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void InitializeDistanceSensor()
        {
            this.ultrasonicTimerToken?.Cancel();
            this.ultrasonicTimerToken = new CancellationTokenSource();
            this.wasObstacleDetected = false;
            this.UltrasonictimerLoopAsync();
        }

        public async Task ExecuteCommandAsync(string commandText, object commandData = null)
        {
            switch (commandText)
            {
                case Commands.DriveForward:
                    {
                        this.arduino.SendCommand(Commands.SpeedNormalValue);

                        this.arduino.SendCommand(Commands.DriveForwardValue);
                        this.displayManager.AppendImage(DisplayImages.TopArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveReverse:
                    {
                        this.arduino.SendCommand(Commands.SpeedNormalValue);

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
                        this.arduino.SendCommand(Commands.SpeedSlowValue);

                        this.arduino.SendCommand(Commands.DriveLeftValue);
                        this.displayManager.AppendImage(DisplayImages.LeftArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);

                        break;
                    }
                case Commands.DriveRight:
                    {
                        this.arduino.SendCommand(Commands.SpeedSlowValue);

                        this.arduino.SendCommand(Commands.DriveRightValue);
                        this.displayManager.AppendImage(DisplayImages.RightArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);

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
                        this.arduino.SendCommand(Commands.SpeedNormalValue);

                        this.arduino.SendCommand(Commands.DriveReverseLeftValue);
                        this.displayManager.AppendImage(DisplayImages.LeftArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        //this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveReverseRight:
                    {
                        this.arduino.SendCommand(Commands.SpeedNormalValue);

                        this.arduino.SendCommand(Commands.DriveReverseRightValue);
                        this.displayManager.AppendImage(DisplayImages.RightArrow, 0, 3);
                        this.displayManager.AppendText("Drive", 19, 3);
                        // this.UpdateDisplayWithSpeed();

                        break;
                    }
                case Commands.DriveAutoModeOn:
                    {
                        await TaskHelper.DispatchAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Task.FromResult(true);

                            this.InitializeDistanceSensor();
                        });

                        this.arduino.SendCommand(Commands.SpeedNormalValue);
                        this.arduino.SendCommand(Commands.DriveForwardValue);

                        this.displayManager.AppendImage(DisplayImages.Logo_16_16, 0, 1);
                        this.Speak("Autonomous mode on!");
                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.ControlMode,
                            Name = "Autonomous",
                            Data = "Autonomous"
                        };

                        this.NotifyUIEvent(notifyEventArgs);

                        break;
                    }
                case Commands.DriveAutoModeOff:
                    {
                        await TaskHelper.DispatchAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            this.ultrasonicTimerToken?.Cancel();
                        });

                        this.displayManager.ClearRow(1);
                        this.arduino.SendCommand(Commands.DriveStopValue);
                        this.arduino.SendCommand(Commands.SpeedStopValue);

                        this.Speak("Autonomous mode off!");
                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.ControlMode,
                            Name = "Parked",
                            Data = "Parked"
                        };

                        this.NotifyUIEvent(notifyEventArgs);

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

                        this.OnNotifyDataEventHandler("RFCOMM", "CAMON");
                        this.displayManager.AppendImage(DisplayImages.Camera, 0, 2);

                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.ButtonState,
                            Name = "camera",
                            Data = Commands.ToggleCommandState.On.ToString()
                        };

                        this.isCameraActive = true;
                        this.NotifyUIEvent(notifyEventArgs);

                        break;
                    }
                case Commands.CameraOff:
                    {
                        var server = this.httpServer;
                        if (server != null)
                        {
                            await server?.Stop();
                        }

                        this.OnNotifyDataEventHandler("RFCOMM", "CAMOFF");
                        this.displayManager.ClearRow(2);
                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.ButtonState,
                            Name = "camera",
                            Data = Commands.ToggleCommandState.Off.ToString()
                        };

                        this.isCameraActive = false;
                        this.NotifyUIEvent(notifyEventArgs);

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
                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.ButtonState,
                            Name = "lights",
                            Data = Commands.ToggleCommandState.On.ToString()
                        };

                        this.NotifyUIEvent(notifyEventArgs);

                        break;
                    }
                case Commands.CameraLedOff:
                    {
                        this.cameraLedPin.Write(GpioPinValue.Low);
                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.ButtonState,
                            Name = "lights",
                            Data = Commands.ToggleCommandState.Off.ToString()
                        };

                        this.NotifyUIEvent(notifyEventArgs);

                        break;
                    }
                default:
                    {
                        this.Speak(commandText);

                        break;
                    }
            }
        }

        private void OnNotifyDataEventHandler(string target, object data)
        {
            this.NotifyCallerEventHandler?.Invoke(this, new NotificationDataEventArgs()
            {
                Target = target,
                Data = data
            });
        }

        /// <summary>
        /// Speaks text using text-to-speech
        /// </summary>
        /// <param name="text">Text to speak</param>
        private async void Speak(string text)
        {
            await TaskHelper.DispatchAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await this.playbackService.SynthesizeTextAsync(text);
            });
        }

        private async void UltrasonictimerLoopAsync()
        {
            while (!this.ultrasonicTimerToken.IsCancellationRequested)
            {
                try
                {
                    var distance = this.ultrasonicsensor.GetDistance(1000);
                    if (distance.Centimeters < Constants.SafeDistanceCm)
                    {
                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.Console,
                            Data = "OBSTACLE in " + distance.Centimeters + "  cm"
                        };

                        this.NotifyUIEvent(notifyEventArgs);

                        this.wasObstacleDetected = true;

                        notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.Console,
                            Data = "Reversing..."
                        };

                        this.NotifyUIEvent(notifyEventArgs);

                        await this.ExecuteCommandAsync(Commands.DriveReverse);

                        await Task.Delay(TimeSpan.FromMilliseconds(500));

                        await this.ExecuteCommandAsync(Commands.DriveReverseLeft);

                        await Task.Delay(TimeSpan.FromMilliseconds(1000));
                    }
                    else if (this.wasObstacleDetected)
                    {
                        this.wasObstacleDetected = false;

                        NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                        {
                            NotificationType = NotificationType.Console,
                            Data = "Rover at safe distance..."
                        };

                        this.NotifyUIEvent(notifyEventArgs);

                        await this.ExecuteCommandAsync(Commands.DriveForward);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50));
                    }
                }
                catch (Exception ex)
                {
                    Debug.Write(ex);

                    NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                    {
                        NotificationType = NotificationType.Console,
                        Data = $"Autonomous mode error... {ex.Message}"
                    };

                    this.NotifyUIEvent(notifyEventArgs);
                }
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.arduino?.Dispose();
                    this.cameraLedPin?.Dispose();
                    this.displayManager?.Dispose();
                    this.ultrasonicTimerToken?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CommandProcessor() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class NotificationDataEventArgs : EventArgs
    {
        public string Target;

        public object Data;
    }
}
