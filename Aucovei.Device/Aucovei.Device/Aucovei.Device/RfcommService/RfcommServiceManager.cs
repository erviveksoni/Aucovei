using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aucovei.Device.Services;
using Aucovei.Device.Web;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Aucovei.Device.RfcommService
{
    public class RfcommServiceManager : BaseService, IDisposable
    {
        private RfcommServiceProvider rfcommProvider;
        private StreamSocket socket;
        private StreamSocketListener socketListener;
        private DataWriter writer;
        private PlaybackService playbackService;
        private DisplayManager displayManager;
        private CommandProcessor.CommandProcessor commandProcessor;
        private HttpServer httpServer;

        public RfcommServiceManager(
            PlaybackService playbackService,
            DisplayManager displayManager,
            CommandProcessor.CommandProcessor commandProcessor,
            HttpServer httpServer)
        {
            this.playbackService = playbackService;
            this.displayManager = displayManager;
            this.commandProcessor = commandProcessor;
            this.httpServer = httpServer;
            this.commandProcessor.NotifyCallerEventHandler += this.CommandProcessor_NotifyCallerEventHandler;
        }

        private void CommandProcessor_NotifyCallerEventHandler(object sender, CommandProcessor.NotificationDataEventArgs e)
        {
            if (string.Equals(e?.Target, "RFCOMM"))
            {
                this.SendMessage(e?.Data?.ToString());
            }
        }

        public async Task InitializeRfcommServer()
        {
            try
            {
                this.rfcommProvider =
                    await RfcommServiceProvider.CreateAsync(
                        RfcommServiceId.FromUuid(Constants.RfcommDeviceServiceUuid));
            }
            // Catch exception HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE).
            catch (Exception ex) when ((uint)ex.HResult == 0x800710DF)
            {
                Debug.Write("Make sure your Bluetooth Radio is on: " + ex.Message);
                throw;
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
                throw;
            }

            Debug.Write("Listening for incoming connections");
        }

        public async void SendMessage(string message)
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

        /// <summary>
        ///     Invoked when the socket listener accepts an incoming Bluetooth connection.
        /// </summary>
        /// <param name="sender">The socket listener that accepted the connection.</param>
        /// <param name="args">The connection accept parameters, which contain the connected socket.</param>
        private async void OnConnectionReceived(
           StreamSocketListener sender,
           StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("Connection received");

            //if (this.isVoiceModeActive)
            //{
            //    Debug.Write("Voice Mode Active!");
            //    this.SendMessage("Disconnecting device. Aucovei voice mode active.");
            //    return;
            //}

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
            var notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.ControlMode,
                Name = "Bluetooth",
                Data = remoteDevice.Name
            };

            this.NotifyUIEvent(notifyEventArgs);

            this.writer = new DataWriter(this.socket.OutputStream);
            var reader = new DataReader(this.socket.InputStream);
            var remoteDisconnection = false;

            this.playbackService.PlaySoundFromFile(PlaybackService.SoundFiles.Default);
            Debug.Write("Connected to Client: " + remoteDevice.Name);

            this.SendMessage("hostip:" + Helper.Helpers.GetIPAddress());
            await Task.Delay(500);
            this.SendMessage("Ready!");

            // Set speed to normal
            await this.commandProcessor.ExecuteCommandAsync(Commands.SpeedNormal);

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
                    notifyEventArgs = new NotifyUIEventArgs()
                    {
                        NotificationType = NotificationType.Console,
                        Data = message
                    };

                    this.NotifyUIEvent(notifyEventArgs);

                    await this.commandProcessor.ExecuteCommandAsync(message);

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
            var notifyEventArgs = new NotifyUIEventArgs()
            {
                NotificationType = NotificationType.ControlMode,
                Name = "Parked",
                Data = "Parked"
            };

            this.NotifyUIEvent(notifyEventArgs);

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

            await this.commandProcessor.ExecuteCommandAsync(Commands.DriveStop);

            this.displayManager.ClearRow(1);
            this.displayManager.ClearRow(2);
            this.displayManager.ClearRow(3);
            Debug.Write("Disconected");
        }

        public void Dispose()
        {
            this.socket?.Dispose();
            this.socketListener?.Dispose();
        }
    }
}
