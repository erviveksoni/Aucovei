using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Q.IoT.Devices.Core
{
    public interface IComponetController
    {
        void AppendCommand(params byte[] cmds);
        void AppendData(params byte[] data);
        void SetCommand(params byte[] cmds);
        void SetData(params byte[] data);
    }
}
