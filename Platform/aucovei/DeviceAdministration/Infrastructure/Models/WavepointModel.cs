using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models
{
    public class WavepointModel
    {
        public int WavepointId { get; set; }
        public double lat { get; set; }
        public double lon { get; set; }
        public string Status { get; set; }
    }
}
