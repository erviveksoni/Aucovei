using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace Aucovei.Device
{
    public class DisplayManager: IDisposable
    {
        private SSD1603 display;

        public async Task Init()
        {
            var deviceI2C = await this.InitI2C();
            SSD1603.SSD1603Configuration screenConfig = SSD1603.CreateConfiguration(Screen.OLED_128_64);
            screenConfig.IsSegmentRemapped = true;
            screenConfig.IsCommonScanDirectionRemapped = true;
            this.display = new SSD1603(screenConfig, deviceI2C);
            while (this.display.State == SSD1603.States.Initializing)
            {
                await Task.Delay(500);
            }
        }

        public void AppendText(string text, uint col, uint row)
        {
            if (this.display.State == SSD1603.States.Ready)
            {
                for (uint i = col; i < 128; i++)
                {
                    char value = ' ';
                    this.display.WriteCharDisplayBuf(value, i, row);
                }
                this.display.Display();
                this.display.WriteLineDisplayBuf(text, col, row);
                this.display.Display();
            }
            else
            {
                Debug.Write("Display not ready!");
            }
        }

        public void AppendImage(DisplayImage image, uint col, uint row)
        {
            if (this.display.State == SSD1603.States.Ready)
            {
                for (uint i = col; i < 15; i++)
                {
                    char value = ' ';
                    this.display.WriteCharDisplayBuf(value, i, row);
                }
                this.display.Display();
                this.display.WriteImageDisplayBuf(image, col, row);
                this.display.Display();
            }
            else
            {
                Debug.Write("Display not ready!");
            }
        }

        public void ClearDisplay()
        {
            if (this.display.State == SSD1603.States.Ready)
            {
                this.display.Clear();
                this.display.Display();
            }
            else
            {
                Debug.Write("Display not ready!");
            }
        }

        public void ClearCharacterRange(uint row, uint start, uint stop)
        {
            if (this.display.State == SSD1603.States.Ready)
            {
                for (uint i = start; i <= stop; i++)
                {
                    char value = ' ';
                    this.display.WriteCharDisplayBuf(value, i, row);
                }
                this.display.Display();
            }
            else
            {
                Debug.Write("Display not ready!");
            }
        }

        public void ClearRow(uint row)
        {
            if (this.display.State == SSD1603.States.Ready)
            {
                for (uint i = 0; i < 128; i++)
                {
                    char value = ' ';
                    this.display.WriteCharDisplayBuf(value, i, row);
                }
                this.display.Display();
            }
            else
            {
                Debug.Write("Display not ready!");
            }
        }

        void Update(DisplayImage img)
        {
            //display.ClearDisplayBuf();

            this.DrawBody(img);

            this.display.Display();
        }

        void DrawBody(DisplayImage img)
        {
            // Row 0, and image
            this.display.WriteImageDisplayBuf(img, 0, 1);


            // Row 1 - 3
            //display.WriteLineDisplayBuf("Hello", 0, 1);
            //display.WriteLineDisplayBuf("World", 0, 2);
        }

        /// <summary>
        /// initialize I2C device
        /// </summary>
        /// <returns></returns>
        private async Task<I2cDevice> InitI2C()
        {
            try
            {
                var settings = new I2cConnectionSettings(SSD1603.I2CSlaveAddress.PrimaryAddress);
                settings.BusSpeed = I2cBusSpeed.FastMode;                       /* 400KHz bus speed */
                string aqs = I2cDevice.GetDeviceSelector();                     /* Get a selector string that will return all I2C controllers on the system */
                var dis = await DeviceInformation.FindAllAsync(aqs);            /* Find the I2C bus controller devices with our selector string             */
                var device = await I2cDevice.FromIdAsync(dis[0].Id, settings);    /* Create an I2cDevice with our selected bus controller and I2C settings    */

                //I2cController controller = await I2cController.GetDefaultAsync();
                //var device = controller.GetDevice(settings);

                if (device == null)
                {
                    Debug.WriteLine(string.Format(
                        "Failed to initialize I2C Port address={0} on I2C Controller", settings.SlaveAddress));
                    return null;
                }
                else
                {
                    Debug.WriteLine(string.Format("I2C Port initialized. address={0}, id={1}", device.ConnectionSettings.SlaveAddress, device.DeviceId));
                    return device;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to initialize I2C Port", ex.Message);
                return null;
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
                    this.display?.Dispose();
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
    }
}
