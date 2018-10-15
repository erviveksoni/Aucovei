using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Q.IoT.Devices.Core;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Devices.Spi;
using Windows.UI;
namespace Aucovei.Device
{
    public partial class SSD1603: IDisposable
    {
        //configuration
        public SSD1603Configuration Configuration { get; private set; }

        //display part
        public Screen Screen => this.Configuration.Screen;
        private byte[] _buffer;

        //I/O
        public BusTypes BusType { get; private set; }
        private SSD1603Controller _controller;

        //draw
        private CanvasDevice _canvasDevice = new CanvasDevice();
        public static readonly Color ForeColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color BackgroundColor = Color.FromArgb(0x00, 0x00, 0x00, 0x00);

        //public properties
        public CanvasRenderTarget Render { get; private set; }
        public States State { get; private set; } = States.Unknown;

        private UInt32 SCREEN_HEIGHT_PAGES;    /* The vertical pixels on this display are arranged into 'pages' of 8 pixels each */

        private byte[,] DisplayBuffer;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="config"></param>
        private SSD1603(SSD1603Configuration config, BusTypes bus)
        {
            this.Configuration = config;
            this.BusType = bus;

            //for drawing
            this._canvasDevice = CanvasDevice.GetSharedDevice();
            this.Render = new CanvasRenderTarget(this._canvasDevice, this.Screen.WidthInDIP, this.Screen.HeightInDIP, this.Screen.DPI,
                            Windows.Graphics.DirectX.DirectXPixelFormat.A8UIntNormalized, CanvasAlphaMode.Straight);

            this.SCREEN_HEIGHT_PAGES = (UInt32)this.Screen.HeightInPixel / 8;
        }

        //I2c constructor
        public SSD1603(SSD1603Configuration config, I2cDevice device, GpioPin pinReset = null) : this(config, BusTypes.I2C)
        {
            this._controller = new SSD1603Controller(device, pinReset);
            this.Init().GetAwaiter();
        }
        public SSD1603(Screen screen, I2cDevice device, GpioPin pinReset = null) : this(new SSD1603Configuration(screen), device, pinReset)
        {
        }

        //SPI constructor
        public SSD1603(SSD1603Configuration config, SpiDevice device, GpioPin pinCmdData, GpioPin pinReset = null) : this(config, BusTypes.SPI)
        {
            this._controller = new SSD1603Controller(device, pinCmdData, pinReset);
            this.Init().Wait();
        }
        public SSD1603(Screen screen, SpiDevice device, GpioPin pinCmdData, GpioPin pinReset = null) : this(new SSD1603Configuration(screen), device, pinCmdData, pinReset)
        {
        }

        public static SSD1603Configuration CreateConfiguration(Screen screen)
        {
            return new SSD1603Configuration(screen);
        }

        #region initialize
        /// <summary>
        /// initialize
        /// </summary>
        /// <returns></returns>
        public async Task Init()
        {
            this.State = States.Initializing;

            if (!this._controller.Initialized)
            {
                this.State = States.Abroted;
                Debug.WriteLine(string.Format("failed to initialize SSD1603 display on {0}", this.BusType));
                return;
            }
            if (!await this.InitDisplay())
            {
                this.State = States.Abroted;
                Debug.WriteLine(string.Format("failed to initialize SSD1603 display on {0}", this.BusType));
                return;
            }
            this.State = States.Ready;
            Debug.WriteLine(string.Format("SSD1603 display on {0} initialized", this.BusType));
            return;
        }

