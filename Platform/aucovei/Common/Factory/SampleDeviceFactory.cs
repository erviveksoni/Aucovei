using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.DeviceSchema;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Models;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Factory
{
    public static class SampleDeviceFactory
    {
        public const string OBJECT_TYPE_DEVICE_INFO = "DeviceInfo";

        public const string VERSION_1_0 = "1.0";
        public const string VERSION_2_0 = "2.0";

        private static Random rand = new Random();

        private const int MAX_COMMANDS_SUPPORTED = 6;

        private const bool IS_SIMULATED_DEVICE = true;

        private static List<string> DefaultDeviceNames = new List<string>{
            "aucovei02"
            /*,"aucovei03",
            "aucovei04",
            "aucovei05"*/
        };

        private class Location
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }

            public Location(double latitude, double longitude)
            {
                Latitude = latitude;
                Longitude = longitude;
            }

        }

        private static List<Location> _possibleDeviceLocations = new List<Location>{
            new Location(17.431939, 78.343703),// Microsoft Red West Campus, Building A
            new Location(17.432894, 78.343050),  // 800 Occidental Ave S, Seattle, WA 98134
            new Location(17.432304, 78.342667),  // 11111 NE 8th St, Bellevue, WA 98004
            new Location(17.431253, 78.343634),  // 3003 160th Ave SE Bellevue, WA 98008
            new Location(17.430999, 78.342786),  // 15580 NE 31st St Redmond, WA 98008
            new Location(17.431466, 78.342098),  // 15255 NE 40th St Redmond, WA 98008
            new Location(17.433980, 78.343757),  // 320 Westlake Ave N, Seattle, WA 98109
            new Location(17.433827, 78.345077), // 15010 NE 36th St, Redmond, WA 98052
            new Location(17.432511, 78.344132), //500 108th Ave NE, Bellevue, WA 98004 
            new Location(17.431088, 78.343527), //3460 157th Ave NE, Redmond, WA 98052
            new Location(17.430754, 78.342650), //11155 NE 8th St, Bellevue, WA 98004
            new Location(17.433308, 78.343201) //18500 NE Union Hill Rd, Redmond, WA 98052
            /*,new Location(47.642528, -122.130565), //3600 157th Ave NE, Redmond, WA 98052
            new Location(47.642876, -122.125492), //16070 NE 36th Way Bldg 33, Redmond, WA 98052
            new Location(47.637376, -122.140445), //14999 NE 31st Way, Redmond, WA 98052
            new Location(47.636121, -122.130254) //3009 157th Pl NE, Redmond, WA 98052*/
        };

        public static dynamic GetSampleSimulatedDevice(string deviceId, string key)
        {
            dynamic device = DeviceSchemaHelper.BuildDeviceStructure(deviceId, true, null);

            AssignDeviceProperties(deviceId, device);
            device.ObjectType = OBJECT_TYPE_DEVICE_INFO;
            device.Version = VERSION_1_0;
            device.IsSimulatedDevice = IS_SIMULATED_DEVICE;

            AssignTelemetry(device);
            AssignCommands(device);

            return device;
        }

        public static dynamic GetSampleDevice(Random randomNumber, SecurityKeys keys)
        {
            string deviceId =
                string.Format(
                    CultureInfo.InvariantCulture,
                    "00000-DEV-{0}C-{1}LK-{2}D-{3}",
                    MAX_COMMANDS_SUPPORTED,
                    randomNumber.Next(99999),
                    randomNumber.Next(99999),
                    randomNumber.Next(99999));

            dynamic device = DeviceSchemaHelper.BuildDeviceStructure(deviceId, false, null);
            device.ObjectName = "IoT Device Description";

            AssignDeviceProperties(deviceId, device);
            AssignTelemetry(device);
            AssignCommands(device);

            return device;
        }

        private static void AssignDeviceProperties(string deviceId, dynamic device)
        {
            int randomId = rand.Next(0, _possibleDeviceLocations.Count - 1);
            dynamic deviceProperties = DeviceSchemaHelper.GetDeviceProperties(device);
            deviceProperties.HubEnabledState = true;
            deviceProperties.SerialNumber = "SER" + randomId;
            deviceProperties.FirmwareVersion = "1." + randomId;
            /*deviceProperties.Manufacturer = "Contoso Inc.";
            deviceProperties.ModelNumber = "MD-" + randomId;
            deviceProperties.Platform = "Plat-" + randomId;
            deviceProperties.Processor = "i3-" + randomId;
            deviceProperties.InstalledRAM = randomId + " MB";*/
            deviceProperties.BatteryCapacity = randomId * 2000 + " Ah";
            deviceProperties.OperatingVoltage = randomId * 20 + " V";
            deviceProperties.LoadingCapacity = randomId * 100 + " Kg";
            deviceProperties.WheelBase = randomId + 2000 + " mm";
            deviceProperties.MaxOperatingTemp = randomId + 100 + " °c";

            // Choose a location among the 16 above and set Lat and Long for device properties
            deviceProperties.Latitude = _possibleDeviceLocations[randomId].Latitude;
            deviceProperties.Longitude = _possibleDeviceLocations[randomId].Longitude;
        }

        private static void AssignTelemetry(dynamic device)
        {
            dynamic telemetry = CommandSchemaHelper.CreateNewTelemetry("Temperature", "Temperature", "double");
            CommandSchemaHelper.AddTelemetryToDevice(device, telemetry);

            telemetry = CommandSchemaHelper.CreateNewTelemetry("Speed", "Speed", "double");
            CommandSchemaHelper.AddTelemetryToDevice(device, telemetry);

            telemetry = CommandSchemaHelper.CreateNewTelemetry("CameraStatus", "CameraStatus", "bool");
            CommandSchemaHelper.AddTelemetryToDevice(device, telemetry);

            telemetry = CommandSchemaHelper.CreateNewTelemetry("DeviceIp", "DeviceIp", "string");
            CommandSchemaHelper.AddTelemetryToDevice(device, telemetry);
        }

        private static void AssignCommands(dynamic device)
        {
            dynamic command = CommandSchemaHelper.CreateNewCommand("PingDevice");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("SendBuzzer");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("SendWaypoints");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "string");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("EmergencyStop");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("SetLights");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "boolean");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("SetCameras");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "boolean");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("StartTelemetry");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("StopTelemetry");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("DemoRun");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "int");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            /*command = CommandSchemaHelper.CreateNewCommand("ChangeSetPointTemp");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "SetPointTemp", "double");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("DiagnosticTelemetry");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "Active", "boolean");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("ChangeDeviceState");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "DeviceState", "string");
            CommandSchemaHelper.AddCommandToDevice(device, command);*/
        }

        public static List<string> GetDefaultDeviceNames()
        {
            long milliTime = DateTime.Now.Millisecond;
            return DefaultDeviceNames.Select(r => string.Concat(r, "_" + milliTime)).ToList();
        }
    }
}
