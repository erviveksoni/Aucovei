namespace Aucovei.Device.Gps
{
    public class SatelliteInfo
    {
        public int? Id { set; get; }
        public int? Elevation { set; get; }
        public int? Azimuth { set; get; }
        public int? Snr { set; get; }
        public bool? InUse { set; get; }
    }
}