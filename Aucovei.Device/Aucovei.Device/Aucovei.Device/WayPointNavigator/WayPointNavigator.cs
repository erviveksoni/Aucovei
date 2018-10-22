using System;
using Aucovei.Device.Compass;

namespace Aucovei.Device.WayPointNavigator
{
    public class WayPointNavigator
    {
        private Gps.GpsInformation gpsInformation;

        private HMC5883L compass;

        public WayPointNavigator(HMC5883L compass, Gps.GpsInformation gpsInformation)
        {
            this.gpsInformation = gpsInformation ?? throw new ArgumentNullException(nameof(gpsInformation));
            this.compass = compass ?? throw new ArgumentNullException(nameof(compass));
        }



    }
}