        /// <summary>
        /// initialize display
        /// </summary>
        /// <returns></returns>
        private async Task<bool> InitDisplay()
        {
            try
            {
                //See the datasheet for more details on these commands: http://www.adafruit.com/datasheets/SSD1306.pdf 
                if (!this._controller.Initialized)
                {
                    return false;
                }

                await this._controller.Reset();

                //initialize command / configuration
                this._controller.SetCommand(FundamentalCommands.DisplayOff);
                this._controller.AppendCommand(TimingAndDrivingSchemeCommands.SetDivideRatioAndOscillatorFrequency, this.Configuration.DivdeRatioAndOscillatorFreuency);
                this._controller.AppendCommand(HardwareConfigurationCommands.SetMultiplexRatio, this.Configuration.MultiplexRatio);    //MUX(duty) = common(height of screen)
                this._controller.AppendCommand(HardwareConfigurationCommands.SetDisplayOffset, this.Configuration.DisplayOffset);
                this._controller.AppendCommand(this.Configuration.DisplayStartLine);
                this._controller.AppendCommand(this.Configuration.IsSegmentRemapped ?
                                            HardwareConfigurationCommands.SetSegmentRemapReverse :
                                            HardwareConfigurationCommands.SetSegmentRemap); //left-right
                this._controller.AppendCommand(this.Configuration.IsCommonScanDirectionRemapped ?
                                            HardwareConfigurationCommands.SetComOutputScanDirectionReverse :
                                            HardwareConfigurationCommands.SetComOutputScanDirection); //up - down

                // DISTORTS FONT
                //this._controller.AppendCommand(HardwareConfigurationCommands.SetComPinsHardwareConfiguration, (byte)this.Configuration.CommonPinConfiguration); //need configure

                // CONTRAST
                //this._controller.AppendCommand(FundamentalCommands.SetContrast, this.Configuration.Contrast);
                this._controller.AppendCommand(AddressingCommands.SetMemoryAddressingMode, this.Configuration.MemoryAddressingMode);
                this._controller.AppendCommand(AddressingCommands.SetPageAddress, this.Configuration.StartPageAddress, this.Configuration.EndPageAddress);       //set page address
                this._controller.AppendCommand(AddressingCommands.SetColumnAddress, this.Configuration.StartColumnAddress, this.Configuration.EndColumnAddress);     //set column address
                this._controller.AppendCommand(TimingAndDrivingSchemeCommands.SetPreChargePreiod, this.Configuration.PreChargePreiod);
                this._controller.AppendCommand(TimingAndDrivingSchemeCommands.SetComDeselectVoltageLevel, this.Configuration.ComDeselectVoltageLevel);
                this._controller.AppendCommand(ScrollingCommand.DeactivateScroll);
                this._controller.AppendCommand(FundamentalCommands.NormalDisplay);
                this._controller.AppendCommand(FundamentalCommands.SetChargePump, this.Configuration.ChargePumpSetting);
                this._controller.AppendCommand(FundamentalCommands.DisplayOn);

                this._controller.Send();

                //clear
                this.InitBuffer();
                this.DisplayBufferFunc();
                await Task.Delay(1000);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region drawing functions
        /// <summary>
        /// generate logo for initialize buffer
        /// </summary>
        /// <param name="resolution"></param>
        /// <returns></returns>
        private void InitBuffer()
        {
            if (this.Screen == Screen.OLED_128_32)
            {
                this._buffer = Logos.LOGO_128_32;
            }
            else if (this.Screen == Screen.OLED_128_64)
            {
                this._buffer = Logos.LOGO_128_64;
            }
            else if (this.Screen == Screen.OLED_64_32)
            {
                this._buffer = Logos.LOGO_64_32;
            }
            else
            {
                this._buffer = new byte[1024];
            }

            this.DisplayBuffer =
                new byte[this.Screen.WidthInPixel, this.SCREEN_HEIGHT_PAGES];
        }

        private void MapCanvasToBuffer()
        {
            byte[] rawData = this.Render.GetPixelBytes();
            int page = 0;
            int col = 0;
            int pixelIdx = 0;
            for (int bufferIdx = 0; bufferIdx < this._buffer.Length; bufferIdx++)
            {
                byte value = 0x00;
                for (byte rowInPage = 0; rowInPage < NumberOfCommonsPerPage; rowInPage++)
                {
                    if (this.IsPixelOn(rawData[pixelIdx + this.Screen.WidthInPixel * rowInPage]))
                    {
                        //ON
                        value = (byte)(value | (1 << rowInPage));
                    }
                    else
                    {
                        //OFF
                        value = (byte)(value & ~(1 << rowInPage));
                    }
                }


                this._buffer[bufferIdx] = value;

                pixelIdx++;
                if (++col == this.Screen.WidthInPixel)
                {
                    col = 0;
                    page++;
                    pixelIdx = 0;
                    pixelIdx = this.Screen.WidthInPixel * NumberOfCommonsPerPage * page;
                }
            }
        }

        private bool IsPixelOn(byte value)
        {
            int diffBg = value - BackgroundColor.A;
            int diffFore = ForeColor.A - value;
            return diffFore < diffBg;
        }

        private void DisplayBufferFunc()
        {
            this._controller.SendData(this._buffer);
        }

        public void Clear()
        {
            using (CanvasDrawingSession ds = this.Render.CreateDrawingSession())
            {
                ds.Clear(Color.FromArgb(0xFF, 0, 0, 0));
            }

            Array.Clear(this._buffer, 0, this._buffer.Length);
            Array.Clear(this.DisplayBuffer, 0, this.DisplayBuffer.Length);
            this.DisplayBufferFunc();
        }

        public void Display()
        {
            this.DisplayUpdate();
            //MapCanvasToBuffer();
            this.DisplayBufferFunc();
        }


        /* Writes the Display Buffer out to the physical screen for display */
        private void DisplayUpdate()
        {
            int Index = 0;
            /* We convert our 2-dimensional array into a serialized string of bytes that will be sent out to the display */
            for (int PageY = 0; PageY < this.SCREEN_HEIGHT_PAGES; PageY++)
            {
                for (int PixelX = 0; PixelX < this.Screen.WidthInPixel; PixelX++)
                {
                    this._buffer[Index] = this.DisplayBuffer[PixelX, PageY];
                    Index++;
                }
            }

            this._controller.SendData(this._buffer);
        }

        /* 
         * NAME:        WriteLineDisplayBuf
         * DESCRIPTION: Writes a string to the display screen buffer (DisplayUpdate() needs to be called subsequently to output the buffer to the screen)
         * INPUTS:
         *
         * Line:      The string we want to render. In this sample, special characters like tabs and newlines are not supported.
         * Col:       The horizontal column we want to start drawing at. This is equivalent to the 'X' axis pixel position.
         * Row:       The vertical row we want to write to. The screen is divided up into 4 rows of 16 pixels each, so valid values for Row are 0,1,2,3.
         *
         * RETURN VALUE:
         * None. We simply return when we encounter characters that are out-of-bounds or aren't available in the font.
         */
        public void WriteLineDisplayBuf(String Line, UInt32 Col, UInt32 Row)
        {
            UInt32 CharWidth = 0;
            foreach (Char Character in Line)
            {
                CharWidth = this.WriteCharDisplayBuf(Character, Col, Row);
                Col += CharWidth;   /* Increment the column so we can track where to write the next character   */
                if (CharWidth == 0) /* Quit if we encounter a character that couldn't be printed                */
                {
                    return;
                }
            }
        }

        /* 
         * NAME:        WriteCharDisplayBuf
         * DESCRIPTION: Writes one character to the display screen buffer (DisplayUpdate() needs to be called subsequently to output the buffer to the screen)
         * INPUTS:
         *
         * Character: The character we want to draw. In this sample, special characters like tabs and newlines are not supported.
         * Col:       The horizontal column we want to start drawing at. This is equivalent to the 'X' axis pixel position.
         * Row:       The vertical row we want to write to. The screen is divided up into 4 rows of 16 pixels each, so valid values for Row are 0,1,2,3.
         *
         * RETURN VALUE:
         * We return the number of horizontal pixels used. This value is 0 if Row/Col are out-of-bounds, or if the character isn't available in the font.
         */
        public UInt32 WriteCharDisplayBuf(Char Chr, UInt32 Col, UInt32 Row)
        {
            /* Check that we were able to find the font corresponding to our character */
            FontCharacterDescriptor CharDescriptor = DisplayFontTable.GetCharacterDescriptor(Chr);
            if (CharDescriptor == null)
            {
                return 0;
            }

            /* Make sure we're drawing within the boundaries of the screen buffer */
            UInt32 MaxRowValue = (this.SCREEN_HEIGHT_PAGES / DisplayFontTable.FontHeightBytes) - 1;
            UInt32 MaxColValue = (UInt32)this.Screen.WidthInPixel;
            if (Row > MaxRowValue)
            {
                return 0;
            }
            if ((Col + CharDescriptor.CharacterWidthPx + DisplayFontTable.FontCharSpacing) > MaxColValue)
            {
                return 0;
            }

            UInt32 CharDataIndex = 0;
            UInt32 StartPage = Row * 2;                                              //0
            UInt32 EndPage = StartPage + CharDescriptor.CharacterHeightBytes;        //2
            UInt32 StartCol = Col;
            UInt32 EndCol = StartCol + CharDescriptor.CharacterWidthPx;
            UInt32 CurrentPage = 0;
            UInt32 CurrentCol = 0;

            /* Copy the character image into the display buffer */
            for (CurrentPage = StartPage; CurrentPage < EndPage; CurrentPage++)
            {
                for (CurrentCol = StartCol; CurrentCol < EndCol; CurrentCol++)
                {
                    this.DisplayBuffer[CurrentCol, CurrentPage] = CharDescriptor.CharacterData[CharDataIndex];
                    CharDataIndex++;
                }
            }

            /* Pad blank spaces to the right of the character so there exists space between adjacent characters */
            for (CurrentPage = StartPage; CurrentPage < EndPage; CurrentPage++)
            {
                for (; CurrentCol < EndCol + DisplayFontTable.FontCharSpacing; CurrentCol++)
                {
                    this.DisplayBuffer[CurrentCol, CurrentPage] = 0x00;
                }
            }

            /* Return the number of horizontal pixels used by the character */
            return CurrentCol - StartCol;
        }

        public UInt32 WriteImageDisplayBuf(DisplayImage img, UInt32 Col, UInt32 Row)
        {
            /* Make sure we're drawing within the boundaries of the screen buffer */
            UInt32 MaxRowValue = (this.SCREEN_HEIGHT_PAGES / img.ImageHeightBytes) - 1;
            UInt32 MaxColValue = (UInt32)this.Screen.WidthInPixel;
            if (Row > MaxRowValue)
            {
                return 0;
            }

            if ((Col + img.ImageWidthPx + DisplayFontTable.FontCharSpacing) > MaxColValue)
            {
                return 0;
            }

            UInt32 CharDataIndex = 0;
            UInt32 StartPage = Row * 2;                                              //0
            UInt32 EndPage = StartPage + img.ImageHeightBytes;        //2
            UInt32 StartCol = Col;
            UInt32 EndCol = StartCol + img.ImageWidthPx;
            UInt32 CurrentPage = 0;
            UInt32 CurrentCol = 0;

            /* Copy the character image into the display buffer */
            for (CurrentCol = StartCol; CurrentCol < EndCol; CurrentCol++)
            {
                for (CurrentPage = StartPage; CurrentPage < EndPage; CurrentPage++)
                {
                    this.DisplayBuffer[CurrentCol, CurrentPage] = img.ImageData[CharDataIndex];
                    CharDataIndex++;
                }
            }

            /* Return the number of horizontal pixels used by the character */
            return CurrentCol - StartCol;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this._controller?.Dispose();
                    this._canvasDevice?.Dispose();
                    this.Render?.Dispose();
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
}
