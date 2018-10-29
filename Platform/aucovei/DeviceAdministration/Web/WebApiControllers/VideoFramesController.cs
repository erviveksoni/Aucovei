using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.WebSockets;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.WebApiControllers
{
    public class SocketConnection
    {
        public string DeviceId { get; set; }
        public WebSocket Socket { get; set; }
    }

    [RoutePrefix("api/v1/videoframes")]
    public class VideoFramesController : ApiController
    {
        public static List<SocketConnection> Sockets = new List<SocketConnection>();

        public VideoFramesController()
        {
            Sockets = new List<SocketConnection>();
        }

        [HttpGet]
        [Route("sender")]
        public HttpResponseMessage Sender()
        {
            var currentContext = HttpContext.Current;
            if (currentContext.IsWebSocketRequest ||
                currentContext.IsWebSocketRequestUpgrading)
            {
                currentContext.AcceptWebSocketRequest(this.ProcessSenderWebsocketSession);
            }

            return this.Request.CreateResponse(HttpStatusCode.SwitchingProtocols);
        }

        [HttpGet]
        [Route("receiver")]
        public HttpResponseMessage Receiver()
        {
            var currentContext = HttpContext.Current;
            if (currentContext.IsWebSocketRequest ||
                currentContext.IsWebSocketRequestUpgrading)
            {
                currentContext.AcceptWebSocketRequest(this.ProcessReceiverWebsocketSession);
            }

            return this.Request.CreateResponse(HttpStatusCode.SwitchingProtocols);
        }

        private async Task ProcessSenderWebsocketSession(AspNetWebSocketContext context)
        {
            if (!context.QueryString.AllKeys.Contains("deviceid"))
            {
                return;
            }

            var deviceid = context.QueryString["deviceid"]?.ToString();
            var webSocket = context.WebSocket;
            if (webSocket.State == WebSocketState.Open)
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var buffer = new ArraySegment<Byte>(new Byte[4096]);
                    var received = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                    switch (received.MessageType)
                    {
                        case WebSocketMessageType.Close:
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed in server by the client", CancellationToken.None);
                            continue;
                        case WebSocketMessageType.Binary:
                            List<byte> data = new List<byte>(buffer.Take(received.Count));
                            while (received.EndOfMessage == false)
                            {
                                received = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                                data.AddRange(buffer.Take(received.Count));
                            }

                            var socketconnectionList = Sockets.Where(x => x.DeviceId.Equals(deviceid, StringComparison.Ordinal)).ToArray();

                            foreach (var socketconnection in socketconnectionList)
                            {
                                var destsocket = socketconnection.Socket;
                                if (destsocket.State == System.Net.WebSockets.WebSocketState.Open)
                                {
                                    var type = WebSocketMessageType.Binary;

                                    try
                                    {
                                        await destsocket.SendAsync(new ArraySegment<byte>(data.ToArray()), type, true, CancellationToken.None);
                                    }
                                    catch (Exception ex)
                                    {
                                        // AppInsights.Client.TrackException(ex);
                                    }
                                }
                                else
                                {
                                    // AppInsights.Client.TrackTrace("Removing closed connection");
                                    Sockets.Remove(socketconnection);
                                }
                            }

                            break;
                    }
                }
            }
        }

        private async Task ProcessReceiverWebsocketSession(AspNetWebSocketContext context)
        {
            if (!context.QueryString.AllKeys.Contains("deviceid"))
            {
                return;
            }

            var deviceid = context.QueryString["deviceid"]?.ToString();
            var webSocket = context.WebSocket;
            if (webSocket.State == WebSocketState.Open)
            {
                var existigsocketconnection = Sockets.FirstOrDefault(x => x.DeviceId.Equals(deviceid));
                if (existigsocketconnection != null)
                {
                    Sockets.Remove(existigsocketconnection);
                }

                Sockets.Add(new SocketConnection()
                {
                    DeviceId = deviceid,
                    Socket = context.WebSocket
                });

                while (webSocket.State == WebSocketState.Open)
                {
                    var buffer = new ArraySegment<Byte>(new Byte[4096]);
                    var received = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                    switch (received.MessageType)
                    {
                        case WebSocketMessageType.Close:
                            var socket = Sockets.FirstOrDefault(x => x.Socket == webSocket);
                            Sockets.Remove(socket);
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "Closed in server by the client", CancellationToken.None);
                            continue;
                    }
                }
            }
        }
    }
}