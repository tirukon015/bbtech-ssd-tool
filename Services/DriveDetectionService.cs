using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using BBTechSSDTool.Models;
using BBTechSSDTool.Services.Native;
using Microsoft.Win32.SafeHandles;

namespace BBTechSSDTool.Services
{
    public class DriveDetectionService
    {
        private readonly HealthEngineService _healthEngine;
        private string _systemDriveLetter = "C:";

        public DriveDetectionService(HealthEngineService healthEngine)
        {
            _healthEngine = healthEngine;
            try
            {
                string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                _systemDriveLetter = Path.GetPathRoot(sysDir)?.TrimEnd('\\') ?? "C:";
            }
            catch
            {
                _systemDriveLetter = "C:";
            }
        }

        public int GetPhysicalDriveCount()
        {
            int count = 0;
            for (uint diskNum = 0; diskNum < 32; diskNum++)
            {
                string path = $@"\\.\PhysicalDrive{diskNum}";
                using SafeFileHandle hDisk = Win32Storage.CreateFile(
                    path,
                    0,
                    Win32Storage.FILE_SHARE_READ | Win32Storage.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Storage.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (!hDisk.IsInvalid)
                    count++;
            }
            return count;
        }

        public List<PhysicalDriveInfo> GetAllDrives()
        {
            var drives = new List<PhysicalDriveInfo>();
            var driveVolumesMap = GetDiskToVolumesMapping();
            int systemDiskNumber = DetectSystemDiskNumber();

            // Probe PhysicalDrive0 through PhysicalDrive32
            for (uint diskNum = 0; diskNum < 32; diskNum++)
            {
                string path = $@"\\.\PhysicalDrive{diskNum}";
                using SafeFileHandle hDisk = Win32Storage.CreateFile(
                    path,
                    0, // Query access
                    Win32Storage.FILE_SHARE_READ | Win32Storage.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Storage.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hDisk.IsInvalid)
                    continue;

                var drive = QueryDriveDescriptor(hDisk, diskNum);
                if (drive != null)
                {
                    // Check system drive status
                    drive.IsSystemDrive = ((int)diskNum == systemDiskNumber);

                    // Add volumes
                    if (driveVolumesMap.TryGetValue(diskNum, out var vols))
                    {
                        drive.Volumes = vols;
                        if (vols.Any(v => v.StartsWith(_systemDriveLetter, StringComparison.OrdinalIgnoreCase)))
                            drive.IsSystemDrive = true;
                    }

                    // Query health directly
                    _healthEngine.QueryAndPopulateHealth(drive);

                    drives.Add(drive);
                }
            }

            // Fallback/enrich via WMI if Win32 list was empty
            if (drives.Count == 0)
            {
                drives = GetDrivesViaWmi(systemDiskNumber, driveVolumesMap);
            }

            return drives;
        }

        public bool ValidateDriveStillMatches(PhysicalDriveInfo targetDrive)
        {
            if (targetDrive == null) return false;

            string path = $@"\\.\PhysicalDrive{targetDrive.DiskNumber}";
            using SafeFileHandle hDisk = Win32Storage.CreateFile(
                path,
                0,
                Win32Storage.FILE_SHARE_READ | Win32Storage.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Storage.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDisk.IsInvalid) return false;

            var current = QueryDriveDescriptor(hDisk, targetDrive.DiskNumber);
            if (current == null) return false;

            // Re-validate that disk number still belongs to the exact same serial number and capacity
            if (!string.IsNullOrEmpty(targetDrive.SerialNumber) && !string.IsNullOrEmpty(current.SerialNumber))
            {
                if (!string.Equals(targetDrive.SerialNumber.Trim(), current.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Allow +/- 5% size difference for partition margin
            if (targetDrive.SizeBytes > 0 && current.SizeBytes > 0)
            {
                double diff = Math.Abs((double)targetDrive.SizeBytes - (double)current.SizeBytes);
                if (diff / (double)targetDrive.SizeBytes > 0.05)
                    return false;
            }

            return true;
        }

        private PhysicalDriveInfo? QueryDriveDescriptor(SafeFileHandle hDisk, uint diskNum)
        {
            var drive = new PhysicalDriveInfo
            {
                DiskNumber = diskNum,
                DevicePath = $@"\\.\PhysicalDrive{diskNum}"
            };

            // Query Geometry / Size
            int geomSize = Marshal.SizeOf(typeof(Win32Storage.DISK_GEOMETRY_EX));
            IntPtr pGeom = Marshal.AllocHGlobal(geomSize);
            uint bytesReturned = 0;
            if (Win32Storage.DeviceIoControl(hDisk, Win32Storage.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, IntPtr.Zero, 0, pGeom, (uint)geomSize, out bytesReturned, IntPtr.Zero))
            {
                var geom = Marshal.PtrToStructure<Win32Storage.DISK_GEOMETRY_EX>(pGeom);
                drive.SizeBytes = (ulong)geom.DiskSize;
            }
            Marshal.FreeHGlobal(pGeom);

            // Query STORAGE_DEVICE_DESCRIPTOR
            var query = new Win32Storage.STORAGE_PROPERTY_QUERY
            {
                PropertyId = Win32Storage.StorageDeviceProperty,
                QueryType = Win32Storage.PropertyStandardQuery,
                AdditionalParameters = new byte[1]
            };

            int querySize = Marshal.SizeOf(query);
            IntPtr pQuery = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(query, pQuery, false);

            int outSize = 2048;
            IntPtr pOut = Marshal.AllocHGlobal(outSize);

            bool success = Win32Storage.DeviceIoControl(hDisk, Win32Storage.IOCTL_STORAGE_QUERY_PROPERTY, pQuery, (uint)querySize, pOut, (uint)outSize, out bytesReturned, IntPtr.Zero);
            if (success)
            {
                var desc = Marshal.PtrToStructure<Win32Storage.STORAGE_DEVICE_DESCRIPTOR>(pOut);
                drive.IsRemovable = desc.RemovableMedia != 0;

                string vendor = string.Empty;
                string product = string.Empty;
                string revision = string.Empty;
                string serial = string.Empty;

                if (desc.VendorIdOffset > 0 && desc.VendorIdOffset < bytesReturned)
                    vendor = Marshal.PtrToStringAnsi(new IntPtr(pOut.ToInt64() + desc.VendorIdOffset))?.Trim() ?? "";
                if (desc.ProductIdOffset > 0 && desc.ProductIdOffset < bytesReturned)
                    product = Marshal.PtrToStringAnsi(new IntPtr(pOut.ToInt64() + desc.ProductIdOffset))?.Trim() ?? "";
                if (desc.ProductRevisionOffset > 0 && desc.ProductRevisionOffset < bytesReturned)
                    revision = Marshal.PtrToStringAnsi(new IntPtr(pOut.ToInt64() + desc.ProductRevisionOffset))?.Trim() ?? "";
                if (desc.SerialNumberOffset > 0 && desc.SerialNumberOffset < bytesReturned)
                    serial = Marshal.PtrToStringAnsi(new IntPtr(pOut.ToInt64() + desc.SerialNumberOffset))?.Trim() ?? "";

                drive.Manufacturer = vendor;
                drive.Model = string.IsNullOrWhiteSpace(vendor) ? product : $"{vendor} {product}".Trim();
                if (string.IsNullOrWhiteSpace(drive.Model)) drive.Model = $"Physical Drive {diskNum}";
                drive.FirmwareRevision = revision;
                drive.SerialNumber = serial;

                // Bus Type mapping
                // 3=Atapi, 7=Usb, 11=Sata, 17=Nvme
                switch (desc.BusType)
                {
                    case 7: // BusTypeUsb
                        drive.BusType = DriveBusType.Usb;
                        // Probe whether it's SATA or NVMe behind USB
                        if (drive.Model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                            drive.InterfaceDisplay = "USB 3.2 → NVMe";
                        else
                            drive.InterfaceDisplay = "USB 3.0 → SATA";
                        drive.Protocol = "USB Mass Storage / UASP";
                        drive.UsbBridgeInfo = "USB Bridge Enclosure";
                        break;
                    case 17: // BusTypeNvme
                        drive.BusType = DriveBusType.Nvme;
                        drive.InterfaceDisplay = "PCIe NVMe";
                        drive.Protocol = "NVMe Express";
                        break;
                    case 11: // BusTypeSata
                        drive.BusType = DriveBusType.Sata;
                        drive.InterfaceDisplay = "SATA III 6Gb/s";
                        drive.Protocol = "Serial ATA";
                        break;
                    default:
                        drive.BusType = DriveBusType.Unknown;
                        drive.InterfaceDisplay = "Standard Interface";
                        drive.Protocol = "SCSI / Block Device";
                        break;
                }
            }
            Marshal.FreeHGlobal(pQuery);
            Marshal.FreeHGlobal(pOut);

            // Query Seek Penalty (SSD vs HDD)
            var seekQuery = new Win32Storage.STORAGE_PROPERTY_QUERY
            {
                PropertyId = Win32Storage.StorageDeviceSeekPenaltyProperty,
                QueryType = Win32Storage.PropertyStandardQuery,
                AdditionalParameters = new byte[1]
            };
            IntPtr pSeekQuery = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(seekQuery, pSeekQuery, false);
            int seekDescSize = Marshal.SizeOf(typeof(Win32Storage.DEVICE_SEEK_PENALTY_DESCRIPTOR));
            IntPtr pSeekDesc = Marshal.AllocHGlobal(seekDescSize);

            if (Win32Storage.DeviceIoControl(hDisk, Win32Storage.IOCTL_STORAGE_QUERY_PROPERTY, pSeekQuery, (uint)querySize, pSeekDesc, (uint)seekDescSize, out bytesReturned, IntPtr.Zero))
            {
                var seekDesc = Marshal.PtrToStructure<Win32Storage.DEVICE_SEEK_PENALTY_DESCRIPTOR>(pSeekDesc);
                drive.MediaType = seekDesc.IncursSeekPenalty ? MediaType.HDD : MediaType.SSD;
            }
            Marshal.FreeHGlobal(pSeekQuery);
            Marshal.FreeHGlobal(pSeekDesc);

            return drive;
        }

        private int DetectSystemDiskNumber()
        {
            try
            {
                // Query which disk has the system partition
                using var searcher = new ManagementObjectSearcher(
                    "SELECT DiskIndex FROM Win32_DiskPartition WHERE IsDisk=TRUE");
                // Or query logical disk C:
                using var partSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{_systemDriveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                foreach (ManagementObject part in partSearcher.Get())
                {
                    var diskIndex = part["DiskIndex"];
                    if (diskIndex != null)
                        return Convert.ToInt32(diskIndex);
                }
            }
            catch
            {
                // Default to Disk 0 if query fails
            }
            return 0;
        }

        private Dictionary<uint, List<string>> GetDiskToVolumesMapping()
        {
            var map = new Dictionary<uint, List<string>>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskPartition");
                foreach (ManagementObject partition in searcher.Get())
                {
                    uint diskIndex = Convert.ToUInt32(partition["DiskIndex"]);
                    string deviceId = partition["DeviceID"]?.ToString() ?? "";

                    using var logSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{deviceId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                    foreach (ManagementObject logDisk in logSearcher.Get())
                    {
                        string driveLetter = logDisk["DeviceID"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(driveLetter))
                        {
                            if (!map.ContainsKey(diskIndex))
                                map[diskIndex] = new List<string>();
                            if (!map[diskIndex].Contains(driveLetter))
                                map[diskIndex].Add(driveLetter);
                        }
                    }
                }
            }
            catch
            {
                // Ignore WMI mapping errors
            }
            return map;
        }

        private List<PhysicalDriveInfo> GetDrivesViaWmi(int systemDisk, Dictionary<uint, List<string>> volsMap)
        {
            var list = new List<PhysicalDriveInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject d in searcher.Get())
                {
                    uint index = Convert.ToUInt32(d["Index"]);
                    ulong size = Convert.ToUInt64(d["Size"] ?? 0);
                    string model = d["Model"]?.ToString() ?? $"Disk {index}";
                    string serial = d["SerialNumber"]?.ToString()?.Trim() ?? "";
                    string iface = d["InterfaceType"]?.ToString() ?? "Unknown";

                    var drive = new PhysicalDriveInfo
                    {
                        DiskNumber = index,
                        DevicePath = $@"\\.\PhysicalDrive{index}",
                        Model = model,
                        SerialNumber = serial,
                        SizeBytes = size,
                        IsSystemDrive = ((int)index == systemDisk),
                        InterfaceDisplay = iface.Contains("USB") ? "USB Enclosure" : iface,
                        BusType = iface.Contains("USB") ? DriveBusType.Usb : DriveBusType.Nvme
                    };

                    if (volsMap.TryGetValue(index, out var vols))
                        drive.Volumes = vols;

                    _healthEngine.QueryAndPopulateHealth(drive);
                    list.Add(drive);
                }
            }
            catch
            {
                // Fallback
            }
            return list;
        }
    }
}
