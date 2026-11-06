namespace BBTechSSDTool.Models
{
    public enum BootSelectionMode
    {
        NonBootable,
        FreeDos,
        DiskOrIsoImage
    }

    public enum WindowsImageOption
    {
        StandardInstallation,
        WindowsToGo
    }

    public class FormatParameters
    {
        public uint DiskNumber { get; set; }
        public string TargetDriveModel { get; set; } = string.Empty;
        public string TargetSerialNumber { get; set; } = string.Empty;

        // Rufus Boot Selection
        public BootSelectionMode BootSelection { get; set; } = BootSelectionMode.NonBootable;
        public string? IsoPath { get; set; }
        public string? IsoFileName { get; set; }
        public string? IsoFileSize { get; set; }
        public WindowsImageOption ImageOption { get; set; } = WindowsImageOption.StandardInstallation;

        // Partition & Target Architecture
        public PartitionScheme PartitionScheme { get; set; } = PartitionScheme.GPT;
        public TargetSystem TargetSystem { get; set; } = TargetSystem.UEFI;

        // File System & Format Options
        public FileSystemType FileSystem { get; set; } = FileSystemType.NTFS;
        public uint ClusterSize { get; set; } = 4096; // 4096 bytes default
        public string VolumeLabel { get; set; } = "BBTECH-SSD";
        public bool QuickFormat { get; set; } = true;
        public bool CreateExtendedLabelAndIcon { get; set; } = false;
        public bool CheckBadBlocks { get; set; } = false;
        public int BadBlockPasses { get; set; } = 1;

        // Advanced Drive Properties
        public bool ListUsbHardDrives { get; set; } = false;
        public bool AddOldBiosFixes { get; set; } = false;
        public bool UseRufusMbrWithBiosId { get; set; } = false;
    }
}
