namespace EcaInformationSystem.Api.ZkDevice
{
    // Command codes for ZKTeco's undocumented "pull SDK" TCP protocol.
    // Values here are confirmed against a live K14/ID device (firmware
    // Ver 6.60) by byte-diffing against pyzk, a mature reference
    // implementation — several of these differ from what's commonly quoted
    // in community write-ups (DataWrrq is 1503, not 1504; FctUser is 5, not
    // 1) and getting them wrong causes the device to silently drop the
    // packet rather than reply with an error.
    internal static class ZkCommand
    {
        public const ushort Connect = 1000;
        public const ushort Exit = 1001;
        public const ushort EnableDevice = 1002;
        public const ushort DisableDevice = 1003;
        public const ushort Auth = 1102;

        public const ushort AckOk = 2000;
        public const ushort AckError = 2001;
        public const ushort AckData = 2002;
        public const ushort AckUnauth = 2005;

        public const ushort PrepareData = 1500;
        public const ushort Data = 1501;
        public const ushort FreeData = 1502;

        // Initiates a buffered bulk-data request (attendance log / user list).
        public const ushort DataWrrq = 1503;

        // Requests one chunk of an already-announced (PrepareData) transfer —
        // payload is (start:int32, size:int32).
        public const ushort ReadChunk = 1504;

        public const ushort AttLogRrq = 13;
        public const ushort UserTempRrq = 9;
        public const ushort GetFreeSizes = 50;

        // "fct" sub-code for DataWrrq when pulling the user list.
        public const int FctUser = 5;
    }
}
