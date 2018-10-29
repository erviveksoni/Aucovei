using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models
{
    public class RouteWavepointModel
    {
        public int RouteId { get; set; }

        public int WavepointId { get; set; }

        public double lat { get; set; }

        public double lon { get; set; }

        public string status { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
