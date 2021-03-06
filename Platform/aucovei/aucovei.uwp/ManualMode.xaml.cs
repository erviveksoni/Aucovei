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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using aucovei.uwp.Helpers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace aucovei.uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ManualMode : Page
    {
        private MainPage rootPage;
        private ServiceHelper svcHelper;
        private CancellationTokenSource tokenSrc;
        private CancellationTokenSource camButtonToken;
        private CancellationTokenSource commandToken;


        private ConcurrentQueue<Tuple<string, string, int>> commandQueue;
        private const string MoveVehicle = "MoveVehicle";
        private const string MoveCamera = "MoveCamera";
        private const string StopVehicle = "Stop";

        public ManualMode()
        {
            this.InitializeComponent();
            this.description.Text = "Manual Driving Mode";
            this.subdescription.Text = $"Manually navigate {App.AppData.ConnectedAucovei.DisplayName}";
            this.svcHelper = new ServiceHelper();
            App.AppData.PropertyChanged += this.AppDataPropertyChanged;
            this.AddEventListeners();
            this.commandQueue = new ConcurrentQueue<Tuple<string, string, int>>();

            this.commandToken = new CancellationTokenSource();
            this.SendCommandAsync(null, null, 0);
        }

        private void AddEventListeners()
        {
            this.CamUp.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerPressed), true);
            this.CamUp.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerReleased), true);

            this.CamDown.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerPressed), true);
            this.CamDown.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerReleased), true);

            this.CamLeft.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerPressed), true);
            this.CamLeft.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerReleased), true);

            this.CamRight.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerPressed), true);
            this.CamRight.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.CameraButtonOnPointerReleased), true);


            this.Forward.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerPressed), true);
            this.Forward.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerReleased), true);

            this.Reverse.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerPressed), true);
            this.Reverse.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerReleased), true);

            this.Left.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerPressed), true);
            this.Left.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerReleased), true);

            this.Right.AddHandler(PointerPressedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerPressed), true);
            this.Right.AddHandler(PointerReleasedEvent,
                new PointerEventHandler(this.DriveButtonOnPointerReleased), true);
        }

        private void DriveButtonHoldingEvent(object sender, HoldingRoutedEventArgs e)
        {
            var btn = sender as Button;
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                this.commandQueue.Enqueue(Tuple.Create(MoveVehicle, btn.Name, 10));
            }
            else
            {
                this.commandQueue.Enqueue(Tuple.Create(MoveVehicle, StopVehicle, 10));
            }
        }

        private void DriveButtonOnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
            var btn = sender as Button;
            if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                this.commandQueue.Enqueue(Tuple.Create(MoveVehicle, btn.Name, 10));
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void DriveButtonOnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
            var btn = sender as Button;
            if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                this.commandQueue.Enqueue(Tuple.Create(MoveVehicle, StopVehicle, 10));
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void DriveButtonOnClick(object sender, RoutedEventArgs e)
        {
            this.commandQueue.Enqueue(Tuple.Create(MoveVehicle, StopVehicle, 10));
        }

        private void CameraButtonOnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
            var btn = sender as Button;
            if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                this.camButtonToken = new CancellationTokenSource();
                Action loop = async () =>
                {
                    while (!this.camButtonToken.IsCancellationRequested)
                    {
                        this.commandQueue.Enqueue(Tuple.Create(MoveCamera, btn.Name.Replace("CAM", string.Empty, StringComparison.OrdinalIgnoreCase), 10));

                        await Task.Delay(TimeSpan.FromMilliseconds(2000));
                    }
                };

                loop.Invoke();
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void CameraButtonOnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
            var btn = sender as Button;
            if (this.camButtonToken != null &&
                ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                this.camButtonToken.Cancel();
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void CameraButtonHoldingEvent(object sender, HoldingRoutedEventArgs e)
        {
            var btn = sender as Button;
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                this.camButtonToken = new CancellationTokenSource();
                Action loop = async () =>
                {
                    while (!this.camButtonToken.IsCancellationRequested)
                    {
                        this.commandQueue.Enqueue(Tuple.Create(MoveCamera, btn.Name.Replace("CAM", string.Empty, StringComparison.OrdinalIgnoreCase), 10));

                        await Task.Delay(TimeSpan.FromMilliseconds(2000));
                    }
                };

                loop.Invoke();
            }
            else
            {
                this.camButtonToken.Cancel();
            }
        }

        private void CameraButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            this.commandQueue.Enqueue(Tuple.Create(MoveCamera, btn.Name.Replace("CAM", string.Empty, StringComparison.OrdinalIgnoreCase), 100));
        }

        private async void SendCommandAsync(string commandName, string data, int delay)
        {
            // this.rootPage.NotifyUser($"Command Started: {commandName}", NotifyType.StatusMessage);

            while (!this.commandToken.IsCancellationRequested)
            {
                int duration = 100;
                try
                {
                    Tuple<string, string, int> cmd;
                    if (this.commandQueue.TryDequeue(out cmd))
                    {
                        Func<Task> appFunction = () => this.svcHelper.SendCommandAsync(App.AppData.ConnectedAucovei.Id, cmd.Item1,
                            new KeyValuePair<string, string>("data", cmd.Item2));

                        await AzureRetryHelper.OperationWithBasicRetryAsync(appFunction);

                        duration = cmd.Item3;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Write(ex);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(duration));
                }
            }
        }

        private void AppDataPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName.Equals(nameof(App.AppData.IsVideoFeedActive)))
                {
                    if (this.tokenSrc == null &&
                        App.AppData.IsVideoFeedActive)
                    {
                        this.tokenSrc = new CancellationTokenSource();
                        this.ReadVideoFramesAsync();
                    }
                    else
                    {
                        this.tokenSrc?.Cancel();
                        this.tokenSrc = null;
                        this.ContainerGrid.Background = new ImageBrush
                        {
                            ImageSource = new BitmapImage(new
                                Uri(this.BaseUri, "Assets/movie.png")),
                            Stretch = Stretch.None
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog("An error has occured. Details: " + ex.Message);
                this.rootPage.ShowError(dialog);
            }
        }

        private async void ReadVideoFramesAsync()
        {
            try
            {
                if (this.tokenSrc.IsCancellationRequested)
                {
                    return;
                }

                string wsUri = $"{App.WebSocketEndpoint}{App.AppData.ConnectedAucovei.Id}";

                using (var socket = new ClientWebSocket())
                {
                    await socket.ConnectAsync(new Uri(wsUri), this.tokenSrc.Token);

                    while (socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            if (this.tokenSrc.IsCancellationRequested)
                            {
                                break;
                            }

                            var buffer = new ArraySegment<Byte>(new Byte[40960]);
                            WebSocketReceiveResult rcvResult = await socket.ReceiveAsync(buffer, (this.tokenSrc.Token));
                            if (rcvResult.MessageType == WebSocketMessageType.Binary)
                            {
                                List<byte> data = new List<byte>(buffer.Take(rcvResult.Count));
                                while (rcvResult.EndOfMessage == false)
                                {
                                    rcvResult = await socket.ReceiveAsync(buffer, CancellationToken.None);
                                    data.AddRange(buffer.Take(rcvResult.Count));
                                }

                                var task = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    BitmapImage bitmap = new BitmapImage();
                                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                                    {
                                        await stream.WriteAsync(data.ToArray().AsBuffer());
                                        stream.Seek(0);
                                        await bitmap.SetSourceAsync(stream);
                                    }

                                    RotateTransform aRotateTransform = new RotateTransform();
                                    aRotateTransform.CenterX = this.ContainerGrid.ActualWidth / 2;
                                    aRotateTransform.CenterY = this.ContainerGrid.ActualHeight / 2;
                                    aRotateTransform.Angle = 270.0;

                                    var imgBrush = new ImageBrush
                                    {
                                        ImageSource = bitmap,
                                        Stretch = Stretch.Uniform,
                                        Transform = aRotateTransform
                                    };

                                    this.ContainerGrid.Background = imgBrush;
                                });
                            }
                        }
                        catch
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                this.ReadVideoFramesAsync();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.rootPage = MainPage.Current;
            if (App.AppData.IsVideoFeedActive &&
                this.tokenSrc == null)
            {
                this.tokenSrc = new CancellationTokenSource();
                this.ReadVideoFramesAsync();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.SourcePageType.Name == nameof(SendWaypoints) &&
                App.AppData.ConnectedAucovei.WayPoints.Count < 2)
            {
                e.Cancel = true;
                this.rootPage.UpdateNavigation(3);
                this.rootPage.NotifyUser("Invalid action.", NotifyType.ErrorMessage);

                return;
            }

            this.commandToken?.Cancel();
            this.commandQueue?.Clear();

            if (App.AppData.IsVideoFeedActive)
            {
                this.tokenSrc?.Cancel();
                this.tokenSrc = null;
                this.ContainerGrid.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new
                        Uri(this.BaseUri, "Assets/movie.png")),
                    Stretch = Stretch.None
                };
            }

            this.rootPage.NotifyUser(string.Empty, NotifyType.StatusMessage);
        }
    }
}