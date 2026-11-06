using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BBTechSSDTool.Services.Native
{
    public static class Win32Storage
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        // IOCTL codes
        public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
        public const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;
        public const uint SMART_GET_VERSION = 0x00074080;
        public const uint SMART_RCV_DRIVE_DATA = 0x0007C088;
        public const uint IOCTL_SCSI_PASS_THROUGH = 0x0004D004;
        public const uint IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x0004D014;

        // Storage Property IDs
        public const uint StorageDeviceProperty = 0;
        public const uint StorageAdapterProperty = 1;
        public const uint StorageDeviceSeekPenaltyProperty = 7;
        public const uint StorageAdapterProtocolSpecificProperty = 49;
        public const uint StorageDeviceProtocolSpecificProperty = 50;

        public const uint PropertyStandardQuery = 0;
        public const uint ProtocolTypeNvme = 1;
        public const uint STORAGE_PROTOCOL_NVME_DATA_TYPE_LOG_PAGE = 2;
        public const uint NVME_LOG_PAGE_HEALTH_INFO = 2;

        // Device Arrival / Notification constants
        public const int WM_DEVICECHANGE = 0x0219;
        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int DBT_DEVNODES_CHANGED = 0x0007;
        public const int DBT_CONFIGCHANGED = 0x0018;
        public const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        public static readonly Guid GUID_DEVINTERFACE_DISK = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
        public static readonly Guid GUID_DEVINTERFACE_VOLUME = new("53f5630d-b6bf-11d0-94f2-00a0c91efb8b");

        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string dbcc_name;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotification(
            IntPtr hRecipient,
            IntPtr NotificationFilter,
            uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterDeviceNotification(IntPtr Handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [StructLayout(LayoutKind.Sequential)]
        public struct STORAGE_PROPERTY_QUERY
        {
            public uint PropertyId;
            public uint QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STORAGE_DEVICE_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            public byte DeviceType;
            public byte DeviceTypeModifier;
            public byte RemovableMedia;
            public byte CommandQueueing;
            public uint VendorIdOffset;
            public uint ProductIdOffset;
            public uint ProductRevisionOffset;
            public uint SerialNumberOffset;
            public int BusType;
            public uint RawPropertiesLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVICE_SEEK_PENALTY_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            [MarshalAs(UnmanagedType.I1)]
            public bool IncursSeekPenalty;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISK_GEOMETRY
        {
            public long Cylinders;
            public int MediaType;
            public uint TracksPerCylinder;
            public uint SectorsPerTrack;
            public uint BytesPerSector;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISK_GEOMETRY_EX
        {
            public DISK_GEOMETRY Geometry;
            public long DiskSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] Data;
        }

        // ATA SMART structs
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct GETVERSIONINPARAMS
        {
            public byte bVersion;
            public byte bRevision;
            public byte bReserved;
            public byte bIDEDeviceMap;
            public uint fCapabilities;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public uint[] dwReserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct IDEREGS
        {
            public byte bFeaturesReg;
            public byte bSectorCountReg;
            public byte bSectorNumberReg;
            public byte bCylLowReg;
            public byte bCylHighReg;
            public byte bDriveHeadReg;
            public byte bCommandReg;
            public byte bReserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SENDCMDINPARAMS
        {
            public uint cBufferSize;
            public IDEREGS irDriveRegs;
            public byte bDriveNumber;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] bReserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public uint[] dwReserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] bBuffer;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DRIVERSTATUS
        {
            public byte bDriverError;
            public byte bIDEError;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] bReserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] dwReserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SENDCMDOUTPARAMS
        {
            public uint cBufferSize;
            public DRIVERSTATUS DriverStatus;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] bBuffer;
        }

        // fmifs.dll FormatEx delegate
        public enum FMIFS_CALLBACK_COMMAND
        {
            PROGRESS = 0,
            DONEWITHSTRUCTURE = 1,
            INSUFFICIENTRIGHTS = 2,
            WRITEPROTECTED = 3,
            STRUCTUREPROGRESS = 4,
            CLUSTERSIZESTOOSMALL = 5,
            CLUSTERSIZETOOBIG = 6,
            VOLUMEINUSE = 7,
            VALUENOTSUPPORTED = 8,
            VOLUMEISDIRTY = 9,
            DONE = 10,
            BADBLOCKS = 11
        }

        public delegate bool FormatExCallback(
            FMIFS_CALLBACK_COMMAND command,
            uint subAction,
            IntPtr actionInfo);

        [DllImport("fmifs.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern void FormatEx(
            string driveRoot,
            uint mediaFlag,
            string format,
            string label,
            bool quickFormat,
            uint clusterSize,
            FormatExCallback callback);
    }
}
