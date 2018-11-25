using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aucovei.Device.RfcommService;
using Aucovei.Device.WayPointNavigator;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aucovei.Device.Azure
{
    public class CloudDataProcessor : BaseService, IDisposable
    {
        private DeviceClient deviceClient;
        private bool _disposed = false;
        private JsonSerialize serializer;
        private CommandProcessor.CommandProcessor commandProcessor;
        private CancellationTokenSource tokenSource;
        private WayPointNavigator.WayPointNavigator wayPointNavigator;
        public bool activateExternalTemperature;
        private const int TELEMETRY_REPORT_FREQUENCY_IN_SECONDS = 5;
        private const int DEVICE_INFO_REPORT_FREQUENCY_IN_SECONDS = 600;

        private JObject reportedTelemetry;
        public bool IsTelemetryActive { get; set; }

        public CloudDataProcessor(
            CommandProcessor.CommandProcessor commandProcessor,
            WayPointNavigator.WayPointNavigator wayPointNavigator
        )
        {
            this.serializer = new JsonSerialize();
            this.commandProcessor = commandProcessor;
            this.wayPointNavigator = wayPointNavigator;
            this.commandProcessor.NotifyCallerEventHandler += this.CommandProcessor_NotifyCallerEventHandler;
        }

        public async Task InitializeAsync()
        {
            this.tokenSource = new CancellationTokenSource();
            this.deviceClient = DeviceClient.CreateFromConnectionString(this.GetConnectionString(), TransportType.Amqp);

            // Purge the commands
            // await this.PurgeCommandQueueAsync(this.tokenSource.Token);

            this.SendDeviceInfoAsync(this.tokenSource.Token);
            this.StartReceiveLoopAsync(this.tokenSource.Token);
            this.StartSendLoopAsync(this.tokenSource.Token);

            await Task.FromResult(true);
        }

        public async void SendDeviceInfoAsync(CancellationToken token)
        {
            Exception exception = null;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (this.IsTelemetryActive)
                    {
                        string deviceinfo = this.GetDeviceInfo();
                        await this.SendEventAsync(JObject.Parse(deviceinfo));
                    }

                    await Task.Delay(TimeSpan.FromSeconds(DEVICE_INFO_REPORT_FREQUENCY_IN_SECONDS), token);
                }
            }
            catch (IotHubException ex)
            {
                exception = ex;
            }

            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                Debug.WriteLine(exception);
            }
        }

        private string GetDeviceInfo()
        {
            dynamic device = DeviceSchemaHelper.BuildDeviceStructure(Constants.DeviceId);
            device.DeviceProperties = DeviceSchemaHelper.GetDeviceProperties(device);
            device.DeviceProperties.Latitude = this.wayPointNavigator.CurrentGpsPosition?.Latitude;
            device.DeviceProperties.Longitude = this.wayPointNavigator.CurrentGpsPosition?.Longitude;
            device.Commands = CommandSchemaHelper.GetSupportedCommands(device);
            device.Telemetry = CommandSchemaHelper.GetTelemetrySchema(device);
            device.Version = Constants.VERSION_2_0;
            device.ObjectType = Constants.OBJECT_TYPE_DEVICE_INFO;

            // Remove the system properties from a device, to better emulate the behavior of real devices when sending device info messages.
            DeviceSchemaHelper.RemoveSystemPropertiesForSimulatedDeviceInfo(device);

            return JsonConvert.SerializeObject(device);
        }

        private async Task PurgeCommandQueueAsync(CancellationToken token)
        {
            try
            {
                Message msg = null;

                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = "Purging device command queue..."
                };

                this.NotifyUIEvent(notifyEventArgs);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Retrieve the message from the IoT Hub
                        msg = await this.deviceClient.ReceiveAsync();

                        if (msg == null)
                        {
                            notifyEventArgs = new NotifyUIEventArgs()
                            {
                                NotificationType = NotificationType.Console,
                                Data = "Finished purging device command queue..."
                            };

                            this.NotifyUIEvent(notifyEventArgs);

                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }

                    if (msg != null)
                    {
                        await this.deviceClient.RejectAsync(msg.LockToken);

                        // Pause before running through the receive loop
                        await Task.Delay(TimeSpan.FromMilliseconds(50));
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("Unexpected exception while purging device commands queue: {0}", ex.ToString())
                };

                this.NotifyUIEvent(notifyEventArgs);
            }
        }

        private async void StartReceiveLoopAsync(CancellationToken token)
        {
            DeserializableCommand command;
            Exception exception;
            CommandProcessingResult processingResult;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    command = null;
                    exception = null;

                    // Pause before running through the receive loop
                    await Task.Delay(TimeSpan.FromSeconds(1), token);

                    try
                    {
                        // Retrieve the message from the IoT Hub
                        command = await this.ReceiveAsync();

                        if (command == null)
                        {
                            continue;
                        }

                        processingResult =
                            await this.HandleCommandAsync(command);

                        switch (processingResult)
                        {
                            case CommandProcessingResult.CannotComplete:
                                await this.SignalRejectedCommand(command);
                                break;

                            case CommandProcessingResult.RetryLater:
                                await this.SignalAbandonedCommand(command);
                                break;

                            case CommandProcessingResult.Success:
                                await this.SignalCompletedCommand(command);
                                break;
                        }
                    }
                    catch (IotHubException ex)
                    {
                        exception = ex;
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }

                    if ((command != null) &&
                        (exception != null))
                    {
                        await this.SignalAbandonedCommand(command);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                //do nothing if the task was cancelled
            }
            catch (Exception ex)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("Unexpected Exception starting device receive loop: {0}", ex.ToString())
                };

                this.NotifyUIEvent(notifyEventArgs);
            }
        }

        public async void StartSendLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await this.SendTelemetryAsync();

                await Task.Delay(TimeSpan.FromSeconds(TELEMETRY_REPORT_FREQUENCY_IN_SECONDS), token);
            }
        }

        private async Task SendTelemetryAsync()
        {
            var monitorData = new RemoteMonitorTelemetryData();

            if (this.IsTelemetryActive && this.reportedTelemetry != null)
            {
                monitorData.DeviceId = Constants.DeviceId;

                string value = JsonConvert.SerializeObject(this.reportedTelemetry["Temperature"] ?? string.Empty);
                if (double.TryParse(value, out double temp))
                {
                    monitorData.Temperature = temp;
                }

                value = JsonConvert.SerializeObject(this.reportedTelemetry["RoverSpeed"] ?? string.Empty);
                if (double.TryParse(value, out double val))
                {
                    monitorData.Speed = val;
                }

                value = JsonConvert.SerializeObject(this.reportedTelemetry["IsCameraActive"] ?? string.Empty);
                if (bool.TryParse(value, out bool cameraFlag))
                {
                    monitorData.CameraStatus = cameraFlag;
                }

                value = JsonConvert.SerializeObject(this.reportedTelemetry["DeviceIp"] ?? string.Empty);
                monitorData.DeviceIp = value;

                value = JsonConvert.SerializeObject(this.reportedTelemetry["IsObstacleDetected"] ?? string.Empty);
                if (bool.TryParse(value, out bool isObstacleDetected))
                {
                    monitorData.IsObstacleDetected = isObstacleDetected;
                }

                if (this.activateExternalTemperature)
                {
                    // monitorData.ExternalTemperature = _externalTemperatureGenerator.GetNextValue();
                }
                else
                {
                    monitorData.ExternalTemperature = null;
                }

                //_logger.LogInfo("Sending " + messageBody + " for Device: " + _deviceId);

                await this.SendEventAsync(JObject.FromObject(monitorData));
            }
        }

        private async Task SignalAbandonedCommand(DeserializableCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            Debug.Assert(
                !string.IsNullOrEmpty(command.LockToken),
                "command.LockToken is a null reference or empty string.");

            try
            {
                await this.deviceClient.AbandonAsync(command.LockToken);
            }
            catch (Exception ex)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("{0}{0}*** Exception: Abandon Command ***{0}{0}Command Name: {1}{0}Command: {2}{0}Exception: {3}{0}{0}",
                        Console.Out.NewLine,
                        command.CommandName,
                        command.Command,
                        ex)
                };

                this.NotifyUIEvent(notifyEventArgs);
            }

        }

        private async Task SignalCompletedCommand(DeserializableCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            Debug.Assert(
                !string.IsNullOrEmpty(command.LockToken),
                "command.LockToken is a null reference or empty string.");

            try
            {
                await this.deviceClient.CompleteAsync(command.LockToken);
            }
            catch (Exception ex)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("{0}{0}*** Exception: Complete Command ***{0}{0}Command Name: {1}{0}Command: {2}{0}Exception: {3}{0}{0}",
                        Console.Out.NewLine,
                        command.CommandName,
                        command.Command,
                        ex)
                };

                this.NotifyUIEvent(notifyEventArgs);
            }
        }

        private async Task SignalRejectedCommand(DeserializableCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            Debug.Assert(
                !string.IsNullOrEmpty(command.LockToken),
                "command.LockToken is a null reference or empty string.");

            try
            {
                await this.deviceClient.RejectAsync(command.LockToken);
            }
            catch (Exception ex)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("{0}{0}*** Exception: Reject Command ***{0}{0}Command Name: {1}{0}Command: {2}{0}Exception: {3}{0}{0}",
                        Console.Out.NewLine,
                        command.CommandName,
                        command.Command,
                        ex)
                };

                this.NotifyUIEvent(notifyEventArgs);
            }
        }

        private async Task<DeserializableCommand> ReceiveAsync()
        {
            Microsoft.Azure.Devices.Client.Message message = null;
            Exception exp = null;
            try
            {
                message = await this.deviceClient.ReceiveAsync();
            }
            catch (Exception exception)
            {
                exp = exception;
            }

            if (exp != null)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("{0}{0}*** Exception: ReceiveAsync ***{0}{0}{1}{0}{0}",
                       Console.Out.NewLine,
                        exp)
                };

                this.NotifyUIEvent(notifyEventArgs);

                if (message != null)
                {
                    await this.deviceClient.AbandonAsync(message);
                }
            }

            if (message != null)
            {
                return new DeserializableCommand(message, this.serializer);
            }

            return null;
        }

        private async Task<CommandProcessingResult> HandleCommandAsync(DeserializableCommand deserializableCommand)
        {
            var notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.Console,
                Data = $"Received cloud command: {deserializableCommand.CommandName}"
            };

            this.NotifyUIEvent(notifyEventArgs);

            if (deserializableCommand.CommandName == "DemoRun")
            {
                return await this.ExecuteDemoCommandAsync(deserializableCommand);
            }
            else if (deserializableCommand.CommandName == "SendWaypoints")
            {
                return await this.ExecuteSendWaypointsCommandAsync(deserializableCommand);
            }
            else if (deserializableCommand.CommandName == "SetLights")
            {
                return await this.ExecuteSetLightsCommandAsync(deserializableCommand);
            }
            else if (deserializableCommand.CommandName == "SetCamera")
            {
                return await this.ExecuteSetCameraCommandAsync(deserializableCommand);
            }
            else if (deserializableCommand.CommandName == "SendBuzzer")
            {
                return await this.ExecuteSendBuzzerCommandAsync();
            }
            else if (deserializableCommand.CommandName == "EmergencyStop")
            {
                return await this.ExecuteEmergencyStopCommandAsync();
            }
            else if (deserializableCommand.CommandName == "StartTelemetry")
            {
                return this.ExecuteStartStopTelemetryCommandAsync(true);
            }
            else if (deserializableCommand.CommandName == "StopTelemetry")
            {
                return this.ExecuteStartStopTelemetryCommandAsync(false);
            }
            else if (deserializableCommand.CommandName == "MoveVehicle")
            {
                return await this.ExecuteMoveVehicleCommandAsync(deserializableCommand);
            }
            else if (deserializableCommand.CommandName == "MoveCamera")
            {
                return await this.ExecuteMoveCameraCommandAsync(deserializableCommand);
            }
            else
            {
                notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = $"Command not registered in the system: {deserializableCommand.CommandName}"
                };

                this.NotifyUIEvent(notifyEventArgs);
            }

            return CommandProcessingResult.CannotComplete;
        }

        private async Task<CommandProcessingResult> ExecuteDemoCommandAsync(DeserializableCommand deserializableCommand)
        {
            var command = deserializableCommand.Command;

            try
            {
                dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                if (parameters != null)
                {
                    string statusstring = ReflectionHelper.GetNamedPropertyValue(
                        parameters,
                        "data",
                        usesCaseSensitivePropertyNameMatch: true,
                        exceptionThrownIfNoMatch: true);

                    int count = 0;
                    if (statusstring != null &&
                        int.TryParse(statusstring.ToString(), out count))
                    {
                        await this.commandProcessor.ExecuteCommandAsync(Commands.DriveForward);
                        await this.commandProcessor.ExecuteCommandAsync(Commands.CameraLedOn);

                        while (count > 0)
                        {
                            count--;
                            await Task.Delay(200);
                        }

                        await this.commandProcessor.ExecuteCommandAsync(Commands.Horn);
                        await this.commandProcessor.ExecuteCommandAsync(Commands.CameraLedOff);
                        await this.commandProcessor.ExecuteCommandAsync(Commands.DriveStop);

                        return CommandProcessingResult.Success;
                    }

                    // setPointTempDynamic is a null reference.
                    return CommandProcessingResult.CannotComplete;
                }
                else
                {
                    // parameters is a null reference.
                    return CommandProcessingResult.CannotComplete;
                }
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteSendWaypointsCommandAsync(DeserializableCommand deserializableCommand)
        {
            var command = deserializableCommand.Command;

            try
            {
                dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                if (parameters != null)
                {
                    string waypointString = ReflectionHelper.GetNamedPropertyValue(
                        parameters,
                        "data",
                        usesCaseSensitivePropertyNameMatch: true,
                        exceptionThrownIfNoMatch: true);
                    if (!string.IsNullOrEmpty(waypointString))
                    {
                        JArray waypointArray = JArray.Parse(waypointString);
                        if (waypointArray != null && waypointArray.Count > 0)
                        {
                            var wayPoints = new List<GeoCoordinate>();
                            foreach (var wayPoint in waypointArray)
                            {
                                if (double.TryParse(wayPoint[0].ToString(), out var lat) &&
                                    double.TryParse(wayPoint[1].ToString(), out var lon))
                                {
                                    var wayPointObject = new GeoCoordinate(lat, lon);
                                    wayPoints.Add(wayPointObject);
                                }
                            }

                            if (wayPoints.Count > 0)
                            {
                                await this.wayPointNavigator.StartWayPointNavigationAsync(wayPoints);
                            }

                            return CommandProcessingResult.Success;
                        }
                    }
                }

                return CommandProcessingResult.CannotComplete;
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteSetCameraCommandAsync(DeserializableCommand deserializableCommand)
        {
            var command = deserializableCommand.Command;

            try
            {
                dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                if (parameters != null)
                {
                    string value = ReflectionHelper.GetNamedPropertyValue(
                        parameters,
                        "data",
                        usesCaseSensitivePropertyNameMatch: true,
                        exceptionThrownIfNoMatch: true);

                    if (bool.TryParse(value, out var result))
                    {
                        var cmd = result ? Commands.CameraOn : Commands.CameraOff;
                        await this.commandProcessor.ExecuteCommandAsync(cmd);
                        return CommandProcessingResult.Success;
                    }

                    // setPointTempDynamic is a null reference.
                    return CommandProcessingResult.CannotComplete;
                }
                else
                {
                    // parameters is a null reference.
                    return CommandProcessingResult.CannotComplete;
                }
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteSetLightsCommandAsync(DeserializableCommand deserializableCommand)
        {
            var command = deserializableCommand.Command;

            try
            {
                dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                if (parameters != null)
                {
                    string value = ReflectionHelper.GetNamedPropertyValue(
                        parameters,
                        "data",
                        usesCaseSensitivePropertyNameMatch: true,
                        exceptionThrownIfNoMatch: true);

                    if (bool.TryParse(value, out var result))
                    {
                        var cmd = result ? Commands.CameraLedOn : Commands.CameraLedOff;
                        await this.commandProcessor.ExecuteCommandAsync(cmd);
                        return CommandProcessingResult.Success;
                    }

                    // setPointTempDynamic is a null reference.
                    return CommandProcessingResult.CannotComplete;
                }
                else
                {
                    // parameters is a null reference.
                    return CommandProcessingResult.CannotComplete;
                }
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteSendBuzzerCommandAsync()
        {
            try
            {
                await this.commandProcessor.ExecuteCommandAsync(Commands.Horn);

                return CommandProcessingResult.Success;
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteEmergencyStopCommandAsync()
        {
            try
            {
                await this.commandProcessor.ExecuteCommandAsync(Commands.DriveStop);

                return CommandProcessingResult.Success;
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private CommandProcessingResult ExecuteStartStopTelemetryCommandAsync(bool startTelemetry)
        {
            try
            {
                this.IsTelemetryActive = startTelemetry;
                return CommandProcessingResult.Success;
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteMoveVehicleCommandAsync(
            DeserializableCommand deserializableCommand)
        {
            var command = deserializableCommand.Command;

            try
            {
                dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                if (parameters != null)
                {
                    string value = ReflectionHelper.GetNamedPropertyValue(
                        parameters,
                        "data",
                        usesCaseSensitivePropertyNameMatch: true,
                        exceptionThrownIfNoMatch: true);

                    if (!string.IsNullOrEmpty(value) &&
                        Enum.TryParse(
                            typeof(Commands.DrivingDirection),
                            value.Trim(),
                            true,
                            out var direction))
                    {
                        var cmddir = (Commands.DrivingDirection)direction;
                        string drivingdirection = Helper.Helpers.MapDrivingDirectionToCommand(cmddir);
                        await this.commandProcessor.ExecuteCommandAsync(drivingdirection);

                        return CommandProcessingResult.Success;
                    }
                }

                return CommandProcessingResult.CannotComplete;
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async Task<CommandProcessingResult> ExecuteMoveCameraCommandAsync(
            DeserializableCommand deserializableCommand)
        {
            var command = deserializableCommand.Command;

            try
            {
                dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                if (parameters != null)
                {
                    string value = ReflectionHelper.GetNamedPropertyValue(
                        parameters,
                        "data",
                        usesCaseSensitivePropertyNameMatch: true,
                        exceptionThrownIfNoMatch: true);

                    if (!string.IsNullOrEmpty(value) &&
                        Enum.TryParse(
                            typeof(Commands.CameraDirection),
                            value.Trim(),
                            true,
                            out var direction))
                    {
                        string camdirection = Helper.Helpers.MapCameraDirectionToCommand((Commands.CameraDirection)direction);
                        await this.commandProcessor.ExecuteCommandAsync(camdirection);

                        return CommandProcessingResult.Success;
                    }
                }

                return CommandProcessingResult.CannotComplete;
            }
            catch (Exception)
            {
                return CommandProcessingResult.RetryLater;
            }
        }

        private async void CommandProcessor_NotifyCallerEventHandler(object sender, CommandProcessor.NotificationDataEventArgs e)
        {
            if (string.Equals(e?.Target, "AZURE"))
            {
                this.reportedTelemetry = e?.Data as JObject;

                if (e.IsUrgent)
                {
                    // send tele instance right now
                    await this.SendTelemetryAsync();
                }
            }
        }

        private async Task SendEventAsync(JObject eventData)
        {
            var eventId = Guid.NewGuid();
            string objectType = this.GetObjectType(eventData);
            var objectTypePrefix = Constants.ObjectTypePrefix;

            if (!string.IsNullOrWhiteSpace(objectType) &&
                !string.IsNullOrEmpty(objectTypePrefix))
            {
                eventData["ObjectType"] = objectTypePrefix + objectType;
            }

            var bytes = this.serializer.SerializeObject(eventData);

            var message = new Microsoft.Azure.Devices.Client.Message(bytes);
            message.Properties["EventId"] = eventId.ToString();

            try
            {
                await this.deviceClient.SendEventAsync(message);
            }
            catch (Exception ex)
            {
                NotifyUIEventArgs notifyEventArgs = new NotifyUIEventArgs()
                {
                    NotificationType = NotificationType.Console,
                    Data = string.Format("{0}{0}*** Exception: SendEventAsync ***{0}{0}EventId: {1}{0}Event Data: {2}{0}Exception: {3}{0}{0}",
                        Console.Out.NewLine,
                        ex)
                };

                this.NotifyUIEvent(notifyEventArgs);
            }
        }

        private string GetObjectType(JObject eventData)
        {
            if (eventData == null)
            {
                throw new ArgumentNullException("eventData");
            }

            if (eventData["ObjectType"] == null)
            {
                return string.Empty;
            }

            return eventData["ObjectType"].ToString();
        }

        /// <summary>
        /// Builds the IoT Hub connection string
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private string GetConnectionString()
        {
            string key = Constants.PrimaryAuthKey;
            string deviceID = Constants.DeviceId;
            string hostName = Constants.HostName;

            var authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(deviceID, key);
            return Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder.Create(hostName, authMethod).ToString();
        }

        /// <summary>
        /// Implement the IDisposable interface in order to close the device manager
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.deviceClient != null)
                {
                    this.deviceClient.CloseAsync().Wait();
                }

                this.tokenSource?.Dispose();
            }

            this._disposed = true;
        }

        ~CloudDataProcessor()
        {
            this.Dispose(false);
        }
    }

    public enum CommandProcessingResult
    {
        Success = 0,
        RetryLater,
        CannotComplete
    }
}
