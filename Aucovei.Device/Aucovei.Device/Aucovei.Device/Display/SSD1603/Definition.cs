using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Q.IoT.Devices.Core;

namespace Aucovei.Device
{
   
    public partial class SSD1603
    {
        #region class / strut
        public struct SSD1603Configuration
        {
            public Screen Screen { get; set; }
            public byte DivdeRatioAndOscillatorFreuency { get; set; }
            public byte MultiplexRatio { get; set; } //MUX(duty) = common(height of screen)
            public byte NumberOfPages { get; set; }
            public byte DisplayOffset { get; set; }
            public byte DisplayStartLine { get; set; }
            public bool IsSegmentRemapped { get; set; }
            public bool IsCommonScanDirectionRemapped { get; set; }
            public CommonPinConfigurationOptions CommonPinConfiguration;
            public byte Contrast { get; set; }
            public byte MemoryAddressingMode { get; set; }
            public byte StartPageAddress { get; set; }
            public byte EndPageAddress { get; set; }
            public byte StartColumnAddress { get; set; }
            public byte EndColumnAddress { get; set; }
            public byte PreChargePreiod { get; set; }
            public byte ComDeselectVoltageLevel { get; set; }
            public bool ChargePumpEnabled { get; set; }
            public byte ChargePumpSetting
            {
                get
                {
                    return (byte)(ChargePumpEnabled ? 0x14 : 0x10);
                }
            }

            public SSD1603Configuration(Screen screen)
            {
                Screen = screen;

                CommonPinConfiguration = CommonPinConfigurationOptions.Sequential | CommonPinConfigurationOptions.RemapDisabled;
                IsSegmentRemapped = false;
                IsCommonScanDirectionRemapped = false;

                DisplayOffset = 0x00;
                DisplayStartLine = SSD1603.HardwareConfigurationCommands.DisplayStartLineMin;

                MultiplexRatio = (byte)(screen.HeightInPixel - 1);
                NumberOfPages = (byte)(screen.HeightInPixel / 8);

                MemoryAddressingMode = MemoryAddressingModes.HorizontalAddressingMode;
                StartPageAddress = 0x00;
                EndPageAddress = (byte)(NumberOfPages - 1);
                StartColumnAddress = 0x00;
                EndColumnAddress = (byte)(screen.WidthInPixel - 1);

                Contrast = 0x1F;
                PreChargePreiod = 0xF1;
                ComDeselectVoltageLevel = ComDeselectVoltageLevels.Percent83VCC;
                DivdeRatioAndOscillatorFreuency = 0x80;
                ChargePumpEnabled = true;
            }
        }
        #endregion
        #region Constants
        //number of commons in every GDDRAM page
        public const byte NumberOfCommonsPerPage = 8;

        //I2C Slave Address
        public sealed class I2CSlaveAddress
        {
            private I2CSlaveAddress() { }
            //7-bit Addressing
            public const int PrimaryAddress = 0x3C;  //b0111100 - SA0 = LOW 
            public const int SecondaryAddress = 0x3D;    //b0111101 - SA0 = HIGH (D/C pin acts as SA0) 
        }
        //States
        public enum States { Unknown, Initializing, Ready, Abroted }

        public class I2CTransmissionControlFlags
        {
            private I2CTransmissionControlFlags() { }
            public const byte Command = 0x00;
            public const byte Data = 0x40;
        }

        public sealed class FundamentalCommands
        {
            private FundamentalCommands() { }
            //Fundamental Command
            public const byte SetContrast = 0x81;
            public const byte DisplayAllOnRAM = 0xA4;
            public const byte DisplayAllOn = 0xA5;
            public const byte NormalDisplay = 0xA6;
            public const byte InverseDisplay = 0xA7;
            public const byte DisplayOn = 0xAF;
            public const byte DisplayOff = 0xAE;

            //Charge Pump Command
            public const byte SetChargePump = 0x8D;
        }

        public sealed class TimingAndDrivingSchemeCommands
        {
            private TimingAndDrivingSchemeCommands() { }
            //Timing & Driving Scheme Setting Command
            public const byte SetDivideRatioAndOscillatorFrequency = 0xD5;
            public const byte SetPreChargePreiod = 0xD9;
            public const byte SetComDeselectVoltageLevel = 0xDB;
        }

        public sealed class ComDeselectVoltageLevels
        {
            private ComDeselectVoltageLevels() { }
            public const byte Percent65VCC = 0x00;
            public const byte Percent77VCC = 0x20;
            public const byte Percent83VCC = 0x30;
            public const byte Percent90VCC = 0x40;
        }

        public sealed class HardwareConfigurationCommands
        {
            private HardwareConfigurationCommands() { }
            //Hardware Configuration (Panel resolution & layout related) Command
            public const byte SetMultiplexRatio = 0xA8;
            public const byte SetDisplayOffset = 0xD3;
            public const byte DisplayStartLineMin = 0x40;
            public const byte DisplayStartLineMax = 0x7F;
            public const byte SetSegmentRemap = 0xA0;
            public const byte SetSegmentRemapReverse = 0xA1;
            public const byte SetComOutputScanDirection = 0xC0;
            public const byte SetComOutputScanDirectionReverse = 0xC8;
            public const byte SetComPinsHardwareConfiguration = 0xDA;
        }

        [Flags]
        public enum CommonPinConfigurationOptions : byte
        {
            Sequential = 0x02, //
            Alternative = 0x12,
            RemapEnabled = 0x22,
            RemapDisabled = 0x02
        }

        public sealed class ScrollingCommand
        {
            private ScrollingCommand() { }
            public const byte DeactivateScroll = 0x2E;
        }

        public sealed class AddressingCommands
        {
            private AddressingCommands() { }
            //Addressing Setting Com
            public const byte SetMemoryAddressingMode = 0x20;
            public const byte SetColumnAddress = 0x21;
            public const byte SetPageAddress = 0x22;
            public const byte FirstPageAddress = 0xB0;
        }

        public sealed class MemoryAddressingModes
        {
            private MemoryAddressingModes() { }
            public const byte HorizontalAddressingMode = 0x00;
            public const byte VerticalAddressingMode = 0x01;
            public const byte PageAddressingMode = 0x02;
        }
        #endregion

    }
}
