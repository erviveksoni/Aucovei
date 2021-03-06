﻿using System;
using System.Collections.Generic;


namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models
{
    /// <summary>
    /// A model that represents a Device's telemetry recording.
    /// </summary>
    public class DeviceTelemetryModel
    {
    
        /// <summary>
        /// Gets or sets the ID of the Device for which telemetry applies.
        /// </summary>
        public string DeviceId
        {
            get;
            set;
        }

        /// <summary>
        /// Values for telemetry data associated with individual fields
        /// </summary>
        private IDictionary<string, double> values = new Dictionary<string, double>();
        public IDictionary<string, double> Values
        {
            get { return values; }
            set { values = value; }
        }

        /// <summary>
        /// Values for telemetry data associated with individual fields
        /// </summary>
        private IDictionary<string, bool> boolvalues = new Dictionary<string, bool>();
        public IDictionary<string, bool> BoolValues
        {
            get { return boolvalues; }
            set { boolvalues = value; }
        }

        /// <summary>
        /// Values for telemetry data associated with individual fields
        /// </summary>
        private IDictionary<string, string> stringvalues = new Dictionary<string, string>();
        public IDictionary<string, string> StringValues
        {
            get { return stringvalues; }
            set { stringvalues = value; }
        }

        /// <summary>
        /// Gets or sets the time of record for the represented telemetry 
        /// recording.
        /// </summary>
        public DateTime? Timestamp
        {
            get;
            set;
        }
    }
}
