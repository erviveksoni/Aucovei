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
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using aucovei.uwp.Helpers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
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
        ServiceHelper svcHelper;
        private CancellationTokenSource tokenSrc;

        public ManualMode()
        {
            this.InitializeComponent();
            this.description.Text = "Manual Driving Mode";
            this.subdescription.Text = $"Manually navigate {App.AppData.ConnectedAucovei.DisplayName}";
            this.svcHelper = new ServiceHelper();
            App.AppData.PropertyChanged += this.AppDataPropertyChanged;
        }

        private void AppDataPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName.Equals(nameof(App.AppData.IsVideoFeedActive)))
                {
                    if (App.AppData.IsVideoFeedActive)
                    {
                        this.tokenSrc = new CancellationTokenSource();
                        this.ReadVideoFramesAsync();
                    }
                    else
                    {
                        this.tokenSrc?.Cancel();
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

            if (App.AppData.IsVideoFeedActive)
            {
                this.tokenSrc?.Cancel();
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