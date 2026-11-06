using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BBTechSSDTool.Models
{
    public class PhysicalDriveInfo
    {
        public uint DiskNumber { get; set; }
        public string DevicePath { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string FirmwareRevision { get; set; } = string.Empty;
        public ulong SizeBytes { get; set; }
        public string FormattedCapacity => FormatBytes(SizeBytes);

        public DriveBusType BusType { get; set; } = DriveBusType.Unknown;
        public string InterfaceDisplay { get; set; } = "Unknown";
        public string Protocol { get; set; } = "Unknown";
        public string UsbBridgeInfo { get; set; } = string.Empty;
        public MediaType MediaType { get; set; } = MediaType.SSD;
        public bool IsRemovable { get; set; }
        public bool IsSystemDrive { get; set; }
        public bool IsReadOnly { get; set; }

        public string PartitionStyle { get; set; } = "GPT";
        public List<string> Volumes { get; set; } = new();
        public string VolumesDisplay => Volumes.Count > 0 ? string.Join(", ", Volumes) : "No assigned drive letters";

        // Health Information (Directly displayed on Dashboard)
        public HealthStatus HealthStatus { get; set; } = HealthStatus.Unknown;
        public int? HealthPercentage { get; set; }
        public int? TemperatureCelsius { get; set; }
        public ulong? PowerOnHours { get; set; }
        public ulong? PowerCycles { get; set; }
        public string TotalHostWrites { get; set; } = "--";
        public string TotalHostReads { get; set; } = "--";
        public int? AvailableSpare { get; set; }
        public int? PercentageUsed { get; set; }
        public string CriticalWarningsSummary { get; set; } = "None";
        public ulong? MediaErrors { get; set; }
        public ulong? UncorrectableErrors { get; set; }

        public bool HasSmartData { get; set; }
        public string SmartUnavailableReason { get; set; } = string.Empty;

        // Detailed Health Collections
        public ObservableCollection<SmartAttribute> SmartAttributes { get; set; } = new();
        public NvmeHealthDetails? NvmeDetails { get; set; }

        // UI Presentation Helpers
        public string HealthStatusText => HealthStatus switch
        {
            HealthStatus.Good => "GOOD",
            HealthStatus.Warning => "WARNING",
            HealthStatus.Critical => "CRITICAL",
            HealthStatus.Failed => "FAILED",
            HealthStatus.Unavailable => "UNAVAILABLE",
            _ => "UNKNOWN"
        };

        public string HealthScoreDisplay => HealthPercentage.HasValue ? $"{HealthPercentage.Value}%" : "--";
        public string TemperatureDisplay => TemperatureCelsius.HasValue ? $"{TemperatureCelsius.Value}°C" : "--";
        public string PowerOnHoursDisplay => PowerOnHours.HasValue ? $"{PowerOnHours.Value:N0} hours" : "--";
        public string PowerCyclesDisplay => PowerCycles.HasValue ? $"{PowerCycles.Value:N0}" : "--";

        public static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "0 B";
            double d = bytes;
            // Display decimal gigabytes/terabytes like drive manufacturers (1000^x) or 1024
            if (d >= 1_000_000_000_000)
                return $"{(d / 1_000_000_000_000):F1} TB";
            if (d >= 1_000_000_000)
                return $"{(d / 1_000_000_000):F0} GB";
            if (d >= 1_000_000)
                return $"{(d / 1_000_000):F0} MB";
            return $"{bytes:N0} B";
        }
    }
}
