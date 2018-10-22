using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Aucovei.Device.RfcommService
{
    public class RfcommServiceManager : IDisposable
    {
        private RfcommServiceProvider rfcommProvider;
        private StreamSocket socket;
        private StreamSocketListener socketListener;

        public delegate void SocketConnectionReceivedEventHandler(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs e);

        public event SocketConnectionReceivedEventHandler ConnectionReceivedEventHandler;

        public RfcommServiceManager()
        {
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

        private void OnConnectionReceived(
            StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            this.ConnectionReceivedEventHandler?.Invoke(sender, args);
        }

        public void Dispose()
        {
            this.socket?.Dispose();
            this.socketListener?.Dispose();
        }
    }
}
