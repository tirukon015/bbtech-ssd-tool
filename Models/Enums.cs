namespace BBTechSSDTool.Models
{
    public enum HealthStatus
    {
        Good,
        Warning,
        Critical,
        Failed,
        Unavailable,
        Unknown
    }

    public enum DriveBusType
    {
        Nvme,
        Sata,
        Usb,
        Scsi,
        Atapi,
        Raid,
        Unknown
    }

    public enum PartitionScheme
    {
        GPT,
        MBR
    }

    public enum TargetSystem
    {
        UEFI,
        BIOS,
        UefiAndBios
    }

    public enum FileSystemType
    {
        NTFS,
        FAT32,
        exFAT
    }

    public enum MediaType
    {
        SSD,
        HDD,
        Removable,
        Unknown
    }
}
