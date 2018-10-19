using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace Aucovei.Device.Gps
{
    public class GpsInformation : IDisposable
    {
        #region ConnectingToModule

        public string PortId { get; set; }

        public string PortName { get; set; }

        public string LastErrorMessage { get; set; }

        public enum GpsStatus { None, Active, Void };

        public GpsStatus CurrentGpsStatus { set; get; }

        public PositionInfoClass PositionInfo { get; set; }

        private SerialDevice serialPort = null;

        private DataWriter dataWriteObject = null;

        private DataReader dataReaderObject = null;

        private string messagebuffer = string.Empty;

        private CancellationTokenSource readCancellationTokenSource;

        private bool disposedValue = false; // To detect redundant calls

        private DispatcherTimer signalActivityTimer;

        public GpsInformation(int baudRate)
        {
            this.PositionInfo = new PositionInfoClass();
            this.SatellitesInfo = new SatellitesInfoClass();

            this.ConnectToUARTAsync(baudRate);
        }

        private async void ConnectToUARTAsync(int baudRate)
        {
            string aqs = SerialDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(aqs);
            this.serialPort = await SerialDevice.FromIdAsync(dis[0].Id);
            this.PortId = dis[0].Id;
            this.PortName = this.serialPort.PortName;
            this.serialPort.WriteTimeout = TimeSpan.FromMilliseconds(2000); // default=2000
            this.serialPort.ReadTimeout = TimeSpan.FromMilliseconds(2000);
            this.serialPort.BaudRate = Convert.ToUInt32(baudRate); // 57600
            this.serialPort.Parity = SerialParity.None;
            this.serialPort.StopBits = SerialStopBitCount.One;
            this.serialPort.DataBits = 8;
            this.serialPort.Handshake = SerialHandshake.None;

            this.readCancellationTokenSource = new CancellationTokenSource();
            this.ListenAsync();
        }

        private DateTime lastMessageReceived;

        private async void ListenAsync()
        {
            try
            {
                if (this.serialPort != null)
                {
                    this.dataReaderObject = new DataReader(this.serialPort.InputStream);
                    this.signalActivityTimer = new DispatcherTimer();
                    this.signalActivityTimer.Interval = TimeSpan.FromMilliseconds(500);
                    this.signalActivityTimer.Tick += this.signalActivityTimer_Tick;

                    while (true)
                    {
                        this.lastMessageReceived = DateTime.UtcNow;

                        if (!this.signalActivityTimer.IsEnabled)
                        {
                            this.signalActivityTimer.Start();
                        }

                        await this.ReadAsync(this.readCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                this.LastErrorMessage = "Listening error:" + ex.Message;
                this.ListenAsync();
            }
        }

        private void signalActivityTimer_Tick(object sender, object e)
        {
            // Reset the objects as no data was received in last 1 second
            if (DateTime.UtcNow.Subtract(this.lastMessageReceived) > TimeSpan.FromSeconds(1))
            {
                this.lastMessageReceived = DateTime.UtcNow;
                this.ResetGpsStats();
            }
        }

        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            uint readBufferLength = 64; //default:1024

            cancellationToken.ThrowIfCancellationRequested();

            this.dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
            this.dataReaderObject.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            var loadAsyncTask = this.dataReaderObject.LoadAsync(readBufferLength).AsTask(cancellationToken);

            var bytesRead = await loadAsyncTask;

            try
            {

                if (bytesRead > 0)
                {
                    this.messagebuffer += (this.dataReaderObject.ReadString(bytesRead));
                    this.ReadGpsMessage();
                }
            }
            catch (Exception ex)
            {
                this.ResetGpsStats();
                this.FlushDataBuffer();
                this.LastErrorMessage = "Reading error:" + ex.Message;
                await this.ReadAsync(cancellationToken);
            }
        }

        private void FlushDataBuffer()
        {
            try
            {
                this.dataReaderObject.ReadBuffer(this.dataReaderObject.UnconsumedBufferLength);

                Debug.WriteLine("Buffer flushed");
            }
            catch
            {
                Debug.WriteLine("Buffer flushed");
            }
        }

        private void ResetGpsStats()
        {
            this.CurrentGpsStatus = GpsStatus.None;
            this.PositionInfo = new PositionInfoClass();
            this.SatellitesInfo = new SatellitesInfoClass();
        }

        private void ReadGpsMessage()
        {
            string msg = string.Empty;

            while (this.messagebuffer.Contains(Environment.NewLine))
            {

                msg = this.messagebuffer.Substring(0, this.messagebuffer.IndexOf(Environment.NewLine, StringComparison.Ordinal));

                // +1 to remove the \n\r
                this.messagebuffer = this.messagebuffer.Substring(this.messagebuffer.IndexOf(Environment.NewLine, StringComparison.Ordinal) + 2);

                this.UpdateFromNMEA(msg);
            }

            if (msg.Equals(string.Empty))
            {
                this.LastErrorMessage = "Message string not parsed";
            }
        }

        private async void WriteAsync(string msg)
        {
            try
            {
                if (this.serialPort != null)
                {
                    this.dataWriteObject = new DataWriter(this.serialPort.OutputStream);

                    await this.WriteAsyncInternal(msg);
                }
            }
            catch (Exception ex)
            {
                this.LastErrorMessage = ex.Message;
            }
        }

        private async Task WriteAsyncInternal(string msg)
        {
            try
            {
                Task<UInt32> storeAsyncTask;

                this.dataWriteObject.WriteString(msg);

                storeAsyncTask = this.dataWriteObject.StoreAsync().AsTask();

                uint byteswritten = await storeAsyncTask;

                if (byteswritten > 0)
                {
                    // printMessage("Command " + msg + "sent sucessfully!");
                }
            }
            catch (Exception ex)
            {
                this.LastErrorMessage = ex.Message;
            }
        }

        private void CancelReadTask()
        {
            if (this.readCancellationTokenSource != null)
            {
                if (!this.readCancellationTokenSource.IsCancellationRequested)
                {
                    this.readCancellationTokenSource.Cancel();
                }
            }
        }

        private void CloseDevice()
        {
            if (this.serialPort != null)
            {
                this.serialPort.Dispose();
            }

            this.serialPort = null;
        }

        #endregion

        public SatellitesInfoClass SatellitesInfo { get; set; }

        public async void SendMessageToModule(string msg)
        {
            this.WriteAsync(msg);
        }

        private void UpdateFromNMEA(string msg)
        {
            try
            {
                // remove checksum
                if (msg.Contains("*"))
                {
                    msg = msg.Substring(0, msg.IndexOf("*", StringComparison.Ordinal));
                }

                string[] data = msg.Split(',');


                switch (data[0].Substring(3, 3))
                {
                    // Recommended minimum
                    case "RMC":
                        DateTime d = new DateTime();

                        // add time
                        d = d.AddHours((data[1] != "") ? Convert.ToInt32(data[1].Substring(0, 2)) : 0);
                        d = d.AddMinutes((data[1] != "") ? Convert.ToInt32(data[1].Substring(2, 2)) : 0);
                        d = d.AddSeconds((data[1] != "") ? Convert.ToInt32(data[1].Substring(4, 2)) : 0);

                        // add date
                        d = d.AddDays((data[9] != "") ? Convert.ToInt32(data[9].Substring(0, 2)) - 1 : 0);
                        d = d.AddMonths((data[9] != "") ? Convert.ToInt32(data[9].Substring(2, 2)) - 1 : 0);
                        d = d.AddYears((data[9] != "") ? Convert.ToInt32("20" + data[9].Substring(4, 2)) - 1 : 0);


                        this.SatellitesInfo.SatelliteDateTime = d.ToLocalTime();

                        // active/not active
                        switch (data[2])
                        {
                            case "A":
                                this.CurrentGpsStatus = GpsStatus.Active;
                                break;
                            case "V":
                                this.CurrentGpsStatus = GpsStatus.Void;
                                break;
                            default:
                                this.CurrentGpsStatus = GpsStatus.None;
                                break;
                        }

                        this.PositionInfo.Latitude = (data[3] != "") ?
                            Convert.ToDouble(data[3].Substring(0, data[3].IndexOf(".", StringComparison.Ordinal) - 2)) +
                            (Convert.ToDouble(data[3].Substring(data[3].IndexOf(".", StringComparison.Ordinal) - 2)) / 60)
                            : (double?)null;

                        if (data[4] == "S")
                        {
                            this.PositionInfo.Latitude -= 2 * this.PositionInfo.Latitude; // make it negative
                        }

                        this.PositionInfo.Longitude = (data[5] != "") ?
                            Convert.ToDouble(data[5].Substring(0, data[5].IndexOf(".", StringComparison.Ordinal) - 2)) +
                            (Convert.ToDouble(data[5].Substring(data[5].IndexOf(".", StringComparison.Ordinal) - 2)) / 60)
                            : (double?)null;
                        if (data[6] == "W")
                        {
                            this.PositionInfo.Longitude -= 2 * this.PositionInfo.Longitude;
                        }

                        // speed
                        this.PositionInfo.Speed = (data[7] != "") ? Convert.ToDouble(data[7]) : (double?)null;

                        // facing position to the true north
                        this.PositionInfo.FacingDirection = (data[8] != "") ? Convert.ToDouble(data[8]) : (double?)null;

                        this.PositionInfo.MagneticVariation = (data[10] != "") ? Convert.ToDouble(data[10]) : (double?)null;

                        break;

                    case "GGA":

                        if (data[6] != "")
                        {
                            switch (Convert.ToInt32(data[6]))
                            {
                                case 0:
                                    this.SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.None;
                                    break;
                                case 1:
                                    this.SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.GpsFix;
                                    break;
                                case 2:
                                    this.SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.DGpsFix;
                                    break;
                                case 3:
                                    this.SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.PpsFix;
                                    break;
                                case 4:
                                    this.SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.RealTimeKinematic;
                                    break;
                                case 5:
                                    this.SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.FloatRTK;
                                    break;

                            }
                        }

                        this.SatellitesInfo.UsedSatelliteCount =
                            (data[7] != "") ? Convert.ToInt32(data[7]) : (int?)null;

                        this.PositionInfo.AltitudeAccuracy =
                            (data[8] != "") ? Convert.ToDouble(data[8]) : (double?)null;

                        this.PositionInfo.Altitude =
                            (data[9] != "") ? Convert.ToDouble(data[9]) : (double?)null;

                        break;

                    // detailed satellite data
                    case "GSV":
                        this.SatellitesInfo.TotalSatelliteCount = (data[3] != "") ? Convert.ToInt32(data[3]) : (int?)null;

                        var s = new List<SatelliteInfoClass>();

                        // 4,8,12,16 is id 5,9,13,17 is elevation and so on
                        for (int i = 4; i <= 16; i += 4)
                        {
                            s.Add(new SatelliteInfoClass()
                            {
                                Id = (data[i] != "") ? Convert.ToInt32(data[i]) : (int?)null,
                                Elevation = (data[i + 1] != "") ? Convert.ToInt32(data[i + 1]) : (int?)null,
                                Azimuth = (data[i + 2] != "") ? Convert.ToInt32(data[i + 2]) : (int?)null,
                                Snr = (data[i + 3] != "") ? Convert.ToInt32(data[i + 3]) : (int?)null,
                                // InUse = (data[i + 1] != "") ? true : false
                            });
                        }

                        // update into list, if dont have, add them
                        if (this.SatellitesInfo.SatelliteList.Count > 0)
                        {
                            foreach (SatelliteInfoClass new_s in s)
                            {
                                bool updated = false;

                                foreach (SatelliteInfoClass sl in this.SatellitesInfo.SatelliteList)
                                {
                                    if (sl.Id == new_s.Id)
                                    {
                                        // sl.Id = new_s.Id;
                                        sl.Elevation = new_s.Elevation;
                                        sl.Azimuth = new_s.Azimuth;
                                        sl.Snr = new_s.Snr;

                                        updated = true;
                                    }

                                }

                                if (!updated) // add new if not updated, after looped finish each sattelites
                                {
                                    this.SatellitesInfo.SatelliteList.Add(new SatelliteInfoClass
                                    {
                                        Id = new_s.Id,
                                        Elevation = new_s.Elevation,
                                        Azimuth = new_s.Azimuth,
                                        Snr = new_s.Snr
                                    });
                                }
                            }
                        }
                        else
                        {
                            // add all of them if the count is 0
                            for (int i = 0; i < s.Count; i++)
                            {
                                this.SatellitesInfo.SatelliteList.Add(s[i]);
                            }
                        }
                        break;

                    case "GSA":
                        switch (data[1])
                        {
                            case "A":
                                this.SatellitesInfo.IsFixTypeAutomatic = true;
                                break;
                            case "M":
                                this.SatellitesInfo.IsFixTypeAutomatic = false;
                                break;
                            default:
                                this.SatellitesInfo.IsFixTypeAutomatic = null;
                                break;
                        }

                        if (data[2] != "")
                        {
                            switch (Convert.ToInt32(data[2]))
                            {
                                case 1:
                                    this.SatellitesInfo.CurrentFixType = SatellitesInfoClass.FixType.None;
                                    break;
                                case 2:
                                    this.SatellitesInfo.CurrentFixType = SatellitesInfoClass.FixType.TwoD;
                                    break;
                                case 3:
                                    this.SatellitesInfo.CurrentFixType = SatellitesInfoClass.FixType.ThreeD;
                                    break;
                            }
                        }

                        // 12 spaces for which satellite used for fix
                        foreach (SatelliteInfoClass satellites in this.SatellitesInfo.SatelliteList)
                        {
                            for (int i = 3; i < 15; i++)
                            {
                                if (data[i] != "")
                                {
                                    int satelliteId = Convert.ToInt32(data[i]);

                                    if (satellites.Id == satelliteId)
                                    {
                                        satellites.InUse = true;
                                    }

                                    // if the satellites is in used list then will find out and break
                                    break;
                                }
                                if (i == 15) // if till 15 still not in used list then it is not used
                                {
                                    satellites.InUse = false;
                                }
                            }
                        }

                        this.PositionInfo.Accuracy =
                            (data[15] != "") ? Convert.ToDouble(data[15]) : (double?)null;

                        this.PositionInfo.LatitudeAccuracy =
                            (data[16] != "") ? Convert.ToDouble(data[16]) : (double?)null;

                        this.PositionInfo.LongitudeAccuracy =
                            (data[17] != "") ? Convert.ToDouble(data[17]) : (double?)null;

                        break;
                }
            }
            catch (Exception ex)
            {
                this.LastErrorMessage = "NMEA parsing error" + ex.Message + ex.Data + ex.StackTrace + ex.Source;
            }
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.dataReaderObject?.Dispose();
                    this.dataWriteObject?.Dispose();
                    this.serialPort.Dispose();
                    this.readCancellationTokenSource?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GpsInformation() {
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
