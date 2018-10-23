﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dynamitey;

namespace Aucovei.Device.Azure
{
    public static class WireCommandSchemaHelper
    {
        public static dynamic GetParameters(dynamic command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            IEnumerable<string> members = Dynamic.GetMemberNames(command);
            if (!members.Any(m => m == "Parameters"))
            {
                return null;
            }

            dynamic parameters = command.Parameters;

            return parameters;
        }
    }
}
