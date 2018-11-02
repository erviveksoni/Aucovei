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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using aucovei.uwp.Helpers;
using aucovei.uwp.Model;
using Newtonsoft.Json.Linq;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace aucovei.uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DeviceConnection : Page
    {
        private MainPage rootPage;
        ServiceHelper svcHelper;

        private ObservableCollection<Vehicle> devices => this.getLocalDevices();

        private ObservableCollection<Vehicle> getLocalDevices()
        {
            ObservableCollection<Vehicle> devices = new ObservableCollection<Vehicle>();
            if (App.AppData.Vehicles == null || App.AppData.Vehicles.Count < 1)
            {
                return devices;
            }

            var _devices = App.AppData.Vehicles.Select(i => new Vehicle()
            {
                DisplayName = i.DisplayName,
                Id = i.Id,
                StartPosition = i.StartPosition,
                WayPoints = i.WayPoints,
                IsNewGeneration = i.IsNewGeneration
            }).ToList();

            devices = new ObservableCollection<Vehicle>(_devices);

            return devices;
        }

        public DeviceConnection()
        {
            this.InitializeComponent();
            this.description.Text = "Connect to an aucovei";
            this.CreateCommandBarButtons();
            this.svcHelper = new ServiceHelper();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.rootPage = MainPage.Current;
            this.RefreshDeviceList();
        }

        private void CreateCommandBarButtons()
        {
            AppBarButton connectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "ConnectDevice") as AppBarButton;
            if (connectBtn == null)
            {
                connectBtn = new AppBarButton();
                BitmapIcon icon = new BitmapIcon();
                icon.UriSource = new Uri("ms-appx:///Assets/link.png");
                connectBtn.Icon = icon;
                connectBtn.Name = "ConnectDevice";
                connectBtn.Label = "Connect Device";
                connectBtn.IsEnabled = false;
                connectBtn.Click += this.ConnectBtn_Click;
                App.AppCommandBar.PrimaryCommands.Insert(0, connectBtn);
            }

            AppBarButton refreshBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "RefreshDeviceList") as AppBarButton;
            if (refreshBtn == null)
            {
                refreshBtn = new AppBarButton();
                refreshBtn.Icon = new SymbolIcon(Symbol.Refresh);
                refreshBtn.Name = "RefreshDeviceList";
                refreshBtn.Label = "Refresh List";
                refreshBtn.Click += this.RefreshBtn_Click;
                App.AppCommandBar.PrimaryCommands.Insert(1, refreshBtn);
            }

            AppBarButton disconnectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "DisconnectDevice") as AppBarButton;
            if (disconnectBtn == null)
            {
                disconnectBtn = new AppBarButton();
                BitmapIcon icon = new BitmapIcon();
                icon.UriSource = new Uri("ms-appx:///Assets/unlink.png");
                disconnectBtn.Icon = icon;
                disconnectBtn.Name = "DisconnectDevice";
                disconnectBtn.Label = "Disconnect Device";
                disconnectBtn.Click += this.DisconnectBtn_Click;
                disconnectBtn.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(1, disconnectBtn);
            }

            AppBarSeparator seperator1 = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            if (seperator1 == null)
            {
                seperator1 = new AppBarSeparator();
                seperator1.Name = "sperator1";
                seperator1.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(1, seperator1);
            }

            AppBarButton hornBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "Horn") as AppBarButton;
            if (hornBtn == null)
            {
                hornBtn = new AppBarButton();
                BitmapIcon icon = new BitmapIcon();
                icon.UriSource = new Uri("ms-appx:///Assets/Horn.png");
                hornBtn.Icon = icon;
                hornBtn.Name = "Horn";
                hornBtn.Label = "Horn";
                hornBtn.Tapped += this.Toggle_Tapped;
                hornBtn.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(2, hornBtn);
            }

            AppBarToggleButton headlights = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarToggleButton && ((AppBarToggleButton)i).Name == "Headlights") as AppBarToggleButton;
            if (headlights == null)
            {
                headlights = new AppBarToggleButton();
                BitmapIcon icon = new BitmapIcon();
                icon.UriSource = new Uri("ms-appx:///Assets/headlights.png");
                headlights.Icon = icon;
                headlights.Name = "Headlights";
                headlights.Label = "Headlights";
                headlights.Tapped += this.Toggle_Tapped; ;
                headlights.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(3, headlights);
            }

            AppBarToggleButton camera = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarToggleButton && ((AppBarToggleButton)i).Name == "Camera") as AppBarToggleButton;
            if (camera == null)
            {
                camera = new AppBarToggleButton();
                BitmapIcon icon = new BitmapIcon();
                icon.UriSource = new Uri("ms-appx:///Assets/camera.png");
                camera.Icon = icon;
                camera.Name = "Camera";
                camera.Label = "Camera";
                camera.Tapped += this.Toggle_Tapped; ;
                camera.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(4, camera);
            }

            AppBarButton deleteroute = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "DeleteRoute") as AppBarButton;
            if (deleteroute == null)
            {
                deleteroute = new AppBarButton();
                deleteroute.Icon = new SymbolIcon(Symbol.Delete);
                deleteroute.Name = "DeleteRoute";
                deleteroute.Label = "Delete Route";
                deleteroute.Tapped += this.Toggle_Tapped;
                deleteroute.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(5, deleteroute);
            }

            AppBarSeparator seperator2 = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator2") as AppBarSeparator;
            if (seperator2 == null)
            {
                seperator2 = new AppBarSeparator();
                seperator2.Name = "sperator2";
                seperator2.Visibility = Visibility.Collapsed;
                App.AppCommandBar.PrimaryCommands.Insert(6, seperator2);
            }

            AppBarButton TestBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "TestBtn") as AppBarButton;
            if (TestBtn == null)
            {
                TestBtn = new AppBarButton();
                TestBtn.Icon = new SymbolIcon(Symbol.Admin);
                TestBtn.Name = "TestBtn";
                TestBtn.Label = "TestBtn";
                TestBtn.Visibility = Visibility.Collapsed;
                TestBtn.Tapped += async (o, i) =>
                {
                    double[] array = { 17.454489832744, 78.3010964468122 };
                    JToken token = JToken.FromObject(array);
                    JArray arr = new JArray();
                    arr.Add(token);
                    //await SendCommandToCar("{" + arr.ToString() + "}", 100);
                    //await Task.Delay(100);
                    //await SendCommandToCar(arr.ToString(), 100);
                };

                App.AppCommandBar.PrimaryCommands.Insert(6, TestBtn);
            }
        }

        private async void Toggle_Tapped(object sender, TappedRoutedEventArgs e)
        {
            JObject command = new JObject();
            AppBarToggleButton togglebtn = sender as AppBarToggleButton;
            if (togglebtn != null)
            {
                string name = togglebtn.Name;
                KeyValuePair<string, string> kvp;
                switch (name)
                {
                    case "Headlights":
                        if (togglebtn.IsChecked.Value)
                        {
                            kvp = new KeyValuePair<string, string>("data", "true");
                        }
                        else
                        {
                            kvp = new KeyValuePair<string, string>("data", "false");
                        }

                        await this.SendCommandToCar("SetLights", kvp);

                        break;

                    case "Camera":
                        if (togglebtn.IsChecked.Value)
                        {
                            kvp = new KeyValuePair<string, string>("data", "true");
                        }
                        else
                        {
                            kvp = new KeyValuePair<string, string>("data", "false");
                        }

                        await this.SendCommandToCar("SetCamera", kvp);

                        break;
                }
            }
            else
            {
                AppBarButton btn = sender as AppBarButton;
                string name = btn.Name;
                switch (name)
                {
                    case "Horn":
                        await this.SendCommandToCar("SendBuzzer", new KeyValuePair<string, string>());
                        break;

                    case "DeleteRoute":

                        break;
                }
            }
        }

        private async Task SendCommandToCar(string commandText, KeyValuePair<string, string> parameters)
        {
            if (App.AppData.ConnectedAucovei != null && App.AppData.IsConnected)
            {
                try
                {
                    this.rootPage.ProgressBar(true);
                    await this.svcHelper.SendCommandAsync(App.AppData.ConnectedAucovei.Id, commandText, parameters);
                    this.rootPage.NotifyUser($"Command sent to {App.AppData.ConnectedAucovei.DisplayName}!", NotifyType.StatusMessage);
                }
                catch (Exception uex)
                {
                    MessageDialog dlg = new MessageDialog(uex.Message);
                    this.rootPage.ShowError(dlg);
                }
                finally
                {
                    this.rootPage.ProgressBar(false);
                }
            }
        }

        private async void RefreshDeviceList(bool serverUpdate = false)
        {
            if (serverUpdate || App.AppData.Vehicles == null || App.AppData.Vehicles.Count == 0)
            {
                this.rootPage.ProgressBar(true);
                await this.GetDeviceList();
                this.rootPage.ProgressBar(false);
            }

            this.DeviceList.ItemsSource = this.devices;
            if (App.AppData.ConnectedAucovei != null)
            {
                this.DeviceList.SelectedItem = App.AppData.ConnectedAucovei;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (App.AppData.ConnectedAucovei == null || !App.AppData.IsConnected)
            {
                e.Cancel = true;
                this.rootPage.UpdateNavigation(0);
                this.rootPage.NotifyUser("Please establish connection with an aucovei", NotifyType.ErrorMessage);
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            this.RefreshDeviceList(true);
        }

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            this.ResetAppLevelParameters();
            this.rootPage.UpdateNavigation(0);

            AppBarButton refreshBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "RefreshDeviceList") as AppBarButton;
            refreshBtn.Visibility = Visibility.Visible;

            AppBarButton disconnectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "DisconnectDevice") as AppBarButton;
            disconnectBtn.Visibility = Visibility.Collapsed;

            AppBarSeparator seperator = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator.Visibility = Visibility.Collapsed;

            AppBarButton connectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "ConnectDevice") as AppBarButton;
            connectBtn.Visibility = Visibility.Visible;

            AppBarSeparator seperator1 = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator1.Visibility = Visibility.Collapsed;

            AppBarSeparator seperator2 = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator2") as AppBarSeparator;
            seperator2.Visibility = Visibility.Collapsed;

            AppBarToggleButton headlights = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarToggleButton && ((AppBarToggleButton)i).Name == "Headlights") as AppBarToggleButton;
            headlights.Visibility = Visibility.Collapsed;

            AppBarToggleButton camera = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarToggleButton && ((AppBarToggleButton)i).Name == "Camera") as AppBarToggleButton;
            camera.Visibility = Visibility.Collapsed;

            AppBarButton deleteroute = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "DeleteRoute") as AppBarButton;
            deleteroute.Visibility = Visibility.Collapsed;

            AppBarButton hornBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "Horn") as AppBarButton;
            hornBtn.Visibility = Visibility.Collapsed;
        }

        private void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            App.AppData.IsConnected = true;
            this.OnConnectionEstablished();
        }

        private void OnConnectionEstablished()
        {
            AppBarButton connectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "ConnectDevice") as AppBarButton;
            connectBtn.Visibility = Visibility.Collapsed;

            AppBarButton refreshBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "RefreshDeviceList") as AppBarButton;
            refreshBtn.Visibility = Visibility.Collapsed;

            AppBarButton disconnectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "DisconnectDevice") as AppBarButton;
            disconnectBtn.Visibility = Visibility.Visible;

            AppBarSeparator seperator1 = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator1") as AppBarSeparator;
            seperator1.Visibility = Visibility.Collapsed;

            AppBarSeparator seperator2 = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarSeparator && ((AppBarSeparator)i).Name == "sperator2") as AppBarSeparator;
            seperator2.Visibility = Visibility.Visible;

            AppBarToggleButton headlights = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarToggleButton && ((AppBarToggleButton)i).Name == "Headlights") as AppBarToggleButton;
            headlights.Visibility = Visibility.Visible;

            if (App.AppData.ConnectedAucovei.IsNewGeneration)
            {
                AppBarToggleButton camera =
                    App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i =>
                        i is AppBarToggleButton && ((AppBarToggleButton) i).Name == "Camera") as AppBarToggleButton;
                camera.Visibility = Visibility.Visible;
            }

            //AppBarButton deleteroute = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "DeleteRoute") as AppBarButton;
            //deleteroute.Visibility = Visibility.Visible;

            AppBarButton hornBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "Horn") as AppBarButton;
            hornBtn.Visibility = Visibility.Visible;

            this.rootPage.NotifyUser("Connected to " + App.AppData.ConnectedAucovei.DisplayName + "!", NotifyType.StatusMessage);

            this.rootPage.UpdateNavigation(1);
            this.Frame.Navigate(typeof(AddWaypoints));
        }

        private async void MessageBox(string message)
        {
            var dialog = new MessageDialog(message.ToString());
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await dialog.ShowAsync());
        }

        private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AppBarButton connectBtn = App.AppCommandBar.PrimaryCommands.ToList().FirstOrDefault(i => i is AppBarButton && ((AppBarButton)i).Name == "ConnectDevice") as AppBarButton;
            if (this.DeviceList.SelectedItem != null)
            {
                App.AppData.ConnectedAucovei = this.DeviceList.SelectedItem as Vehicle;
                connectBtn.IsEnabled = true;
            }
            else
            {
                connectBtn.IsEnabled = false;
            }
        }

        private void ResetAppLevelParameters()
        {
            //App.StartPosition = null;
            App.AppData.CurrentZoomLevel = 2.0;
            if (App.AppData.ConnectedAucovei.WayPoints != null)
            {
                App.AppData.ConnectedAucovei.WayPoints.Clear();
            }

            this.devices.Clear();
            App.AppData.ConnectedAucovei = null;
            App.AppData.IsConnected = false;
        }

        private async Task GetDeviceList()
        {
            try
            {
                App.AppData.Vehicles = new ObservableCollection<Vehicle>();
                dynamic results = await this.svcHelper.GetDevicesAsync();
                if (results != null)
                {
                    JObject root = JObject.Parse(results);
                    JArray jdevices = root["data"] as JArray ?? new JArray();
                    foreach (var jdevice in jdevices)
                    {
                        Vehicle vehicle = new Vehicle();
                        JToken deviceName = jdevice["DeviceProperties"]["DeviceID"];
                        vehicle.DisplayName = deviceName.Value<string>();
                        vehicle.Id = deviceName.Value<string>();

                        JToken lat = jdevice["DeviceProperties"]["Latitude"];
                        JToken lon = jdevice["DeviceProperties"]["Longitude"];

                        if (lat != null && lon != null)
                        {
                            vehicle.StartPosition = new Windows.Devices.Geolocation.Geopoint(new Windows.Devices.Geolocation.BasicGeoposition()
                            {
                                Latitude = lat.Value<double>(),
                                Longitude = lon.Value<double>(),
                                // Altitude = 529.16547723114491
                            });
                        }

                        var generation = jdevice["Version"];
                        if (generation != null &&
                            !string.IsNullOrWhiteSpace(generation.ToString()))
                        {
                            if (double.TryParse(generation.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var version))
                            {
                                if (version > 1.0)
                                {
                                    vehicle.IsNewGeneration = true;
                                }
                            }
                        }

                        App.AppData.Vehicles.Add(vehicle);
                    }
                }
            }
            catch (Exception uex)
            {
                MessageDialog dlg = new MessageDialog(uex.Message);
                this.rootPage.ShowError(dlg);
            }
        }
    }
}
