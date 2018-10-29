using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models
{
    public class RouteWavepointUpdateModel
    {
        public int RouteId { get; set; }

        public int WavepointId { get; set; }

        public string Status { get; set; }
    }
}
