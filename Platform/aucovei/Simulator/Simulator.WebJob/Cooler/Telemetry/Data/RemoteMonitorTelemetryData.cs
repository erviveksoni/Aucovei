namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.Simulator.WebJob.Cooler.Telemetry.Data
{
    public class RemoteMonitorTelemetryData
    {
        public string DeviceId { get; set; }
        public double Temperature { get; set; }
        public double Speed { get; set; }
        public double? ExternalTemperature { get; set; }
        public bool CameraStatus {
            get { return true; }
            set { }
        }
        public string DeviceIp { get; set; }
    }
}
