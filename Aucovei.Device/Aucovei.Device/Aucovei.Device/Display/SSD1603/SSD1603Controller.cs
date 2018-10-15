using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Q.IoT.Devices.Core;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Devices.Spi;

namespace Aucovei.Device
{
    internal class TransmissionData
    {
        internal enum TransmissionDataTypes { Command, Data }
        internal TransmissionDataTypes Type { get; set; }
        internal byte[] Data { get; set; }
        internal TransmissionData(TransmissionDataTypes type)
        {
            this.Type = type;
            this.Data = new byte[0];
        }
    }
    #region class / strut
    public class SSD1603Controller : IDisposable
    {
        private I2cDevice _i2cDevic;
        private SpiDevice _spiDevice;
        private GpioPin _pinReset;
        private GpioPin _pinCmdData;

        //properties
        private BusTypes _busType;

        private Queue<TransmissionData> _dataQueue = new Queue<TransmissionData>();
        private TransmissionData _lastData = null;

        //constructor
        public SSD1603Controller(I2cDevice device, GpioPin pinReset = null)
        {
            this._busType = BusTypes.I2C;
            this._i2cDevic = device;
            this._pinReset = pinReset;
            this.Empty();
            Debug.WriteLine(string.Format("SSD1603 controller on {0} created", this._busType));
        }

        public SSD1603Controller(SpiDevice device, GpioPin pinCmdData, GpioPin pinReset = null)
        {
            this._busType = BusTypes.SPI;
            this._spiDevice = device;
            this._pinCmdData = pinCmdData;
            this._pinReset = pinReset;
            this.Empty();
            Debug.WriteLine(string.Format("SSD1603 controller on {0} created", this._busType));
        }

        public async Task Reset()
        {
            if (this._pinReset != null)
            {
                this._pinReset.Write(GpioPinValue.Low);   //Put display into reset 
                await Task.Delay(1);                // Wait at least 3uS (We wait 1mS since that is the minimum delay we can specify for Task.Delay()
                this._pinReset.Write(GpioPinValue.High);  //Bring display out of reset
            }
            await Task.Delay(100);                //Wait at least 100mS before sending commands
        }

        public bool Initialized
        {
            get
            {
                if (this._busType == BusTypes.I2C && this._i2cDevic == null)
                {
                    return false;
                }
                else if (this._busType == BusTypes.SPI && (this._spiDevice == null || this._pinCmdData == null))
                {
                    return false;
                }

                return true;
            }
        }

        public void Empty()
        {
            this._lastData = null;
            this._dataQueue.Clear();
        }

        //
        private void Append(bool isCommand, params byte[] data)
        {
            //add I2C control flag when operation changed
            if (data == null)
            {
                return;
            }
            bool needI2CCtrlFlag = this._busType == BusTypes.I2C;
            TransmissionData.TransmissionDataTypes currType = isCommand ? TransmissionData.TransmissionDataTypes.Command : TransmissionData.TransmissionDataTypes.Data;
            if (this._lastData == null)
            {
                //first data
                this._lastData = new TransmissionData(currType);

                needI2CCtrlFlag = needI2CCtrlFlag && true;
            }
            else if (this._lastData.Type != currType)
            {
                //data type changed
                this._dataQueue.Enqueue(this._lastData);
                this._lastData = new TransmissionData(currType);


                needI2CCtrlFlag = needI2CCtrlFlag && true;
            }
            else
            {
                //same type as previous
                needI2CCtrlFlag = false;
            }

            byte[] oriData = this._lastData.Data;
            int oriSize = oriData.Length;
            int newSize = oriSize + data.Length + (needI2CCtrlFlag ? 1 : 0);

            //extend array
            Array.Resize(ref oriData, newSize);
            if (needI2CCtrlFlag)
            {
                //add I2C control flag
                oriData[oriSize++] = isCommand ? SSD1603.I2CTransmissionControlFlags.Command : SSD1603.I2CTransmissionControlFlags.Data;
            }

            //append data
            foreach (byte s in data)
            {
                oriData[oriSize++] = s;
            }

            this._lastData.Data = oriData;
        }

        public bool Send()
        {
            if (!this.Initialized)
            {
                return false;
            }
            if (this._lastData == null)
            {
                return false;
            }
            try
            {
                //queue the last data
                this._dataQueue.Enqueue(this._lastData);
                this._lastData = null;

                foreach (TransmissionData td in this._dataQueue)
                {
                    if (this._busType == BusTypes.I2C)
                    {
                        this._i2cDevic.Write(td.Data);
                    }
                    else if (this._busType == BusTypes.SPI)
                    {
                        this._pinCmdData.Write(td.Type == TransmissionData.TransmissionDataTypes.Command ? GpioPinValue.Low : GpioPinValue.High);
                        this._spiDevice.Write(td.Data);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed to send data to {0} device", this._busType), ex.Message);
                return false;
            }
            finally
            {
                this.Empty();
            }
        }

        #region extend for convenience
        public void AppendCommand(params byte[] cmds)
        {
            this.Append(true, cmds);
        }
        public void AppendData(params byte[] data)
        {
            this.Append(false, data);
        }

        public void SetCommand(params byte[] cmds)
        {
            this.Empty();
            this.AppendCommand(cmds);
        }
        public void SetData(params byte[] data)
        {
            this.Empty();
            this.AppendData(data);
        }
        public void SendCommand(params byte[] cmds)
        {
            this.SetCommand(cmds);
            this.Send();
        }
        public void SendData(params byte[] data)
        {
            this.SetData(data);
            this.Send();
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this._i2cDevic?.Dispose();
                    this._spiDevice?.Dispose();
                    this._pinReset?.Dispose();
                    this._pinCmdData?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~SSD1603Controller() {
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

        #endregion
    }
    #endregion
}
