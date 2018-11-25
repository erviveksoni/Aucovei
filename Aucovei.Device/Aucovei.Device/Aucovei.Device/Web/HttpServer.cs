using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aucovei.Device.Configuration;
using Aucovei.Device.Devices;
using Aucovei.Device.Helper;
using Windows.AI.MachineLearning;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Aucovei.Device.Web
{
    public sealed class HttpServer
    {
        private const uint BUFFER_SIZE = 3024;
        private readonly StreamSocketListener _listener;
        private RoadSignDetectionMLModel mlModel;
        private CancellationTokenSource streamCancellationTokenSource;

        //Dependency objects
        private Camera _camera;
        private VideoSetting videoSetting;
        private bool isFeedActive;
        private StreamWebSocket cloudStreamWebSocket;

        public delegate void NotifyEventHandler(object sender, NotificationEventArgs e);

        public event NotifyEventHandler NotifyCallerEventHandler;

        public HttpServer(Camera camera, VideoSetting videoSetting)
        {
            this._camera = camera;
            this.videoSetting = videoSetting;
            this.isFeedActive = false;

            this._listener = new StreamSocketListener();
            this._listener.ConnectionReceived += this.ProcessRequest;
            this._listener.Control.KeepAlive = false;
            this._listener.Control.NoDelay = false;
            this._listener.Control.QualityOfService = SocketQualityOfService.LowLatency;
            this._listener.BindServiceNameAsync(80.ToString()).GetAwaiter();
            this.LoadModelAsync().GetAwaiter();
        }

        public async Task Start()
        {
            if (this.isFeedActive)
            {
                return;
            }

            this.streamCancellationTokenSource = new CancellationTokenSource();
            await this._camera.Initialize(this.videoSetting);
            this._camera.Start();
            this.isFeedActive = true;
            await this.StreamToCloudAsync();
            await this.PerformObstacleDetectionAsync();
        }

        public async Task Stop()
        {
            try
            {
                if (!this.isFeedActive)
                {
                    return;
                }

                this.streamCancellationTokenSource.Cancel();
                await this._camera.Stop();
                this.isFeedActive = false;
                this.OnNotifyEventHandler(false);
            }
            catch (Exception ex)
            {

            }
        }

        private async Task LoadModelAsync()
        {
            var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/{Constants.RoadSignDetectionMLModelFileName}"));
            this.mlModel = await RoadSignDetectionMLModel.Createmlmodel(modelFile);
        }

        private async void ProcessRequest(StreamSocketListener streamSocktetListener, StreamSocketListenerConnectionReceivedEventArgs eventArgs)
        {
            try
            {
                var socket = eventArgs.Socket;

                //Read request
                var request = await this.ReadRequest(socket);

                //Write Response
                await this.WriteResponse(request, socket);

                socket.InputStream.Dispose();
                socket.OutputStream.Dispose();
                socket.Dispose();
            }
            catch (Exception) { }
        }

        private async Task<HttpServerRequest> ReadRequest(StreamSocket socket)
        {
            var request = string.Empty;
            var error = false;

            var inputStream = socket.InputStream;

            var data = new byte[BUFFER_SIZE];
            var buffer = data.AsBuffer();

            var startReadRequest = DateTime.Now;
            while (!this.HttpGetRequestHasUrl(request))
            {
                if (DateTime.Now.Subtract(startReadRequest) >= TimeSpan.FromMilliseconds(5000))
                {
                    error = true;
                    return new HttpServerRequest(null, true);
                }

                var inputStreamReadTask = inputStream.ReadAsync(buffer, BUFFER_SIZE, InputStreamOptions.Partial);
                var timeout = TimeSpan.FromMilliseconds(1000);
                await TaskHelper.WithTimeoutAfterStart(ct => inputStreamReadTask.AsTask(ct), timeout);

                request += Encoding.UTF8.GetString(data, 0, (int)inputStreamReadTask.AsTask().Result.Length);
            }

            return new HttpServerRequest(request, error);
        }

        private async Task WriteResponse(HttpServerRequest request, StreamSocket socket)
        {
            var relativeUrlLower = request.Url.ToLowerInvariant();
            var outputStream = socket.OutputStream;

            //Get javascript files
            if (relativeUrlLower.StartsWith("/javascript"))
            {
                await HttpServerResponse.WriteResponseFile(this.ToFolderPath(request.Url), HttpContentType.JavaScript, outputStream);
            }
            //Get css style files
            else if (relativeUrlLower.StartsWith("/styles"))
            {
                await HttpServerResponse.WriteResponseFile(this.ToFolderPath(request.Url), HttpContentType.Css, outputStream);
            }
            //Get video setting
            else if (relativeUrlLower.StartsWith("/videosetting"))
            {
                HttpServerResponse.WriteResponseJson(ConfigurationFile.VideoSetting.Stringify(), outputStream);
            }
            //Get supported video settings
            else if (relativeUrlLower.StartsWith("/supportedvideosettings"))
            {
                HttpServerResponse.WriteResponseJson(ConfigurationFile.VideoSettingsSupported.Stringify(), outputStream);
            }
            //Set video settings
            else if (relativeUrlLower.StartsWith("/savevideosetting"))
            {
                await this._camera.Stop();

                var videoSetting = new VideoSetting
                {
                    VideoSubtype = VideoSubtypeHelper.Get(request.Body["VideoSubtype"].GetString()),
                    VideoResolution = (VideoResolution)request.Body["VideoResolution"].GetNumber(),
                    VideoQuality = request.Body["VideoQuality"].GetNumber(),
                    UsedThreads = (int)request.Body["UsedThreads"].GetNumber()
                };

                await ConfigurationFile.Write(videoSetting);
                await this._camera.Initialize(videoSetting);
                this._camera.Start();

                HttpServerResponse.WriteResponseOk(outputStream);
            }
            //Get current camera frame
            else if (relativeUrlLower.StartsWith("/videoframe"))
            {
                if (this._camera.Frame != null)
                {
                    var webSocket = new WebSocket(socket, request, this._camera);
                    await webSocket.Start();
                }
                else
                {
                    HttpServerResponse.WriteResponseError("Not camera fram available. Maybe there is an error or camera is not started.", outputStream);
                }
            }
            //Get index.html page
            else
            {
                await HttpServerResponse.WriteResponseFile(@"\Html\Index.html", HttpContentType.Html, outputStream);
            }
        }

        private bool HttpGetRequestHasUrl(string httpRequest)
        {
            var regex = new Regex("GET.*HTTP.*\r\n", RegexOptions.IgnoreCase);
            return regex.IsMatch(httpRequest.ToUpper());
        }

        private string ToFolderPath(string relativeUrl)
        {
            var folderPath = relativeUrl.Replace('/', '\\');
            return folderPath;
        }

        public async Task PerformObstacleDetectionAsync()
        {
            try
            {
                SoftwareBitmap templateImage;
                StorageFile file =
                    await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(
                        @"Assets\" + "stop_sign_prototype.png");
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    templateImage = await decoder.GetSoftwareBitmapAsync();
                }

                var task = Task.Run(async () =>
                {
                    while (!this.streamCancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            if (this._camera.Bitmap != null)
                            {
                                VideoFrame rawImage = VideoFrame.CreateWithSoftwareBitmap(this._camera.Bitmap);
                                RoadSignDetectionMLModelInput input = new RoadSignDetectionMLModelInput
                                {
                                    Data = ImageFeatureValue.CreateFromVideoFrame(rawImage)
                                };

                                var output = await this.mlModel.EvaluateAsync(input);
                                var result = output.ClassLabel.GetAsVectorView()[0];
                                var loss = output.Loss[0][result] * 100.0f;
                                if (string.Equals(result, "stop", StringComparison.OrdinalIgnoreCase) && loss > 50.0)
                                {
                                    // send high signal for 5 seconds
                                    int counter = 10;
                                    while (counter > 0)
                                    {
                                        counter--;
                                        this.OnNotifyEventHandler(true);
                                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                                    }
                                }
                                else
                                {
                                    this.OnNotifyEventHandler(false);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            //
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(10));
                    }
                }, this.streamCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void OnNotifyEventHandler(bool isStopSignDetected)
        {
            this.NotifyCallerEventHandler?.Invoke(this, new NotificationEventArgs()
            {
                IsObstacleDetected = isStopSignDetected
            });
        }

        private void MStreamWebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            this.closeSocket(sender);
        }

        public async Task StreamToCloudAsync()
        {
            if (this.cloudStreamWebSocket != null)
            {
                this.closeSocket(this.cloudStreamWebSocket);
            }

            this.cloudStreamWebSocket = new StreamWebSocket();
            this.cloudStreamWebSocket.Closed += this.MStreamWebSocket_Closed;

            try
            {
                await this.cloudStreamWebSocket.ConnectAsync(new Uri(string.Format("{0}{1}", Constants.WebSocketEndpoint, Constants.DeviceId)));

                var task = Task.Run(async () =>
                {
                    var socket = this.cloudStreamWebSocket;
                    while (!this.streamCancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            if (this._camera.Frame != null)
                            {
                                var clone = this._camera.Frame.ToArray();
                                await socket.OutputStream.WriteAsync(clone.AsBuffer());
                                await Task.Delay(100);
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }, this.streamCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                this.cloudStreamWebSocket.Dispose();
                this.cloudStreamWebSocket = null;
            }
        }

        private void closeSocket(IWebSocket webSocket)
        {
            try
            {
                webSocket.Close(1000, "Closed due to user request.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }

    public class NotificationEventArgs : EventArgs
    {
        public bool IsObstacleDetected;
    }
}
