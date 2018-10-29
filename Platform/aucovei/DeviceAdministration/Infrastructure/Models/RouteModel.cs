using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models
{
    public class RouteModel
    {
        public int RouteId { get; set; }

        public string RouteStatus { get; set; }

        public List<dynamic> Wavepoints { get; set; }
    }
}
