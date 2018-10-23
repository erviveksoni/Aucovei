﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aucovei.Device
{
    /// <summary>
    /// Class containing Attributes and UUIDs that will populate the SDP record.
    /// </summary>
    class Constants
    {
        // The Chat Server's custom service Uuid: 34B1CF4D-1069-4AD6-89B6-E161D79BE4D8
        public static readonly Guid RfcommDeviceServiceUuid = Guid.Parse("34B1CF4D-1069-4AD6-89B6-E161D79BE4D9");

        // The Id of the Service Name SDP attribute
        public const UInt16 SdpServiceNameAttributeId = 0x100;

        // The SDP Type of the Service Name SDP attribute.
        // The first byte in the SDP Attribute encodes the SDP Attribute Type as follows :
        //    -  the Attribute Type size in the least significant 3 bits,
        //    -  the SDP Attribute Type value in the most significant 5 bits.
        public const byte SdpServiceNameAttributeType = (4 << 3) | 5;

        // The value of the Service Name SDP attribute
        public const string SdpServiceName = "Aucoveu Rfcomm Channel";

        public const int TriggerPin = 16;
        public const int LedPin = 26;
        public const int EchoPin = 12;

        public const double SafeDistanceCm = 50.0;

        public const int GpsBaudRate = 9600;

        public const string PrimaryAuthKey = "NXOkpJ5m+HQpfy/mWyxtjw==";
        public const string DeviceId = "aucovei02";
        public const string HostName = "aucoveidemo.azure-devices.net";
    }
}