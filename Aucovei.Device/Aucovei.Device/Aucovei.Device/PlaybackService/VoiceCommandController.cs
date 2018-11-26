// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace Aucovei.Device.Services
{
    public class VoiceCommandControllerEventArgs : EventArgs
    {
        public object Data;
    }

    public enum VoiceControllerState
    {
        ListenCommand,
        ListenTrigger,
        ProcessCommand,
        ProcessTrigger,
        Idle
    }

    public enum VoiceCommandType
    {
        None,
        Navigate,
        Move,
        Turn,
        Stop,
        VoiceMode,
        Camera,
        Lights,
        AutoDrive,
        Tilt,
        Pan,
        Intro
    }

    public struct VoiceCommand
    {
        public VoiceCommandType CommandType;
        public string CommandText;
        public string Data;
    }

    public class VoiceCommandController
    {
        public const string ROBOT_NAME = "aucovei";

        /// <summary>
        /// The list of key phrases the robot is listening for to start interaction.
        /// </summary>
        private string[] activationPhrases =
        {
            "ok " + ROBOT_NAME,
            "hey " + ROBOT_NAME,
            "hi " + ROBOT_NAME,
            "hello " + ROBOT_NAME
        };

        private string[] navCommands =
        {
            "go to",
            "navigate to",
            "take me to",
            "navigate me to",
        };

        private string[] autoDriveCommands =
        {
            "self driving",
            "auto driving",
            "autonomous mode",
            "autonomous driving",
            "self drive",
            "auto drive",
            "autodrive",
            "selfdrive"
        };

        private string[] selfIntroCommands =
        {
            "introduce your self",
            "introduce yourself",
            "about yourself",
            "about you"
        };

        private string initText = "Say \"Hey " + ROBOT_NAME + "\" to activate me!";

        /// <summary>
        /// Local recognizer used for listening for the robot's name to start interacting.
        /// </summary>
        private SpeechRecognizer triggerRecognizer;

        /// <summary>
        /// Web speech recognizer used for command interpretation.
        /// </summary>
        private SpeechRecognizer speechRecognizer;

        public delegate void VoiceCommandControllerResponseEventHandler(object sender, VoiceCommandControllerEventArgs e);
        public delegate void VoiceCommandControllerCommandReceivedEventHandler(object sender, VoiceCommandControllerEventArgs e);
        public delegate void VoiceCommandControllerStateChangedEventHandler(object sender, VoiceCommandControllerEventArgs e);

        public event VoiceCommandControllerResponseEventHandler ResponseReceived;
        public event VoiceCommandControllerCommandReceivedEventHandler CommandReceived;
        public event VoiceCommandControllerStateChangedEventHandler StateChanged;

        public VoiceControllerState State = VoiceControllerState.Idle;


        /// <summary>
        /// Initializes the speech recognizer.
        /// </summary>
        public async void Initialize()
        {
            // Local recognizer
            this.triggerRecognizer = new SpeechRecognizer();

            var list = new SpeechRecognitionListConstraint(this.activationPhrases);
            this.triggerRecognizer.Constraints.Add(list);
            await this.triggerRecognizer.CompileConstraintsAsync();

            this.triggerRecognizer.ContinuousRecognitionSession.Completed += this.localSessionCompleted;

            this.triggerRecognizer.ContinuousRecognitionSession.ResultGenerated +=
                this.LocalSessionResult;

            //triggerRecognizer.HypothesisGenerated += CommandHypothesisGenerated;


            // Command recognizer (web)
            this.speechRecognizer = new SpeechRecognizer();
            var result = await this.speechRecognizer.CompileConstraintsAsync();

            this.speechRecognizer.ContinuousRecognitionSession.ResultGenerated +=
                this.CommandResultGenerated;

            this.speechRecognizer.HypothesisGenerated += this.CommandHypothesisGenerated;

            this.speechRecognizer.ContinuousRecognitionSession.Completed +=
                this.CommandSessionCompleted;

            await this.StartTriggerRecognizer();

            this.OnResponseReceived(this.initText);
        }

        private async Task StartTriggerRecognizer()
        {
            try
            {
                await this.triggerRecognizer.ContinuousRecognitionSession.StartAsync();
                Debug.WriteLine("Launched trigger recognizer");
                this.OnStateChanged(VoiceControllerState.ListenTrigger);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// This method will stop the trigger recognizer session and launch the command recognizer session.
        /// </summary>
        /// <returns></returns>
        private async Task StartCommandRecognizer()
        {
            try
            {
                await this.triggerRecognizer.ContinuousRecognitionSession.StopAsync();
                await this.speechRecognizer.ContinuousRecognitionSession.StartAsync();
                Debug.WriteLine("Trigger -> Command");
                this.OnStateChanged(VoiceControllerState.ListenCommand);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// This method will launch the trigger recognizer session.
        /// </summary>
        /// <returns></returns>
        private async Task StopCommandRecognizer()
        {
            try
            {
                await this.speechRecognizer.ContinuousRecognitionSession.StopAsync();
                Debug.WriteLine("Stopped command recognizer.");
                this.OnStateChanged(VoiceControllerState.Idle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Method that runs when the local recognizer generates a result.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void LocalSessionResult(
            SpeechContinuousRecognitionSession sender,
            SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            Debug.WriteLine("Received command");
            if (args.Result.Confidence != SpeechRecognitionConfidence.Rejected)
            {
                Debug.WriteLine("Received command: OK");
                await this.StartCommandRecognizer();
                this.OnResponseReceived("TriggerSuccess");
            }
        }

        /// <summary>
        /// This method is called whenever the local recognizer generates a completed event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void localSessionCompleted(
            SpeechContinuousRecognitionSession sender,
            SpeechContinuousRecognitionCompletedEventArgs args)
        {
            string text = args.Status.ToString();
            Debug.WriteLine("EndLocal -> " + text);

            //If the session stopped for some reason not related to success relaunch it.
            if (text != "Success" && text != "MicrophoneUnavailable")
            {
                Debug.WriteLine("Relaunching");
                this.StartTriggerRecognizer();
            }
        }

        /// <summary>
        /// Method that runs when the recognition session is completed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void CommandSessionCompleted(
            SpeechContinuousRecognitionSession sender,
            SpeechContinuousRecognitionCompletedEventArgs args)
        {
            string text = args.Status.ToString();
            Debug.WriteLine("EndCommand -> " + text);
            if (text == "TimeoutExceeded")
            {
                this.OnResponseReceived(text);
                await this.StartTriggerRecognizer();
            }
        }

        /// <summary>
        /// Runs when a hypothesis is generated, displays the text on the screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void CommandHypothesisGenerated(
            SpeechRecognizer sender,
            SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            this.OnResponseReceived(args.Hypothesis.Text);
        }

        /// <summary>
        /// Runs when a final result is created.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void CommandResultGenerated(
            SpeechContinuousRecognitionSession sender,
            SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            try
            {
                string text = args.Result.Text;
                Debug.WriteLine("-> " + text);

                await this.StopCommandRecognizer();

                string[] tokens = text.Split();
                var type = this.ValidateCommand(text);

                if (text.Contains("what can I say"))
                {
                    this.OnResponseReceived("Try: \"Go to room 2011\"");
                    //VoiceFeedback("Try take me to room 2011");
                    await Task.Delay(3000);
                }
                else
                {
                    string dest = null;
                    try
                    {
                        dest = tokens.Last();
                    }
                    catch (Exception)
                    {
                        // Fail silently
                    }

                    this.OnCommandReceived(new VoiceCommand()
                    {
                        CommandType = type,
                        CommandText = text,
                        Data = dest
                    });
                }

                await this.StartTriggerRecognizer();
            }
            catch (Exception ex)
            {
                // Command not in expected format
                Debug.WriteLine("Something Broke!\n {0}", ex.Message);
                await this.FailedCommand();
            }
        }

        private async Task FailedCommand(string text = "Sorry, I didn't understand that.", string error = "Failed Command.")
        {
            // Telemetry.SendReport(Telemetry.MessageType.VoiceCommand, error);
            //VoiceFeedback(text);
            this.OnResponseReceived(text);
            await Task.Delay(3000);
        }

        /// <summary>
        /// Validates a command string against pre-defined key phrases.
        /// </summary>
        /// <param name="spr"></param>
        /// <returns></returns>
        private VoiceCommandType ValidateCommand(string spr)
        {
            foreach (string s in this.navCommands)
            {
                if (spr.Contains(s))
                {
                    return VoiceCommandType.Navigate;
                }
                else if (spr.Contains("lights"))
                {
                    return VoiceCommandType.Lights;
                }
                else if (spr.Contains("look"))
                {
                    return VoiceCommandType.Pan;
                }
                else if (spr.Contains("camera"))
                {
                    return VoiceCommandType.Camera;
                }
                else if (spr.Contains("turn"))
                {
                    return VoiceCommandType.Turn;
                }
                else if (spr.Contains("move"))
                {
                    return VoiceCommandType.Move;
                }
                else if (spr.Contains("voice mode"))
                {
                    return VoiceCommandType.VoiceMode;
                }
                else if (spr.Contains("stop"))
                {
                    return VoiceCommandType.Stop;
                }
            }

            foreach (string s in this.autoDriveCommands)
            {
                if (spr.Contains(s))
                {
                    return VoiceCommandType.AutoDrive;
                }
            }

            foreach (string s in this.selfIntroCommands)
            {
                if (spr.Contains(s))
                {
                    return VoiceCommandType.Intro;
                }
            }

            return VoiceCommandType.None;
        }

        protected virtual void OnResponseReceived(string message)
        {
            ResponseReceived?.Invoke(this, new VoiceCommandControllerEventArgs()
            {
                Data = message
            });
        }

        protected virtual void OnCommandReceived(VoiceCommand command)
        {
            CommandReceived?.Invoke(this, new VoiceCommandControllerEventArgs()
            {
                Data = command
            });
        }

        protected virtual void OnStateChanged(VoiceControllerState state)
        {
            this.State = state;
            StateChanged?.Invoke(this, new VoiceCommandControllerEventArgs()
            {
                Data = state
            });
        }
    }
}
