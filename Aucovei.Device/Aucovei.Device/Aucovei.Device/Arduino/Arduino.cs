
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.I2c;
using Windows.UI.Xaml;

namespace Aucovei.Device.Arduino
{
    public class Arduino : IDisposable
    {
        #region Address
        public const byte SLAVEADDRESS = 0x40;
        #endregion

        private I2cDevice sensor;
        private readonly DispatcherTimer arduinoDataReadTimer;

        public enum ConnectionStates
        {
            Connecting,
            Error,
            Connected
        }

        public ConnectionStates ConnectionState { get; private set; }

        public delegate void I2CDataReceivedEventHandler(object sender, I2CDataReceivedEventArgs e);
        public delegate void I2CStateChangedEventHandler(object sender, StateChangedEventArgs e);

        public event I2CDataReceivedEventHandler I2CDataReceived;
        public event I2CStateChangedEventHandler StateChangedEventHandler;

        public Arduino()
        {
            this.arduinoDataReadTimer = new DispatcherTimer();
        }

        public async Task InitializeAsync()
        {
            var settings = new I2cConnectionSettings(SLAVEADDRESS);
            settings.BusSpeed = I2cBusSpeed.FastMode;

            var controller = await I2cController.GetDefaultAsync();
            this.sensor = controller.GetDevice(settings);
            this.ConnectionState = ConnectionStates.Connecting;
            this.OnStateChanged();

            this.arduinoDataReadTimer.Tick += this.I2CReadTimer_Tick; // We will create an event handler 
            this.arduinoDataReadTimer.Interval = new TimeSpan(0, 0, 0, 0, 100); // Timer_Tick is executed every 500 milli second
            this.arduinoDataReadTimer.Start();
        }

        public void SendCommand(string data)
        {
            if (this.ConnectionState == ConnectionStates.Connected)
            {
                var adata = Encoding.Unicode.GetBytes(data);
                this.sensor.Write(adata);
            }
            else
            {
                throw new InvalidOperationException("Device is not connected");
            }
        }

        private async void I2CReadTimer_Tick(object sender, object e)
        {
            var response = new byte[10];

            try
            {
                this.sensor.Read(response); // this funtion will read displayName from Arduino 
                if (this.ConnectionState != ConnectionStates.Connected)
                {
                    this.ConnectionState = ConnectionStates.Connected;
                    this.OnStateChanged();
                }

                var datArray = Encoding.ASCII.GetString(response, 0, 10).ToCharArray(); // Converte  Byte to Char
                var data = new string(datArray);
                data = data.Trim().TrimEnd('?');
                if (!string.IsNullOrEmpty(data))
                {
                    this.OnDataReceived(data);
                }
            }
            catch (Exception ex)
            {
                this.ConnectionState = ConnectionStates.Error;
                this.OnStateChanged();
                Debug.Write(ex);
            }
        }

        private void OnDataReceived(object message)
        {
            this.I2CDataReceived?.Invoke(this, new I2CDataReceivedEventArgs()
            {
                Data = message
            });
        }

        private void OnStateChanged()
        {
            this.StateChangedEventHandler?.Invoke(this, new StateChangedEventArgs()
            {
                State = this.ConnectionState
            });
        }

        public void Dispose()
        {
            this.sensor?.Dispose();
        }

        public class I2CDataReceivedEventArgs : EventArgs
        {
            public object Data;
        }

        public class StateChangedEventArgs : EventArgs
        {
            public ConnectionStates State;
        }
    }
}