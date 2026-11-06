# ⚡ BBTech SSD Tool

<div align="center">

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-blue?style=for-the-badge&logo=windows)
![Framework](https://img.shields.io/badge/Framework-.NET%208.0%20WPF-512BD4?style=for-the-badge&logo=dotnet)
![Architecture](https://img.shields.io/badge/Deployment-100%25%20Self--Contained%20Single%20EXE-success?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-orange?style=for-the-badge)
![Status](https://img.shields.io/badge/Edition-Technician%20v1.0.0-00A3FF?style=for-the-badge)

**The all-in-one storage diagnostics, automated health monitoring, and advanced formatting suite for computer technicians, system builders, and hardware enthusiasts.**

</div>

---

## 📖 Overview

**BBTech SSD Tool** is a lightweight, high-performance Windows desktop application designed to inspect, monitor, diagnose, and format physical storage drives. Built from the ground up to replace heavy, slow, or internet-dependent utilities, BBTech SSD Tool is **100% offline and self-contained**—packaged as a single portable `.exe` that runs immediately on any Windows machine with **zero setup and zero runtime installation**.

Whether diagnosing failing NVMe SSDs in external USB enclosures, preparing bootable Windows To Go or FreeDOS drives, or performing rapid single-click partition wipeouts, BBTech SSD Tool delivers professional-grade storage engineering in an intuitive, high-contrast dark interface.

---

## ✨ Key Features

### 1. 🛡 7-Tier Universal Health & SMART Telemetry Engine
Never displays "UNAVAILABLE"—BBTech SSD Tool utilizes a comprehensive 7-tier diagnostic fallback pipeline to extract accurate endurance, temperature, and health telemetry from any storage device:
1. **Tier 1 — HD Sentinel Portable Engine**: Direct parsing of local HD Sentinel diagnostic telemetry (`.hdd` / `.sta`).
2. **Tier 2 — Native NVMe Protocol IOCTL**: Low-level `IOCTL_STORAGE_QUERY_PROPERTY` querying NVMe Log Page 0x02 for percentage endurance used, host reads/writes, unsafe shutdowns, media error counts, and critical warning flags.
3. **Tier 3 — Direct ATA SMART**: Direct IDE/SATA command pass-through via `SMART_RCV_DRIVE_DATA`.
4. **Tier 4 — SCSI SAT-16 Passthrough**: SCSI-to-ATA Translation (SAT) standard commands to penetrate USB enclosure bridge chips (Realtek, ASMedia, JMicron) that conventionally block SMART queries.
5. **Tier 5 — Windows Storage Reliability Counter**: WMI querying `MSFT_StorageReliabilityCounter` for hardware wear indicators and temperature.
6. **Tier 6 — Windows MSFT_PhysicalDisk Verification**: Storage subsystem driver status verification (functional even under standard non-admin privileges).
7. **Tier 7 — Automated Verified Storage Fallback**: Validates controller operational status, media readiness, and partition geometry.

### 2. 🔌 Real-Time Plug-and-Play Hardware Discovery
- Integrated Win32 `WM_DEVICECHANGE` notification hooks (`RegisterDeviceNotification` for disk interface `GUID_DEVINTERFACE_DISK` and volume interface `GUID_DEVINTERFACE_VOLUME`).
- Active 2.5-second topology change watcher.
- **Zero manual refresh required**: Plug in or disconnect any USB SSD, external hard drive, or thumb drive, and the dashboard updates instantaneously in real time—just like HD Sentinel Pro.

### 3. 🚀 Rufus-Grade Advanced Formatting Page
- Dedicated Rufus formatting screen accessible via **`[ ⚙ RUFUS FORMAT ]`**:
  - **Boot Selection**: Non-bootable, FreeDOS, or Disk/ISO Image (with interactive ISO file selection).
  - **Image Options**: Standard Windows Installation vs. **Windows To Go** live bootable system creation.
  - **Partition Schemes**: GPT (Modern UEFI) or MBR (Legacy BIOS / CSM).
  - **File Systems**: NTFS, FAT32, exFAT, Large FAT32.
  - **Cluster Allocation Sizes**: 512 B up to 64 KB.
  - **Advanced Formatting Tweaks**: Multi-pass bad block verification (1 to 4 passes), extended label and icon generation (`autorun.inf`), and BIOS ID compatibility fixes.

### 4. ⚡ 1-Click Quick Format Flow
- Formats non-system drives straight from the main dashboard drive card.
- Safe, instant one-click confirmation modal: simply click **`[ OK, FORMAT NOW ]`** (no tedious manual typing of "ERASE" required).
- Real-time elapsed time counter (`hh:mm:ss`), percentage progress bar, and status phase telemetry.
- **System Drive Protection**: Disk 0 / System OS partitions (`C:\`) are strictly locked and protected against accidental erasure.

### 5. ⚙ Gear Preferences Panel
- Accessible from the **`[ ⚙ PREFERENCES ]`** button in the header bar:
  - Toggle visibility of **`[ ⚡ QUICK FORMAT ]`** on dashboard drive cards.
  - Toggle visibility of **`[ ⚙ RUFUS FORMAT ]`** on dashboard drive cards.
  - Configure default partition scheme (GPT / MBR).
  - Configure default file system (NTFS / exFAT / FAT32).
  - Set default custom volume label (e.g. `BBTECH-SSD`).

### 6. 💻 Responsive Viewport & Dark High-Contrast Theme
- Handcrafted high-contrast dark theme with pure white text (`#FFFFFF`) on dark navy backgrounds (`#161E28`) and electric cyan accents (`#00A3FF`).
- ComboBox popups, dropdown items, text inputs, and checkboxes feature explicit templates to eliminate Win32 color-clashing bugs.
- Responsive, auto-scrolling container optimized for **13", 14", and 15" laptop screens** (including 1366x768 and 1080p scaled displays) up to ultra-wide 4K monitors.

---

## 🛠 System Architecture

```
BBTech SSD Tools/
├── App.xaml / App.xaml.cs          # High-contrast theme resources, elevation & crash telemetry
├── MainWindow.xaml / .cs           # Root window, header, status bar, Win32 WM_DEVICECHANGE hooks
├── Models/
│   ├── PhysicalDriveInfo.cs        # Comprehensive physical drive domain model & SMART state
│   ├── FormatParameters.cs         # Rufus boot, file system, partitioning, and ISO parameters
│   └── NvmeHealthDetails.cs        # Low-level NVMe health structures (log page 0x02)
├── ViewModels/
│   ├── MainViewModel.cs            # Primary coordinator: drive scanning, quick format, preferences
│   ├── FormatViewModel.cs          # Rufus formatting logic, ISO selection, background runner
│   ├── HealthDetailsViewModel.cs   # Deep SMART / NVMe attribute inspector & report exporter
│   ├── ViewModelBase.cs            # INotifyPropertyChanged helper
│   └── RelayCommand.cs             # ICommand implementation
├── Views/
│   ├── DashboardView.xaml          # Main drive cards list, health tiles, 1-click format dialog
│   ├── FormatView.xaml             # Authentic Rufus formatting interface
│   └── HealthDetailsView.xaml      # SMART attribute table and NVMe telemetry breakdown
├── Services/
│   ├── DriveDetectionService.cs    # WMI and Win32 disk discovery
│   ├── HealthEngineService.cs      # 7-tier diagnostic and SMART query engine
│   ├── FormattingService.cs        # DiskPart, VDS, Format-Volume, and ISO extraction engine
│   ├── HDSentinelDataService.cs    # Portable HD Sentinel telemetry parser
│   └── Native/
│       └── Win32Storage.cs         # P/Invoke kernel32 & setupapi storage IOCTL definitions
└── HDSentinel/                     # Portable diagnostic dependencies (offline self-contained)
```

---

## 🚀 Building from Source

### Prerequisites
- **Windows 10 / 11 (64-bit)**
- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** or Visual Studio 2022 (v17.8+)

### 1. Clone the Repository
```powershell
git clone https://github.com/tirukon015/BBTech-SSD-Tools.git
cd "BBTech-SSD-Tools"
```

### 2. Standard Debug / Release Build
```powershell
dotnet build -c Release
```

### 3. Publish as a 100% Self-Contained Single EXE
To produce a standalone `.exe` that requires **no .NET installation on client laptops**:
```powershell
dotnet publish -c Release -o "./dist"
```
The resulting executable will be located at:
```
dist/BBTechSSDTool.exe
```

---

## 🛡 Security & Permissions

- **Administrator Privileges**: Storage operations (such as raw IOCTL SMART queries, partition table re-initialization, and bad block testing) require elevated administrative privileges. 
- **In-App Elevation**: If launched without administrator rights, the app runs in non-admin mode using safe WMI queries and provides a dedicated **`[ ⚡ ELEVATE TO ADMIN ]`** button in the header to restart with elevated rights on demand.
- **System Drive Safeguards**: Drive 0 / Windows system partitions are automatically locked from destructive formatting operations.

---

## 📜 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

```
Copyright (c) 2026 Touhidul Islam Rukon (BBTech)
```
