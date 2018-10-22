using System;
using System.Collections.ObjectModel;

namespace Aucovei.Device.Gps
{
    public class SatellitesInfo
    {
        public enum FixQuality { None, GpsFix, DGpsFix, PpsFix, RealTimeKinematic, FloatRTK };
        public enum FixType { None, TwoD, ThreeD }

        public ObservableCollection<SatelliteInfo> SatelliteList
            = new ObservableCollection<SatelliteInfo>();

        public int? TotalSatelliteCount { set; get; }
        public int? UsedSatelliteCount { get; set; }

        public FixQuality CurrentFixQuality { set; get; }
        public FixType CurrentFixType { set; get; }
        public bool? IsFixTypeAutomatic { get; set; }
        public DateTime? SatelliteDateTime { set; get; }

    }
}
