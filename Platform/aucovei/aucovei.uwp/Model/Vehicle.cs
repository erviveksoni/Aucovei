using System;
using System.Collections.ObjectModel;
using Windows.Devices.Geolocation;

namespace aucovei.uwp.Model
{
    public class Vehicle
    {
        public Vehicle()
        {

        }

        public string DisplayName { get; set; }

        public string Id { get; set; }

        public Geopoint StartPosition { get; set; }

        public ObservableCollection<Waypoint> WayPoints { get; set; }

        public bool IsNewGeneration { get; set; }
    }
}
