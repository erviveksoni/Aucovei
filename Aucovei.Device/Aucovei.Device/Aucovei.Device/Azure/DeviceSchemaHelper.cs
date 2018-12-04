using System;
using Newtonsoft.Json.Linq;

namespace Aucovei.Device.Azure
{
    /// <summary>
    /// Helper class to encapsulate interactions with the device schema.
    ///
    /// Elsewhere in the app we try to always deal with this flexible schema as dynamic,
    /// but here we take a dependency on Json.Net where necessary to populate the objects
    /// behind the schema.
    /// </summary>
    public static class DeviceSchemaHelper
    {
        /// <summary>
        /// Gets a DeviceProperties instance from a Device.
        /// </summary>
        /// <param name="device">
        /// The Device from which to extract a DeviceProperties instance.
        /// </param>
        /// <returns>
        /// A DeviceProperties instance, extracted from <paramref name="device"/>.
        /// </returns>
        public static dynamic GetDeviceProperties(dynamic device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }


            var props = device.DeviceProperties;

            if (props == null)
            {
                throw new Exception("'DeviceProperties' property is missing");
            }

            return props;
        }

        /// <summary>
        /// Build a valid device representation in the dynamic format used throughout the app.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="isSimulated"></param>
        /// <param name="iccid"></param>
        /// <returns></returns>
        public static dynamic BuildDeviceStructure(string deviceId)
        {
            JObject device = new JObject();

            InitializeDeviceProperties(device, deviceId, false);
            InitializeSystemProperties(device, null);
            AssignCommands(device);
            AssignTelemetry(device);

            return device;
        }

        /// <summary>
        /// Initialize the device properties for a new device.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="deviceId"></param>
        /// <param name="isSimulated"></param>
        /// <returns></returns>
        public static void InitializeDeviceProperties(dynamic device, string deviceId, bool isSimulated)
        {
            JObject deviceProps = new JObject();
            deviceProps.Add(DevicePropertiesConstants.DEVICE_ID, deviceId);
            deviceProps.Add(DevicePropertiesConstants.HUB_ENABLED_STATE, 1);
            deviceProps.Add(DevicePropertiesConstants.CREATED_TIME, DateTime.UtcNow);
            deviceProps.Add(DevicePropertiesConstants.DEVICE_STATE, "normal");
            deviceProps.Add(DevicePropertiesConstants.UPDATED_TIME, null);
            deviceProps.Add(DevicePropertiesConstants.SERIAL_NUMBER, "SER" + 1);
            deviceProps.Add(DevicePropertiesConstants.FIRMWARE_VERSION, "1.1");
            deviceProps.Add(DevicePropertiesConstants.BATTERY_CAPACITY, "2000 Ah");
            deviceProps.Add(DevicePropertiesConstants.OPERATING_VOLTAGE, "240 v");
            deviceProps.Add(DevicePropertiesConstants.LPOADING_CAPACITY, "1000kg");
            deviceProps.Add(DevicePropertiesConstants.WHEEL_BASE, "2000 m");
            deviceProps.Add(DevicePropertiesConstants.MAX_OPERATING_TEMP, "120 °c");
            deviceProps.Add(DevicePropertiesConstants.LATITUDE, 17.442965);
            deviceProps.Add(DevicePropertiesConstants.LONGITUDE, 78.3575231);

            (device as JObject).Add(DeviceModelConstants.DEVICE_PROPERTIES, deviceProps);
        }

        /// <summary>
        /// Initialize the system properties for a new device.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="iccid"></param>
        /// <returns></returns>
        private static void InitializeSystemProperties(dynamic device, string iccid)
        {
            JObject systemProps = new JObject();
            systemProps.Add(SystemPropertiesConstants.ICCID, iccid);

            (device as JObject).Add(DeviceModelConstants.SYSTEM_PROPERTIES, systemProps);
        }

        /// <summary>
        /// Remove the system properties from a device, to better emulate the behavior of real devices when sending device info messages.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="iccid"></param>
        /// <param name="isSimulated"></param>
        /// <returns></returns>
        public static void RemoveSystemPropertiesForSimulatedDeviceInfo(dynamic device)
        {
            // Our simulated devices share the structure code with the rest of the system,
            // so we need to explicitly handle this case; since this is only an issue when
            // the code is shared in this way, this special case is kept separate from the
            // rest of the initialization code which would be present in a non-simulated system
            (device as JObject).Remove(DeviceModelConstants.SYSTEM_PROPERTIES);
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

            telemetry = CommandSchemaHelper.CreateNewTelemetry("IsObstacleDetected", "IsObstacleDetected", "bool");
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

            command = CommandSchemaHelper.CreateNewCommand("SetCamera");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "boolean");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("StartTelemetry");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("StopTelemetry");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("DemoRun");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "int");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("MoveVehicle");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "string");
            CommandSchemaHelper.AddCommandToDevice(device, command);

            command = CommandSchemaHelper.CreateNewCommand("MoveCamera");
            CommandSchemaHelper.DefineNewParameterOnCommand(command, "data", "string");
            CommandSchemaHelper.AddCommandToDevice(device, command);
        }
    }
}
