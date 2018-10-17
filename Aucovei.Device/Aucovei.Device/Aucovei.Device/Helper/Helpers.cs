using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Windows.Networking.Connectivity;

namespace Aucovei.Device.Helper
{
    public class Helpers
    {
        public static string MapDrivingDirectionToCommand(Commands.DrivingDirection drivingDirection)
        {
            switch (drivingDirection)
            {
                default:
                case Commands.DrivingDirection.Forward:
                    return Commands.DriveForward;
                case Commands.DrivingDirection.Reverse:
                    return Commands.DriveReverse;
                case Commands.DrivingDirection.Left:
                    return Commands.DriveLeft;
                case Commands.DrivingDirection.Right:
                    return Commands.DriveRight;
            }
        }

        public static string MapCameraDirectionToCommand(Commands.CameraDirection cameraDirection)
        {
            switch (cameraDirection)
            {
                default:
                case Commands.CameraDirection.Center:
                    return Commands.PanTiltCenter;
                case Commands.CameraDirection.Up:
                    return Commands.TiltUp;
                case Commands.CameraDirection.Down:
                    return Commands.TiltDown;
                case Commands.CameraDirection.Left:
                    return Commands.PanLeft;
                case Commands.CameraDirection.Right:
                    return Commands.PanRight;
            }
        }

        public static IPAddress GetIPAddress()
        {
            var IpAddress = new List<string>();
            var Hosts = NetworkInformation.GetHostNames().ToList();
            foreach (var Host in Hosts)
            {
                var IP = Host.DisplayName;
                IpAddress.Add(IP);
            }

            var address = IPAddress.Parse(IpAddress.Last());
            return address;
        }


        public static double ConvertRPSToMeterPerSecond(string data)
        {
            const double wheelradiusMeters = 0.0315;
            if (int.TryParse(data, out var rps))
            {
                var speed = wheelradiusMeters * (2 * Math.PI) * (rps);
                return Math.Round(speed, 2);
            }

            return 0;
        }
    }
}
