using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using BBTechSSDTool.Models;
using BBTechSSDTool.Services;

namespace BBTechSSDTool.ViewModels
{
    public enum AppView
    {
        Dashboard,
        Format,
        HealthDetails
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly DriveDetectionService _driveDetectionService;
        private readonly FormattingService _formattingService;
        private CancellationTokenSource? _quickFormatCts;
        private readonly DispatcherTimer _quickFormatTimer;
        private DateTime _quickFormatStartTime;

        public ObservableCollection<PhysicalDriveInfo> Drives { get; } = new();

        private PhysicalDriveInfo? _selectedDrive;
        public PhysicalDriveInfo? SelectedDrive
        {
            get => _selectedDrive;
            set => SetProperty(ref _selectedDrive, value);
        }

        private AppView _currentView = AppView.Dashboard;
        public AppView CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(IsDashboardView));
                    OnPropertyChanged(nameof(IsFormatView));
                    OnPropertyChanged(nameof(IsHealthDetailsView));
                }
            }
        }

        public bool IsDashboardView => CurrentView == AppView.Dashboard;
        public bool IsFormatView => CurrentView == AppView.Format;
        public bool IsHealthDetailsView => CurrentView == AppView.HealthDetails;

        private FormatViewModel? _formatViewModel;
        public FormatViewModel? FormatViewModel
        {
            get => _formatViewModel;
            set => SetProperty(ref _formatViewModel, value);
        }

        private HealthDetailsViewModel? _healthDetailsViewModel;
        public HealthDetailsViewModel? HealthDetailsViewModel
        {
            get => _healthDetailsViewModel;
            set => SetProperty(ref _healthDetailsViewModel, value);
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string AppTitle => "BBTech SSD Tool";
        public string AppVersion => "v1.0.0";
        public bool IsElevated => App.IsAdmin;

        #region Gear Settings (Preferences Modal)
        private bool _isSettingsDialogOpen;
        public bool IsSettingsDialogOpen
        {
            get => _isSettingsDialogOpen;
            set => SetProperty(ref _isSettingsDialogOpen, value);
        }

        private bool _showQuickFormatButton = true;
        public bool ShowQuickFormatButton
        {
            get => _showQuickFormatButton;
            set => SetProperty(ref _showQuickFormatButton, value);
        }

        private bool _showRufusFormatButton = true;
        public bool ShowRufusFormatButton
        {
            get => _showRufusFormatButton;
            set => SetProperty(ref _showRufusFormatButton, value);
        }

        private string _defaultQuickVolumeLabel = "BBTECH-SSD";
        public string DefaultQuickVolumeLabel
        {
            get => _defaultQuickVolumeLabel;
            set => SetProperty(ref _defaultQuickVolumeLabel, value);
        }

        private FileSystemType _defaultQuickFileSystem = FileSystemType.NTFS;
        public FileSystemType DefaultQuickFileSystem
        {
            get => _defaultQuickFileSystem;
            set => SetProperty(ref _defaultQuickFileSystem, value);
        }

        private PartitionScheme _defaultQuickPartitionScheme = PartitionScheme.GPT;
        public PartitionScheme DefaultQuickPartitionScheme
        {
            get => _defaultQuickPartitionScheme;
            set => SetProperty(ref _defaultQuickPartitionScheme, value);
        }
        #endregion

        #region Dashboard Quick 1-Click Format Flow
        private PhysicalDriveInfo? _quickFormatTargetDrive;
        public PhysicalDriveInfo? QuickFormatTargetDrive
        {
            get => _quickFormatTargetDrive;
            set => SetProperty(ref _quickFormatTargetDrive, value);
        }

        private bool _isQuickFormatConfirmationOpen;
        public bool IsQuickFormatConfirmationOpen
        {
            get => _isQuickFormatConfirmationOpen;
            set => SetProperty(ref _isQuickFormatConfirmationOpen, value);
        }

        private bool _isQuickFormatting;
        public bool IsQuickFormatting
        {
            get => _isQuickFormatting;
            set => SetProperty(ref _isQuickFormatting, value);
        }

        private string _quickFormatStatus = "Ready";
        public string QuickFormatStatus
        {
            get => _quickFormatStatus;
            set => SetProperty(ref _quickFormatStatus, value);
        }

        private int _quickFormatProgress;
        public int QuickFormatProgress
        {
            get => _quickFormatProgress;
            set => SetProperty(ref _quickFormatProgress, value);
        }

        private string _quickFormatElapsed = "00:00:00";
        public string QuickFormatElapsed
        {
            get => _quickFormatElapsed;
            set => SetProperty(ref _quickFormatElapsed, value);
        }
        #endregion

        // Commands
        public ICommand RefreshDrivesCommand { get; }
        public ICommand ElevateCommand { get; }
        public ICommand OpenFormatCommand { get; }
        public ICommand OpenHealthDetailsCommand { get; }
        public ICommand BackToDashboardCommand { get; }
        public ICommand ToggleSettingsCommand { get; }

        // Quick Format Commands
        public ICommand RequestQuickFormatCommand { get; }
        public ICommand ConfirmQuickFormatCommand { get; }
        public ICommand CancelQuickFormatCommand { get; }

        public MainViewModel(DriveDetectionService driveDetectionService, FormattingService formattingService)
        {
            _driveDetectionService = driveDetectionService;
            _formattingService = formattingService;

            _quickFormatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _quickFormatTimer.Tick += (s, e) =>
            {
                var span = DateTime.Now - _quickFormatStartTime;
                QuickFormatElapsed = span.ToString(@"hh\:mm\:ss");
            };

            RefreshDrivesCommand = new RelayCommand(async () => await ScanDrivesAsync());
            ElevateCommand = new RelayCommand(() => App.RestartAsAdministrator());
            OpenFormatCommand = new RelayCommand(driveObj =>
            {
                if (driveObj is PhysicalDriveInfo drive)
                    NavigateToFormat(drive);
            });
            OpenHealthDetailsCommand = new RelayCommand(driveObj =>
            {
                if (driveObj is PhysicalDriveInfo drive)
                    NavigateToHealthDetails(drive);
            });
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            ToggleSettingsCommand = new RelayCommand(() => IsSettingsDialogOpen = !IsSettingsDialogOpen);

            RequestQuickFormatCommand = new RelayCommand(driveObj =>
            {
                if (driveObj is PhysicalDriveInfo drive && !drive.IsSystemDrive)
                {
                    QuickFormatTargetDrive = drive;
                    IsQuickFormatConfirmationOpen = true;
                }
            });

            ConfirmQuickFormatCommand = new RelayCommand(async () => await ExecuteQuickFormatAsync(), () => !IsQuickFormatting);
            CancelQuickFormatCommand = new RelayCommand(() =>
            {
                if (!IsQuickFormatting)
                {
                    IsQuickFormatConfirmationOpen = false;
                    QuickFormatTargetDrive = null;
                }
            });

            // Initial drive scan
            _ = ScanDrivesAsync();
        }

        public async Task ScanDrivesAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            StatusText = "Scanning physical storage devices & querying SMART telemetry...";

            try
            {
                var detected = await Task.Run(() => _driveDetectionService.GetAllDrives());
                Drives.Clear();
                foreach (var d in detected)
                {
                    Drives.Add(d);
                }

                StatusText = $"{Drives.Count} drive(s) detected. Hardware telemetry up to date.";
            }
            catch (Exception ex)
            {
                StatusText = $"Scan error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        public async Task ExecuteQuickFormatAsync()
        {
            if (QuickFormatTargetDrive == null || QuickFormatTargetDrive.IsSystemDrive)
                return;

            IsQuickFormatting = true;
            QuickFormatProgress = 0;
            QuickFormatStatus = "Starting quick format...";
            _quickFormatStartTime = DateTime.Now;
            _quickFormatTimer.Start();

            _quickFormatCts = new CancellationTokenSource();

            var parameters = new FormatParameters
            {
                DiskNumber = QuickFormatTargetDrive.DiskNumber,
                TargetDriveModel = QuickFormatTargetDrive.Model,
                TargetSerialNumber = QuickFormatTargetDrive.SerialNumber,
                PartitionScheme = DefaultQuickPartitionScheme,
                TargetSystem = DefaultQuickPartitionScheme == PartitionScheme.GPT ? TargetSystem.UEFI : TargetSystem.BIOS,
                FileSystem = DefaultQuickFileSystem,
                ClusterSize = 4096,
                VolumeLabel = DefaultQuickVolumeLabel,
                QuickFormat = true,
                CheckBadBlocks = false
            };

            var progress = new Progress<(int percent, string message)>(report =>
            {
                QuickFormatProgress = report.percent;
                QuickFormatStatus = report.message;
            });

            bool success = await _formattingService.FormatDriveAsync(QuickFormatTargetDrive, parameters, progress, _quickFormatCts.Token);

            _quickFormatTimer.Stop();
            IsQuickFormatting = false;

            if (success)
            {
                QuickFormatStatus = "Format Complete! Disk is ready.";
                await Task.Delay(1500);
                IsQuickFormatConfirmationOpen = false;
                QuickFormatTargetDrive = null;
                await ScanDrivesAsync();
            }
            else
            {
                QuickFormatStatus = "Format encountered an issue.";
            }
        }

        public void NavigateToFormat(PhysicalDriveInfo drive)
        {
            SelectedDrive = drive;
            FormatViewModel = new FormatViewModel(drive, _formattingService, NavigateToDashboard);
            CurrentView = AppView.Format;
        }

        public void NavigateToHealthDetails(PhysicalDriveInfo drive)
        {
            SelectedDrive = drive;
            HealthDetailsViewModel = new HealthDetailsViewModel(drive, NavigateToDashboard);
            CurrentView = AppView.HealthDetails;
        }

        public void NavigateToDashboard()
        {
            CurrentView = AppView.Dashboard;
            _ = ScanDrivesAsync();
        }
    }
}
