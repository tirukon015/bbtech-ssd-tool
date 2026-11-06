using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using BBTechSSDTool.Models;
using BBTechSSDTool.Services;
using Microsoft.Win32;

namespace BBTechSSDTool.ViewModels
{
    public class ClusterSizeOption
    {
        public uint Value { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class PartitionSchemeOption
    {
        public PartitionScheme Scheme { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class TargetSystemOption
    {
        public TargetSystem Target { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class FileSystemOption
    {
        public FileSystemType Type { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class BootSelectionOption
    {
        public BootSelectionMode Mode { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class ImageOptionItem
    {
        public WindowsImageOption Option { get; set; }
        public string Display { get; set; } = string.Empty;
    }

    public class FormatViewModel : ViewModelBase
    {
        private readonly FormattingService _formattingService;
        private readonly Action _onBackToDashboard;
        private CancellationTokenSource? _cts;
        private readonly DispatcherTimer _elapsedTimer;
        private DateTime _formatStartTime;

        private PhysicalDriveInfo _drive;
        public PhysicalDriveInfo Drive
        {
            get => _drive;
            set => SetProperty(ref _drive, value);
        }

        #region Rufus Boot Selection & ISO
        public ObservableCollection<BootSelectionOption> BootSelections { get; } = new()
        {
            new BootSelectionOption { Mode = BootSelectionMode.NonBootable, Display = "Non-bootable (Standard Storage)" },
            new BootSelectionOption { Mode = BootSelectionMode.FreeDos, Display = "FreeDOS (Bootable DOS Environment)" },
            new BootSelectionOption { Mode = BootSelectionMode.DiskOrIsoImage, Display = "Disk or ISO image (Windows / Linux Installation)" }
        };

        private BootSelectionOption _selectedBootSelection;
        public BootSelectionOption SelectedBootSelection
        {
            get => _selectedBootSelection;
            set
            {
                if (SetProperty(ref _selectedBootSelection, value))
                {
                    OnPropertyChanged(nameof(IsIsoSelectionVisible));
                }
            }
        }

        public bool IsIsoSelectionVisible => SelectedBootSelection?.Mode == BootSelectionMode.DiskOrIsoImage;

        private string? _selectedIsoPath;
        public string? SelectedIsoPath
        {
            get => _selectedIsoPath;
            set
            {
                if (SetProperty(ref _selectedIsoPath, value))
                {
                    OnPropertyChanged(nameof(HasIsoSelected));
                    OnPropertyChanged(nameof(IsoDisplayName));
                }
            }
        }

        private string? _selectedIsoSize;
        public string? SelectedIsoSize
        {
            get => _selectedIsoSize;
            set => SetProperty(ref _selectedIsoSize, value);
        }

        public bool HasIsoSelected => !string.IsNullOrEmpty(SelectedIsoPath) && File.Exists(SelectedIsoPath);
        public string IsoDisplayName => HasIsoSelected ? $"{Path.GetFileName(SelectedIsoPath)} ({SelectedIsoSize})" : "No ISO selected — click SELECT to browse";

        public ObservableCollection<ImageOptionItem> ImageOptions { get; } = new()
        {
            new ImageOptionItem { Option = WindowsImageOption.StandardInstallation, Display = "Standard Windows installation" },
            new ImageOptionItem { Option = WindowsImageOption.WindowsToGo, Display = "Windows To Go (Portable OS on SSD)" }
        };

        private ImageOptionItem _selectedImageOption;
        public ImageOptionItem SelectedImageOption
        {
            get => _selectedImageOption;
            set => SetProperty(ref _selectedImageOption, value);
        }
        #endregion

        #region Partition Scheme & Target System
        public ObservableCollection<PartitionSchemeOption> PartitionSchemes { get; } = new()
        {
            new PartitionSchemeOption { Scheme = PartitionScheme.GPT, Display = "GPT (GUID Partition Table — UEFI)" },
            new PartitionSchemeOption { Scheme = PartitionScheme.MBR, Display = "MBR (Master Boot Record — BIOS or UEFI-CSM)" }
        };

        private PartitionSchemeOption _selectedPartitionSchemeOption;
        public PartitionSchemeOption SelectedPartitionSchemeOption
        {
            get => _selectedPartitionSchemeOption;
            set
            {
                if (SetProperty(ref _selectedPartitionSchemeOption, value) && value != null)
                {
                    // Auto-align Target System like Rufus
                    if (value.Scheme == PartitionScheme.GPT)
                        SelectedTargetSystemOption = TargetSystems.FirstOrDefault(t => t.Target == TargetSystem.UEFI) ?? TargetSystems[0];
                    else
                        SelectedTargetSystemOption = TargetSystems.FirstOrDefault(t => t.Target == TargetSystem.BIOS) ?? TargetSystems[0];
                }
            }
        }

        public ObservableCollection<TargetSystemOption> TargetSystems { get; } = new()
        {
            new TargetSystemOption { Target = TargetSystem.UEFI, Display = "UEFI (non CSM)" },
            new TargetSystemOption { Target = TargetSystem.BIOS, Display = "BIOS (or UEFI-CSM)" },
            new TargetSystemOption { Target = TargetSystem.UefiAndBios, Display = "UEFI + BIOS (Hybrid)" }
        };

        private TargetSystemOption _selectedTargetSystemOption;
        public TargetSystemOption SelectedTargetSystemOption
        {
            get => _selectedTargetSystemOption;
            set => SetProperty(ref _selectedTargetSystemOption, value);
        }
        #endregion

        #region File System & Format Options
        public ObservableCollection<FileSystemOption> FileSystems { get; } = new()
        {
            new FileSystemOption { Type = FileSystemType.NTFS, Display = "NTFS (Default Windows)" },
            new FileSystemOption { Type = FileSystemType.FAT32, Display = "FAT32 (Maximum Compatibility)" },
            new FileSystemOption { Type = FileSystemType.exFAT, Display = "exFAT (Large Files & Mac/PC)" }
        };

        private FileSystemOption _selectedFileSystemOption;
        public FileSystemOption SelectedFileSystemOption
        {
            get => _selectedFileSystemOption;
            set => SetProperty(ref _selectedFileSystemOption, value);
        }

        public ObservableCollection<ClusterSizeOption> ClusterSizes { get; } = new()
        {
            new ClusterSizeOption { Value = 4096, Display = "4096 bytes (Default)" },
            new ClusterSizeOption { Value = 8192, Display = "8192 bytes" },
            new ClusterSizeOption { Value = 16384, Display = "16 kilobytes" },
            new ClusterSizeOption { Value = 32768, Display = "32 kilobytes" },
            new ClusterSizeOption { Value = 65536, Display = "64 kilobytes" }
        };

        private ClusterSizeOption _selectedClusterSizeOption;
        public ClusterSizeOption SelectedClusterSizeOption
        {
            get => _selectedClusterSizeOption;
            set => SetProperty(ref _selectedClusterSizeOption, value);
        }

        private string _volumeLabel = "BBTECH-SSD";
        public string VolumeLabel
        {
            get => _volumeLabel;
            set => SetProperty(ref _volumeLabel, value);
        }

        private bool _isQuickFormat = true;
        public bool IsQuickFormat
        {
            get => _isQuickFormat;
            set => SetProperty(ref _isQuickFormat, value);
        }

        private bool _createExtendedLabelAndIcon = false;
        public bool CreateExtendedLabelAndIcon
        {
            get => _createExtendedLabelAndIcon;
            set => SetProperty(ref _createExtendedLabelAndIcon, value);
        }

        private bool _isBadBlockCheck = false;
        public bool IsBadBlockCheck
        {
            get => _isBadBlockCheck;
            set
            {
                if (SetProperty(ref _isBadBlockCheck, value))
                {
                    OnPropertyChanged(nameof(IsBadBlockPassVisible));
                }
            }
        }

        public bool IsBadBlockPassVisible => IsBadBlockCheck;

        public ObservableCollection<int> BadBlockPassOptions { get; } = new() { 1, 2 };
        private int _selectedBadBlockPass = 1;
        public int SelectedBadBlockPass
        {
            get => _selectedBadBlockPass;
            set => SetProperty(ref _selectedBadBlockPass, value);
        }

        // Advanced Drive Properties Toggle
        private bool _showAdvancedProperties = false;
        public bool ShowAdvancedProperties
        {
            get => _showAdvancedProperties;
            set => SetProperty(ref _showAdvancedProperties, value);
        }

        private bool _listUsbHardDrives = true;
        public bool ListUsbHardDrives
        {
            get => _listUsbHardDrives;
            set => SetProperty(ref _listUsbHardDrives, value);
        }

        private bool _addOldBiosFixes = false;
        public bool AddOldBiosFixes
        {
            get => _addOldBiosFixes;
            set => SetProperty(ref _addOldBiosFixes, value);
        }

        private bool _useRufusMbr = false;
        public bool UseRufusMbr
        {
            get => _useRufusMbr;
            set => SetProperty(ref _useRufusMbr, value);
        }
        #endregion

        #region Progress & Execution
        private bool _isFormatting;
        public bool IsFormatting
        {
            get => _isFormatting;
            set => SetProperty(ref _isFormatting, value);
        }

        private int _progressPercentage;
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        private string _statusMessage = "Ready to format";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _elapsedTimeDisplay = "00:00:00";
        public string ElapsedTimeDisplay
        {
            get => _elapsedTimeDisplay;
            set => SetProperty(ref _elapsedTimeDisplay, value);
        }

        private readonly StringBuilder _logBuilder = new();
        private string _activityLog = "";
        public string ActivityLog
        {
            get => _activityLog;
            set => SetProperty(ref _activityLog, value);
        }

        // Confirmation Modal Properties (One-Click Format)
        private bool _isConfirmationDialogOpen;
        public bool IsConfirmationDialogOpen
        {
            get => _isConfirmationDialogOpen;
            set => SetProperty(ref _isConfirmationDialogOpen, value);
        }

        private bool _showLogWindow = false;
        public bool ShowLogWindow
        {
            get => _showLogWindow;
            set => SetProperty(ref _showLogWindow, value);
        }
        #endregion

        #region Commands
        public ICommand SelectIsoCommand { get; }
        public ICommand RequestFormatCommand { get; }
        public ICommand ConfirmFormatCommand { get; }
        public ICommand CancelConfirmationCommand { get; }
        public ICommand AbortFormatCommand { get; }
        public ICommand ToggleLogCommand { get; }
        public ICommand BackCommand { get; }
        #endregion

        public FormatViewModel(PhysicalDriveInfo drive, FormattingService formattingService, Action onBackToDashboard)
        {
            _drive = drive;
            _formattingService = formattingService;
            _onBackToDashboard = onBackToDashboard;

            _selectedBootSelection = BootSelections[0];
            _selectedImageOption = ImageOptions[0];
            _selectedPartitionSchemeOption = PartitionSchemes[0];
            _selectedTargetSystemOption = TargetSystems[0];
            _selectedFileSystemOption = FileSystems[0];
            _selectedClusterSizeOption = ClusterSizes[0];

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (s, e) =>
            {
                var span = DateTime.Now - _formatStartTime;
                ElapsedTimeDisplay = span.ToString(@"hh\:mm\:ss");
            };

            SelectIsoCommand = new RelayCommand(OnSelectIso, () => !IsFormatting);
            RequestFormatCommand = new RelayCommand(OnRequestFormat, () => !IsFormatting && !Drive.IsSystemDrive);
            ConfirmFormatCommand = new RelayCommand(async () => await ExecuteFormatAsync(), () => !IsFormatting);
            CancelConfirmationCommand = new RelayCommand(() => IsConfirmationDialogOpen = false);
            AbortFormatCommand = new RelayCommand(OnAbortFormat, () => IsFormatting);
            ToggleLogCommand = new RelayCommand(() => ShowLogWindow = !ShowLogWindow);
            BackCommand = new RelayCommand(OnBack, () => !IsFormatting);

            AppendLog($"Rufus-compatible engine ready for {drive.Model} (Disk {drive.DiskNumber}, {drive.FormattedCapacity}).");
        }

        private void OnSelectIso()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select Bootable Disk Image or ISO",
                Filter = "Disk or ISO Images (*.iso;*.img;*.vhd)|*.iso;*.img;*.vhd|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                SelectedIsoPath = ofd.FileName;
                var fi = new FileInfo(ofd.FileName);
                double gb = fi.Length / (1024.0 * 1024.0 * 1024.0);
                SelectedIsoSize = gb >= 1.0 ? $"{gb:F1} GB" : $"{(fi.Length / (1024.0 * 1024.0)):F0} MB";

                // Suggest label based on ISO name
                string rawName = Path.GetFileNameWithoutExtension(ofd.FileName);
                VolumeLabel = new string(rawName.Take(11).ToArray()).ToUpperInvariant();

                AppendLog($"Loaded disk image: {ofd.FileName} [{SelectedIsoSize}].");
            }
        }

        private void OnRequestFormat()
        {
            // Open the One-Click confirmation dialog directly
            IsConfirmationDialogOpen = true;
        }

        private async Task ExecuteFormatAsync()
        {
            IsConfirmationDialogOpen = false;
            IsFormatting = true;
            ProgressPercentage = 0;
            StatusMessage = "Starting drive preparation...";
            _formatStartTime = DateTime.Now;
            _elapsedTimer.Start();

            _cts = new CancellationTokenSource();
            _logBuilder.Clear();
            AppendLog($"Starting Rufus-compatible preparation on PhysicalDrive{Drive.DiskNumber} ({Drive.Model})...");

            var parameters = new FormatParameters
            {
                DiskNumber = Drive.DiskNumber,
                TargetDriveModel = Drive.Model,
                TargetSerialNumber = Drive.SerialNumber,
                BootSelection = SelectedBootSelection.Mode,
                IsoPath = SelectedIsoPath,
                IsoFileName = HasIsoSelected ? Path.GetFileName(SelectedIsoPath) : null,
                IsoFileSize = SelectedIsoSize,
                ImageOption = SelectedImageOption.Option,
                PartitionScheme = SelectedPartitionSchemeOption.Scheme,
                TargetSystem = SelectedTargetSystemOption.Target,
                FileSystem = SelectedFileSystemOption.Type,
                ClusterSize = SelectedClusterSizeOption.Value,
                VolumeLabel = VolumeLabel,
                QuickFormat = IsQuickFormat,
                CreateExtendedLabelAndIcon = CreateExtendedLabelAndIcon,
                CheckBadBlocks = IsBadBlockCheck,
                BadBlockPasses = SelectedBadBlockPass,
                ListUsbHardDrives = ListUsbHardDrives,
                AddOldBiosFixes = AddOldBiosFixes,
                UseRufusMbrWithBiosId = UseRufusMbr
            };

            var progress = new Progress<(int percent, string message)>(report =>
            {
                ProgressPercentage = report.percent;
                StatusMessage = report.message;
                AppendLog(report.message);
            });

            bool success = await _formattingService.FormatDriveAsync(Drive, parameters, progress, _cts.Token);

            _elapsedTimer.Stop();
            IsFormatting = false;
            if (success)
            {
                StatusMessage = "Format completed successfully! Ready for use.";
                AppendLog("Ready.");
            }
            else
            {
                AppendLog("Format aborted or encountered an error.");
            }
        }

        private void OnAbortFormat()
        {
            _cts?.Cancel();
            AppendLog("Cancellation requested by technician...");
        }

        private void OnBack()
        {
            _onBackToDashboard?.Invoke();
        }

        private void AppendLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logBuilder.AppendLine(line);
            ActivityLog = _logBuilder.ToString();
        }
    }
}
