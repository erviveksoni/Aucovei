//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using aucovei.uwp.Helpers;
using aucovei.uwp.Model;
using Newtonsoft.Json.Linq;
using Windows.Devices.Geolocation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace aucovei.uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AddWaypoints : Page
    {
        RandomAccessStreamReference mapIconStreamReference;
        private MainPage rootPage;
        private bool statusflag = false;
        private bool addNewElementLock = false;
        private WaypointManager waypointManager = null;
        private SolidColorBrush greyBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);
        private SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);
        private CancellationTokenSource tokenSrc;
        private ServiceHelper svcHelper;

        public AddWaypoints()
        {
            this.InitializeComponent();
            this.description.Text = "Add Waypoints";
            this.mapIconStreamReference = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/waypoint.png"));
            this.waypointManager = new WaypointManager(10);
            this.svcHelper = new ServiceHelper();
        }

        private void MyMap_ZoomLevelChanged(MapControl sender, object args)
        {
            App.AppData.CurrentZoomLevel = this.myMap.ZoomLevel;
        }

        private void MyMap_Loaded(object sender, RoutedEventArgs e)
        {
            this.rootPage.ProgressBar(true);
            this.myMap.Style = MapStyle.Aerial;
            this.myMap.DesiredPitch = 45;
            this.myMap.Center = App.AppData.ConnectedAucovei.StartPosition;
            this.myMap.ZoomLevel = App.AppData.CurrentZoomLevel;
            if (App.AppData.CurrentZoomLevel < this.myMap.MaxZoomLevel)
            {
                App.AppData.CurrentZoomLevel = this.myMap.MaxZoomLevel;
                this.myMap.StartContinuousZoom(20);
            }

            this.waypointManager.AddWayPoint(App.AppData.ConnectedAucovei.StartPosition);
            this.UpdateMapPolyLine();

            this.MapItems.ItemsSource = App.AppData.ConnectedAucovei.WayPoints;

            this.rootPage.ProgressBar(false);
        }

        private void myMap_MapTapped(MapControl sender, MapInputEventArgs args)
        {
            if (this.addNewElementLock)
            {
                this.addNewElementLock = false;

                return;
            }

            if (App.AppData.CurrentZoomLevel < this.myMap.MaxZoomLevel)
            {
                this.rootPage.NotifyUser("Please use the max zoom level before adding waypoints.", NotifyType.ErrorMessage);

                return;
            }

            Geopoint location = new Geopoint(args.Location.Position);
            Waypoint wp = this.waypointManager.AddWayPoint(location);
            if (wp != null)
            {
                this.UpdateMapPolyLine();
                this.rootPage.NotifyUser($"Added {wp.DisplayName}", NotifyType.StatusMessage);
            }

            // Reverse geocode the specified geographic location.  
            /*MapLocationFinderResult result = await MapLocationFinder.FindLocationsAtAsync(pointToReverseGeocode);
            var resultText = new StringBuilder();
            if (result.Status == MapLocationFinderStatus.Success)
            {
                resultText.AppendLine(result.Locations[0].Address.District + ", " + result.Locations[0].Address.Town + ", " + result.Locations[0].Address.Country);
            }

            MessageBox(resultText.ToString());*/
        }

        private void UpdateMapPolyLine()
        {
            MapElement element = this.myMap.MapElements.FirstOrDefault(i => i.GetType().Name == "MapPolyline");
            if (element != null)
            {
                this.myMap.MapElements.Remove(element);
            }

            if (App.AppData.ConnectedAucovei.WayPoints.Count < 2)
            {
                return;
            }

            var locations = (from p in App.AppData.ConnectedAucovei.WayPoints
                             where p.GetType() != typeof(PolylinePath)
                             select p.Location.Position);


            //MapPolyline mapPolyline = new MapPolyline();
            //Geopath path = new Geopath(locations);
            //mapPolyline.Path = path;
            //mapPolyline.StrokeColor = Colors.Red;
            //mapPolyline.StrokeThickness = 3;
            //mapPolyline.StrokeDashed = true;
            //myMap.MapElements.Add(mapPolyline);


            App.AppData.ConnectedAucovei.WayPoints.Add(new PolylinePath(this.myMap)
            {
                PolylineColor = new SolidColorBrush(Colors.Red),
                PolylineThinkness = 3,
                PolylineTag = "Route",
                PolylinePoints = locations.ToList()
            });
        }

        private void mapItemButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (this.statusflag)
            {
                this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
                this.statusflag = false;

                return;
            }

            this.statusflag = true;
            var buttonSender = sender as Button;
            Waypoint wp = buttonSender.DataContext as Waypoint;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Waypoint Info");
            sb.AppendLine($"Name: {wp.DisplayName}");
            sb.AppendLine($"Latitude: {wp.Location.Position.Latitude}");
            sb.AppendLine($"Longitude: {wp.Location.Position.Longitude}");

            this.rootPage.NotifyUser(sb.ToString(), NotifyType.StatusMessage);
        }

        private void mapItemButton_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            var buttonSender = sender as Button;
            Waypoint wp = buttonSender.DataContext as Waypoint;
            this.waypointManager.RemoveWayPoint(wp.Location);
            this.myMap.UpdateLayout();
            this.UpdateMapPolyLine();
            this.rootPage.NotifyUser($"Removed waypoint", NotifyType.StatusMessage);
        }

        private async void MessageBox(string message)
        {
            var dialog = new MessageDialog(message.ToString());
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.SourcePageType.Name != "DeviceConnection" &&
                e.SourcePageType.Name != "AddWaypoints" &&
                App.AppData.ConnectedAucovei.WayPoints.Count < 2)
            {
                e.Cancel = true;
                this.rootPage.UpdateNavigation(1);
                this.rootPage.NotifyUser("Please add atleast 1 waypoint to proceed to next step.", NotifyType.ErrorMessage);
            }
            else if (this.tokenSrc != null && !this.tokenSrc.IsCancellationRequested)
            {
                this.tokenSrc.Cancel();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.rootPage = MainPage.Current;
            this.CreateCommandBarButtons();
            AppBarSeparator seperator = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator.Visibility = Visibility.Visible;
            this.tokenSrc = new CancellationTokenSource();
            this.UpdateDeviceOnlineStatus();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            AppBarButton centerMap = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "CenterMap") as AppBarButton;
            centerMap.Visibility = Visibility.Collapsed;
            AppBarButton clearMap = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "ClearMap") as AppBarButton;
            clearMap.Visibility = Visibility.Collapsed;
            AppBarSeparator seperator = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator.Visibility = Visibility.Collapsed;
        }

        private void myMap_MapElementClick(MapControl sender, MapElementClickEventArgs args)
        {
            this.addNewElementLock = true;
            if (args.MapElements[0] is MapPolyline)
            {
                MapPolyline polyLine = args.MapElements[0] as MapPolyline;
                Geopath path = polyLine.Path;
                Geopoint end = new Geopoint(path.Positions.LastOrDefault());
                Waypoint wp = this.waypointManager.GetWayPointByPosition(end);
                if (wp != null)
                {
                    this.rootPage.NotifyUser(wp.DistanceToPreviousWayPoint, NotifyType.StatusMessage);
                }
            }
        }

        private void CreateCommandBarButtons()
        {
            AppBarButton centerMap = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "CenterMap") as AppBarButton;
            if (centerMap == null)
            {
                centerMap = new AppBarButton();
                centerMap.Icon = new SymbolIcon(Symbol.Target);
                centerMap.Name = "CenterMap";
                centerMap.Label = "Center Map";
                centerMap.Click += this.LocateMe_Click;
                App.AppCommandBar.PrimaryCommands.Insert(1, centerMap);
            }
            else
            {
                centerMap.Visibility = Visibility.Visible;
            }

            AppBarButton clearMap = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "ClearMap") as AppBarButton;
            if (clearMap == null)
            {
                clearMap = new AppBarButton();
                clearMap.Icon = new SymbolIcon(Symbol.Clear);
                clearMap.Name = "ClearMap";
                clearMap.Label = "Clear Map";
                clearMap.Click += this.ClearMap_Click;
                App.AppCommandBar.PrimaryCommands.Insert(2, clearMap);
            }
            else
            {
                clearMap.Visibility = Visibility.Visible;
            }
        }

        private async void LocateMe_Click(object sender, RoutedEventArgs e)
        {
            this.rootPage.ProgressBar(true);
            Geolocator geolocator = new Geolocator();
            // Request permission to access location
            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                geolocator.DesiredAccuracyInMeters = 1;
                Geoposition geoposition = await geolocator.GetGeopositionAsync(
                                        maximumAge: TimeSpan.FromMinutes(5),
                                        timeout: TimeSpan.FromSeconds(10));

                this.myMap.Center = new Geopoint(new BasicGeoposition()
                {
                    Latitude = geoposition.Coordinate.Latitude,
                    Longitude = geoposition.Coordinate.Longitude
                });
            }
            else
            {
                this.myMap.Center = new Geopoint(new BasicGeoposition()
                {
                    Latitude = 17.4544868990779,
                    Longitude = 78.3009620849043
                });
            }

            this.rootPage.ProgressBar(false);
        }

        private void ClearMap_Click(object sender, RoutedEventArgs e)
        {
            this.waypointManager.RemoveAllWayPoints();
            this.myMap.MapElements.Clear();
            this.waypointManager.AddWayPoint(App.AppData.ConnectedAucovei.StartPosition);
            this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
        }

        private async void UpdateDeviceOnlineStatus()
        {
            this.connectedDevice.Text = $"You are connected to {App.AppData.ConnectedAucovei.DisplayName}.";
            while (!this.tokenSrc.IsCancellationRequested)
            {
                try
                {
                    bool result = await this.svcHelper.IsAucovieOnlineAsync(App.AppData.ConnectedAucovei.Id);
                    if (result)
                    {
                        this.connStatus.Fill = this.greenBrush;
                        ToolTipService.SetToolTip(this.connStatus, "Online");
                    }
                    else
                    {
                        this.connStatus.Fill = this.greyBrush;
                        ToolTipService.SetToolTip(this.connStatus, "Offline");
                    }
                }
                catch (UnauthorizedAccessException uex)
                {
                    this.tokenSrc.Cancel();
                    MessageDialog dlg = new MessageDialog(uex.Message);
                    this.rootPage.ShowError(dlg);
                }
                catch
                {
                    //do nothing
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }
        }
    }
}
