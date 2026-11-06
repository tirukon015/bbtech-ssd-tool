using System.Collections.Generic;

namespace BBTechSSDTool.Models
{
    public class NvmeHealthDetails
    {
        public byte CriticalWarning { get; set; }
        public List<string> CriticalWarningFlags { get; set; } = new();
        public int CompositeTemperatureCelsius { get; set; }
        public byte AvailableSparePercent { get; set; }
        public byte AvailableSpareThresholdPercent { get; set; }
        public byte PercentageUsed { get; set; }
        public ulong DataUnitsRead { get; set; }
        public ulong DataUnitsWritten { get; set; }
        public ulong HostReadCommands { get; set; }
        public ulong HostWriteCommands { get; set; }
        public ulong ControllerBusyTimeMinutes { get; set; }
        public ulong PowerCycles { get; set; }
        public ulong PowerOnHours { get; set; }
        public ulong UnsafeShutdowns { get; set; }
        public ulong MediaErrors { get; set; }
        public ulong NumErrLogEntries { get; set; }

        public string TotalWrittenFormatted => FormatDataUnits(DataUnitsWritten);
        public string TotalReadFormatted => FormatDataUnits(DataUnitsRead);

        public static string FormatDataUnits(ulong dataUnits)
        {
            // NVMe spec: each unit represents 1,000 sectors of 512 bytes = 512,000 bytes
            double bytes = (double)dataUnits * 512000.0;
            if (bytes >= 1e12)
                return $"{(bytes / 1e12):F1} TB";
            if (bytes >= 1e9)
                return $"{(bytes / 1e9):F1} GB";
            if (bytes >= 1e6)
                return $"{(bytes / 1e6):F1} MB";
            return $"{bytes:N0} B";
        }
    }
}
