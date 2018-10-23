using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using Microsoft.IoT.DeviceCore;

namespace Aucovei.Device.Azure
{
    public class CloudDataProcessor : IDisposable
    {
        private readonly IDevice _device;
        private DeviceClient deviceClient;
        private bool _disposed = false;
        private JsonSerialize serializer;
        private CommandProcessor.CommandProcessor commandProcessor;
        private CancellationTokenSource tokenSource;
        private WayPointNavigator.WayPointNavigator wayPointNavigator;

        public CloudDataProcessor(CommandProcessor.CommandProcessor commandProcessor, WayPointNavigator.WayPointNavigator wayPointNavigator)
        {
            this.serializer = new JsonSerialize();
            this.commandProcessor = commandProcessor;
            this.wayPointNavigator = wayPointNavigator;

        }

        public async Task InitializeAsync()
        {
            this.tokenSource = new CancellationTokenSource();
            this.deviceClient = DeviceClient.CreateFromConnectionString(this.GetConnectionString());

            this.StartReceiveLoopAsync(this.tokenSource.Token);
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
                    await Task.Delay(TimeSpan.FromSeconds(10), token);

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
                // Logger<>.LogError("Unexpected Exception starting device receive loop: {0}", ex.ToString());
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
                //_logger.LogError(
                //    "{0}{0}*** Exception: Abandon Command ***{0}{0}Command Name: {1}{0}Command: {2}{0}Exception: {3}{0}{0}",
                //    Console.Out.NewLine,
                //    command.CommandName,
                //    command.Command,
                //    ex);
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
                //_logger.LogError(
                //    "{0}{0}*** Exception: Complete Command ***{0}{0}Command Name: {1}{0}Command: {2}{0}Exception: {3}{0}{0}",
                //    Console.Out.NewLine,
                //    command.CommandName,
                //    command.Command,
                //    ex);
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
                //_logger.LogError(
                //    "{0}{0}*** Exception: Reject Command ***{0}{0}Command Name: {1}{0}Command: {2}{0}Exception: {3}{0}{0}",
                //    Console.Out.NewLine,
                //    command.CommandName,
                //    command.Command,
                //    ex);
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
                //_logger.LogError(
                //    "{0}{0}*** Exception: ReceiveAsync ***{0}{0}{1}{0}{0}",
                //    Console.Out.NewLine,
                //    exp);

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
            if (deserializableCommand.CommandName == "DemoRun")
            {
                var command = deserializableCommand.Command;

                try
                {
                    dynamic parameters = WireCommandSchemaHelper.GetParameters(command);
                    if (parameters != null)
                    {
                        //dynamic statusstring = ReflectionHelper.GetNamedPropertyValue(
                        //    parameters,
                        //    "data",
                        //    usesCaseSensitivePropertyNameMatch: true,
                        //    exceptionThrownIfNoMatch: true);

                        dynamic statusstring = null;

                        await this.commandProcessor.ExecuteCommandAsync(statusstring);

                        int count = 0;
                        if (statusstring != null &&
                            int.TryParse(statusstring.ToString(), out count))
                        {
                            return CommandProcessingResult.Success;
                        }
                        else
                        {
                            // setPointTempDynamic is a null reference.
                            return CommandProcessingResult.CannotComplete;
                        }
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

            return CommandProcessingResult.CannotComplete;
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

        private async Task CloseAsync()
        {
            await this.deviceClient.CloseAsync();
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
