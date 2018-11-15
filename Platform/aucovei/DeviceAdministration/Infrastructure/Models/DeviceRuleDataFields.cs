using System.Collections.Generic;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models
{
    public static class DeviceRuleDataFields
    {
        public static string Temperature
        { 
            get 
            { 
                return "Temperature"; 
            } 
        }

        public static string Speed
        {
            get
            {
                return "Speed";
            }
        }

        public static string IsObstacleDetected
        {
            get
            {
                return "IsObstacleDetected";
            }
        }

        private static List<string> _availableDataFields = new List<string>
        {
            Temperature, Speed, IsObstacleDetected
        };

        public static List<string> GetListOfAvailableDataFields()
        {
            return _availableDataFields;
        }
    }
}
