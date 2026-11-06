namespace BBTechSSDTool.Models
{
    public class SmartAttribute
    {
        public byte Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Worst { get; set; }
        public int Threshold { get; set; }
        public ulong RawValue { get; set; }
        public string RawDisplay { get; set; } = string.Empty;
        public string Status { get; set; } = "PASS";
        public bool IsCritical { get; set; }
        public string Description { get; set; } = string.Empty;

        public static string GetAttributeName(byte id)
        {
            return id switch
            {
                0x01 => "Read Error Rate",
                0x05 => "Reallocated Sectors Count",
                0x09 => "Power-On Hours",
                0x0C => "Power Cycle Count",
                0xAA => "Available Reserved Space",
                0xAB => "Program Fail Count",
                0xAC => "Erase Fail Count",
                0xAE => "Unexpected Power Loss Count",
                0xB4 => "Unused Reserved Block Count Total",
                0xB7 => "SATA Downshift Error Count",
                0xB8 => "End-to-End Error",
                0xBB => "Reported Uncorrectable Errors",
                0xBC => "Command Timeout",
                0xBD => "High Fly Writes",
                0xBE => "Airflow Temperature",
                0xC2 => "Temperature",
                0xC4 => "Reallocation Event Count",
                0xC5 => "Current Pending Sector Count",
                0xC6 => "Offline Uncorrectable Sector Count",
                0xC7 => "UltraDMA CRC Error Count",
                0xE7 => "SSD Life Remaining / Wear Leveling",
                0xE8 => "Available Reserved Space",
                0xE9 => "Media Wearout Indicator",
                0xEA => "Average Erase Count",
                0xF1 => "Total Host Writes (LBAs)",
                0xF2 => "Total Host Reads (LBAs)",
                _ => $"Vendor Specific (0x{id:X2})"
            };
        }
    }
}
