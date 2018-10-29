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

using aucovei.uwp.ViewModel;
using aucovei.uwp.Model;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Text;
using Windows.UI.Popups;
using Windows.UI.Core;
using aucovei.uwp.Helpers;

namespace aucovei.uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SendWaypoints : Page
    {
        private List<Waypoint> _groups;
        private MainPage rootPage;
        ServiceHelper svcHelper;

        public SendWaypoints()
        {
            this.InitializeComponent();
            this.description.Text = "Send Waypoints";
            this.subdescription.Text = $"Review and send waypoints to {App.ConnectedAucovei.DisplayName}";
            this.svcHelper = new ServiceHelper();
        }

        public IEnumerable<Waypoint> Groups
        {
            get { return this._groups; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.rootPage = MainPage.Current;
            _groups = new List<Waypoint>();

            var waypoints = (from waypoint in App.ConnectedAucovei.WayPoints
                where waypoint.GetType() != typeof(PolylinePath) select waypoint);

            _groups = waypoints.Select(i => new Waypoint()
            {
                DisplayName = i.DisplayName,
                IsStartLocation = i.IsStartLocation,
                ImageSourceUri = new Uri("ms-appx:///Assets/circle.png"),
                Index = i.Index,
                Location = i.Location,
                DistanceToPreviousWayPoint = i.DistanceToPreviousWayPoint
            }).ToList();

            _groups.FirstOrDefault(i => i.IsStartLocation).ImageSourceUri = new Uri("ms-appx:///Assets/circlegreen.png");
            CreateCommandBarButtons();
            AppBarSeparator seperator = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
            AppBarButton sendwaypoints = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "SendWaypoints") as AppBarButton;
            sendwaypoints.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            AppBarSeparator seperator = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private async void SendWayPoints_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            /*string json = this.GetJsonText();
            if (!string.IsNullOrEmpty(json))
            {
                json = json.Replace("\r\n", string.Empty).Replace(" ", string.Empty);
                json = string.Concat(json, "#");
                //await App.BluetoothManager.SendStringAsync(string.Empty + "#");
                await Task.Delay(100);
                //await App.BluetoothManager.SendStringAsync(json);
                await Task.Delay(1000);
                this.rootPage.NotifyUser("Waypoints sent!", NotifyType.StatusMessage);
            }*/

            try
            {
                rootPage.ProgressBar(true);
                int wpcount = App.ConnectedAucovei.WayPoints.Skip(1).Count();
                await this.svcHelper.SendCommand(App.ConnectedAucovei.Id, "DemoRun",
                    new KeyValuePair<string, string>("data", wpcount.ToString()));
                this.rootPage.NotifyUser($"Waypoints sent to {App.ConnectedAucovei.DisplayName}!", NotifyType.StatusMessage);
            }
            catch (Exception uex)
            {
                MessageDialog dlg = new MessageDialog(uex.Message);
                rootPage.ShowError(dlg);
            }
            finally
            {
                rootPage.ProgressBar(false);
            }
        }

        private void CreateCommandBarButtons()
        {
            AppBarButton sendwaypoints = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "SendWaypoints") as AppBarButton;
            if (sendwaypoints == null)
            {
                sendwaypoints = new AppBarButton();
                sendwaypoints.Icon = new SymbolIcon(Symbol.Send);
                sendwaypoints.Name = "SendWaypoints";
                sendwaypoints.Label = "Send Waypoints";
                sendwaypoints.Tapped += SendWayPoints_Tapped;
                App.AppCommandBar.PrimaryCommands.Insert(1, sendwaypoints);
            }
            else
            {
                sendwaypoints.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
        }

        private async void MessageBox(string message)
        {
            var dialog = new MessageDialog(message.ToString());
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }

        private string GetJsonText()
        {
            JArray dataArray = new JArray();

            var waypoints = (from waypoint in App.ConnectedAucovei.WayPoints
                where waypoint.GetType() != typeof(PolylinePath)
                select waypoint);

            foreach (var waypoint in waypoints)
            {
                double[] arr = { Math.Round(waypoint.Location.Position.Latitude, 5),
                    Math.Round(waypoint.Location.Position.Longitude, 5) };
                JToken token = JToken.FromObject(arr);
                dataArray.Add(token);
            }

            return dataArray.ToString();
        }
    }
}