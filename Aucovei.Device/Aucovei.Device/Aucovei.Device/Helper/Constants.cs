using System;

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

        public const double SafeDistanceCm = 35.0;

        public const int GpsBaudRate = 9600;

        public const string PrimaryAuthKey = "tda2MAMZXX9WhZDQmzGAnQ==";
        public const string DeviceId = "aucovei02";
        public const string HostName = "aucoveidemo.azure-devices.net";
        public const string ObjectTypePrefix = "";
        public const string OBJECT_TYPE_DEVICE_INFO = "DeviceInfo";
        public const string VERSION_2_0 = "2.0";


        public const string WebSocketEndpoint = "ws://aucoveidemo.azurewebsites.net/api/v1/videoframes/sender?deviceid=";
    }
}
