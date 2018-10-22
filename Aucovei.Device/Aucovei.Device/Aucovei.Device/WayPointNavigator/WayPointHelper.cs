using System;

namespace Aucovei.Device.WayPointNavigator
{
    public class WayPointHelper
    {
        private const double EarthRadiusInMiles = 3959.0;

        private const double EarthRadiusInKilometers = 6371.0;

        private const double EarthRadiusInMeters = 6371000.0;

        public static double ToRad(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        public static double ToDegrees(double radians)
        {
            return radians * 180 / Math.PI;
        }

        public static double ToBearing(double radians)
        {
            // convert radians to degrees (as bearing: 0...360)
            return (ToDegrees(radians) + 360) % 360;
        }

        public static double DegreeBearing(
            GeoCoordinate current,
            GeoCoordinate target)
        {
            var dLon = WayPointHelper.ToRad(target.Longitude - current.Longitude);
            var dPhi = Math.Log(
                Math.Tan(WayPointHelper.ToRad(target.Latitude) / 2 + Math.PI / 4) / Math.Tan(WayPointHelper.ToRad(current.Latitude) / 2 + Math.PI / 4));
            if (Math.Abs(dLon) > Math.PI)
            {
                dLon = dLon > 0 ? -(2 * Math.PI - dLon) : (2 * Math.PI + dLon);
            }

            return WayPointHelper.ToBearing(Math.Atan2(dLon, dPhi));
        }

        public static double DistanceBetweenGeoCoordinate(GeoCoordinate current, GeoCoordinate target, DistanceType dType)
        {
            double radius = (dType == DistanceType.Miles) ? WayPointHelper.EarthRadiusInMiles :
                (dType == DistanceType.Kilometers) ? WayPointHelper.EarthRadiusInKilometers : WayPointHelper.EarthRadiusInMeters;
            double dLat = WayPointHelper.ToRad(target.Latitude) - WayPointHelper.ToRad(current.Latitude);
            double dLon = WayPointHelper.ToRad(target.Longitude) - WayPointHelper.ToRad(current.Longitude);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(WayPointHelper.ToRad(current.Latitude)) * Math.Cos(WayPointHelper.ToRad(target.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = c * radius;

            return Math.Round(distance, 2);
        }

        public enum DistanceType : int
        {
            Miles = 0,
            Kilometers = 1,
            Meters = 2
        }
    }
}
