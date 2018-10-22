using System;

namespace Aucovei.Device.WayPointNavigator
{
    public class GeoCoordinate
    {
        private readonly double latitude;

        private readonly double longitude;

        public double Latitude => this.latitude;

        public double Longitude => this.longitude;

        public GeoCoordinate(double latitude, double longitude)
        {
            this.latitude = latitude;
            this.longitude = longitude;
        }

        public override string ToString()
        {
            return string.Format("{0},{1}", this.Latitude, this.Longitude);
        }

        public override bool Equals(Object other)
        {
            return other is GeoCoordinate && this.Equals((GeoCoordinate)other);
        }

        public bool Equals(GeoCoordinate other)
        {
            return this.Latitude == other.Latitude && this.Longitude == other.Longitude;
        }

        public override int GetHashCode()
        {
            return this.Latitude.GetHashCode() ^ this.Longitude.GetHashCode();
        }
    }
}
