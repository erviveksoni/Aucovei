using aucovei.uwp;
using aucovei.uwp.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Services.Maps;

namespace aucovei.uwp.Helpers
{
    public class WaypointManager
    {
        private int threshold;

        public WaypointManager(int threshold)
        {
            App.AppData.ConnectedAucovei.WayPoints = App.AppData.ConnectedAucovei.WayPoints ?? new ObservableCollection<Waypoint>();
            this.threshold = threshold;
        }

        public Waypoint AddWayPoint(Geopoint position)
        {
            if (App.AppData.ConnectedAucovei.WayPoints.Count >= this.threshold)
            {
                return null;
            }

            var item = App.AppData.ConnectedAucovei.WayPoints.FirstOrDefault(i => i.Location == position);

            if (item != null)
            {
                return null;
            }

            var waypointlist = (from waypoint in App.AppData.ConnectedAucovei.WayPoints
                                where waypoint.GetType() != typeof(PolylinePath)
                                select waypoint).ToList();

            Waypoint wp = new Waypoint()
            {
                Location = new Geopoint(new BasicGeoposition() { Altitude = 0, Latitude = position.Position.Latitude, Longitude = position.Position.Longitude }),
                DisplayName = waypointlist.Count == 0 ? "Start" : $"Waypoint { waypointlist.Count}",
                IsStartLocation = waypointlist.Count == 0 ? true : false,
                ImageSourceUri = waypointlist.Count == 0 ? new Uri("ms-appx:///Assets/caricon.png") : new Uri("ms-appx:///Assets/waypoint.png"),
                NormalizedAnchorPoint = new Point(0.5, 1.0),
                Index = waypointlist.Count
            };

            App.AppData.ConnectedAucovei.WayPoints.Add(wp);

            waypointlist = (from waypoint in App.AppData.ConnectedAucovei.WayPoints
                            where waypoint.GetType() != typeof(PolylinePath)
                            select waypoint).ToList();

            this.UpdateDistanceLocal(waypointlist.Count - 1);

            return wp;
        }

        public Waypoint GetWayPointByPosition(Geopoint position)
        {
            var wp = App.AppData.ConnectedAucovei.WayPoints.FirstOrDefault(i =>
            i.Location.Position.Latitude == position.Position.Latitude &&
            i.Location.Position.Longitude == position.Position.Longitude);

            return wp;
        }

        public void RemoveWayPoint(Geopoint position)
        {
            var item = App.AppData.ConnectedAucovei.WayPoints.FirstOrDefault(i => i.Location == position);

            if (item == null)
            {
                return;
            }

            if (item.IsStartLocation)
            {
                return;
            }

            var polylinePaths = App.AppData.ConnectedAucovei.WayPoints.Where(i =>
                  i.GetType() == typeof(PolylinePath) && ((PolylinePath)i).PolylinePoints.Contains(position.Position));

            App.AppData.ConnectedAucovei.WayPoints.Remove(item);

            if (polylinePaths.Any())
            {
                foreach (var polylinePath in polylinePaths.ToList())
                {
                    App.AppData.ConnectedAucovei.WayPoints.Remove(polylinePath);
                }
            }

            UpdateWaypointData();
        }

        private void UpdateWaypointData()
        {
            int index = 1;
            foreach (var item in App.AppData.ConnectedAucovei.WayPoints)
            {
                if (!item.IsStartLocation)
                {
                    item.Index = index;
                    item.DisplayName = $"Waypoint {index}";
                    index++;
                }
            }
        }

        public void RemoveAllWayPoints()
        {
            App.AppData.ConnectedAucovei.WayPoints.Clear();
        }

        public async void UpdateDistanceBing(int index)
        {
            if (index >= 1)
            {
                Geopoint start = new Geopoint(App.AppData.ConnectedAucovei.WayPoints[index - 1].Location.Position);
                Geopoint end = new Geopoint(App.AppData.ConnectedAucovei.WayPoints[index].Location.Position);
                MapRouteFinderResult results = await MapRouteFinder.GetWalkingRouteAsync(start, end);
                if (results.Status == MapRouteFinderStatus.Success)
                {
                    App.AppData.ConnectedAucovei.WayPoints[index].DistanceToPreviousWayPoint = "Distance to previous waypoint: " + results.Route.LengthInMeters.ToString() + "m";
                }
            }
        }

        public void UpdateDistanceLocal(int index)
        {
            if (index >= 1)
            {
                var waypointlist = (from waypoint in App.AppData.ConnectedAucovei.WayPoints
                                    where waypoint.GetType() != typeof(PolylinePath)
                                    select waypoint).ToList();

                var previousWayPoint = waypointlist[index - 1];
                Geopoint start = new Geopoint(previousWayPoint.Location.Position);
                Geopoint end = new Geopoint(waypointlist[index].Location.Position);
                Position p1 = new Position() { Latitude = start.Position.Latitude, Longitude = start.Position.Longitude };
                Position p2 = new Position() { Latitude = end.Position.Latitude, Longitude = end.Position.Longitude };
                double results = Haversine.Distance(p1, p2, DistanceType.Kilometers) * 1000;
                App.AppData.ConnectedAucovei.WayPoints.FirstOrDefault(i => i.DisplayName == previousWayPoint.DisplayName).DistanceToPreviousWayPoint = "Distance to previous waypoint: " + Math.Round(results, 2).ToString() + "m";
            }
        }
    }
}
