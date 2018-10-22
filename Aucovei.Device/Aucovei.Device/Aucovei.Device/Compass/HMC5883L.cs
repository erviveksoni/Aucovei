﻿/*
 * MIT License
 * Copyright(c) 2018 - Zhang Yuexin

 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Threading.Tasks;
using Windows.Devices.I2c;

namespace Aucovei.Device.Compass
{
    public class HMC5883LData
    {
        public short X_Axis { get; set; }
        public short Y_Axis { get; set; }
        public short Z_Axis { get; set; }
    }

    /// <summary>
    /// The mode of measuring
    /// </summary>
    public enum MeasurementMode
    {
        Continuous = 0x00,
        Single = 0x01
    }

    public class HMC5883L : IDisposable
    {
        #region Address
        private const byte HMC_I2C_ADDR = 0x1E;
        private const byte HMC_CONFIG_REG_A_ADDR = 0x01;
        private const byte HMC_MODE_REG_ADDR = 0x02;
        private const byte HMC_X_MSB_REG_ADDR = 0x03;
        private const byte HMC_Z_MSB_REG_ADDR = 0x05;
        private const byte HMC_Y_MSB_REG_ADDR = 0x07;
        #endregion

        private I2cDevice sensor;

        private readonly byte measurement;

        public HMC5883L(MeasurementMode measurement)
        {
            this.measurement = (byte)measurement;
        }

        public async Task InitializeAsync()
        {
            var settings = new I2cConnectionSettings(HMC_I2C_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;

            var controller = await I2cController.GetDefaultAsync();
            this.sensor = controller.GetDevice(settings);

            // In datasheet page 11
            this.sensor.Write(new byte[] { HMC_CONFIG_REG_A_ADDR, 0x70 });
            // In datasheet page 13
            this.sensor.Write(new byte[] { HMC_MODE_REG_ADDR, this.measurement });
        }

        /// <summary>
        /// Read raw data from HMC5883L
        /// </summary>
        /// <returns>Raw data</returns>
        public HMC5883LData ReadRaw()
        {
            byte[] xRead = new byte[2];
            byte[] yRead = new byte[2];
            byte[] zRead = new byte[2];

            this.sensor.WriteRead(new byte[] { HMC_X_MSB_REG_ADDR }, xRead);
            this.sensor.WriteRead(new byte[] { HMC_Z_MSB_REG_ADDR }, zRead);
            this.sensor.WriteRead(new byte[] { HMC_Y_MSB_REG_ADDR }, yRead);

            Array.Reverse(xRead);
            Array.Reverse(yRead);
            Array.Reverse(zRead);

            short x = BitConverter.ToInt16(xRead, 0);
            short y = BitConverter.ToInt16(yRead, 0);
            short z = BitConverter.ToInt16(zRead, 0);

            return new HMC5883LData
            {
                X_Axis = x,
                Y_Axis = y,
                Z_Axis = z
            };
        }

        /// <summary>
        /// Calculate direction angle
        /// </summary>
        /// <param name="rawData">HMC5883LData</param>
        /// <returns>Angle</returns>
        public double GetHeadingInDegrees(HMC5883LData rawData)
        {
            //double angle = Math.Atan2(rawData.Y_Axis, rawData.X_Axis) * (180 / Math.PI) + 180;
            //return angle;

            // Calculate heading
            double heading = Math.Atan2(rawData.Y_Axis, rawData.X_Axis);

            // Set declination angle on your location and fix heading
            // You can find your declination on: http://magnetic-declination.com/
            // (+) Positive or (-) for negative
            // For Bytom / Poland declination angle is 4'26E (positive)
            // Formula: (deg + (min / 60.0)) / (180 / M_PI);

            //#define DEC_ANGLE 0.01190
            double declinationAngle = (0.0 + (41.0 / 60.0)) / (180 / Math.PI);
            heading -= declinationAngle;

            // Correct for heading < 0deg and heading > 360deg
            if (heading < 0)
            {
                heading += 2 * Math.PI;
            }

            if (heading > 2 * Math.PI)
            {
                heading -= 2 * Math.PI;
            }

            // Convert to degrees
            double headingDegrees = WayPointNavigator.WayPointHelper.ToDegrees(heading);

            return headingDegrees;
        }

        public void Dispose()
        {
            this.sensor?.Dispose();
        }
    }
}