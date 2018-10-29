using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Media;

namespace aucovei.uwp.Model
{
    public class Waypoint
    {
        public Waypoint()
        {

        }

        public Point NormalizedAnchorPoint { get; set; }
        public string DisplayName { get; set; }
        public Geopoint Location { get; set; }
        public Uri ImageSourceUri { get; set; }
        public string DistanceToPreviousWayPoint { get; set; }
        public int Index { get; set; }
        public bool IsStartLocation { get; set; }
        public string LatLonString
        {
            get
            {
                return string.Concat("Lat,Long:",
                    Location.Position.Latitude.ToString(),
                    ", ",
                    Location.Position.Longitude.ToString());
            }
        }
    }

    public class PolylinePath : Waypoint
    {
        public PolylinePath(MapControl MyMap)
        {
            this.MyMap = MyMap;
        }

        MapControl MyMap;
        public SolidColorBrush PolylineColor { get; set; }

        public int PolylineThinkness { get; set; }

        public string PolylineTag { get; set; }

        public IEnumerable<BasicGeoposition> PolylinePoints { get; set; }

        public PointCollection Polyline
        {
            get
            {
                PointCollection returnObject = new PointCollection();
                //could have used LINQ but wanted to check if the collection is being populated correctly
                foreach (var location in PolylinePoints)
                {
                    Point actualpoint;
                    MyMap.GetOffsetFromLocation(new Geopoint(location), out actualpoint);
                    returnObject.Add(actualpoint);
                }
                return returnObject;
            }
        }
    }
}
