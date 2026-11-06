using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using BBTechSSDTool.Services;
using BBTechSSDTool.Services.Native;
using BBTechSSDTool.ViewModels;

namespace BBTechSSDTool
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;
        private readonly DriveDetectionService _driveDetector;
        private IntPtr _hDiskNotify = IntPtr.Zero;
        private IntPtr _hVolNotify = IntPtr.Zero;
        private readonly DispatcherTimer _pollTimer;
        private int _lastKnownDriveCount = -1;

        public MainWindow()
        {
            InitializeComponent();

            var healthEngine = new HealthEngineService();
            _driveDetector = new DriveDetectionService(healthEngine);
            var formatService = new FormattingService(_driveDetector);

            _mainViewModel = new MainViewModel(_driveDetector, formatService);
            DataContext = _mainViewModel;

            // HD Sentinel Pro style auto-detection timer (every 2.5 seconds)
            // Seamlessly detects newly plugged in SSDs, HDDs, and USB enclosures in real-time
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            _pollTimer.Tick += OnPollTimerTick;
            _pollTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(HwndHook);
                RegisterHardwareNotifications(source.Handle);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _pollTimer.Stop();
            if (_hDiskNotify != IntPtr.Zero)
            {
                Win32Storage.UnregisterDeviceNotification(_hDiskNotify);
                _hDiskNotify = IntPtr.Zero;
            }
            if (_hVolNotify != IntPtr.Zero)
            {
                Win32Storage.UnregisterDeviceNotification(_hVolNotify);
                _hVolNotify = IntPtr.Zero;
            }
            base.OnClosed(e);
        }

        private void RegisterHardwareNotifications(IntPtr hwnd)
        {
            try
            {
                // Register for Disk Device Interface notifications (e.g. USB SSD enclosures, external drives)
                var diskFilter = new Win32Storage.DEV_BROADCAST_DEVICEINTERFACE
                {
                    dbcc_size = Marshal.SizeOf<Win32Storage.DEV_BROADCAST_DEVICEINTERFACE>(),
                    dbcc_devicetype = Win32Storage.DBT_DEVTYP_DEVICEINTERFACE,
                    dbcc_classguid = Win32Storage.GUID_DEVINTERFACE_DISK
                };
                IntPtr pDiskFilter = Marshal.AllocHGlobal(diskFilter.dbcc_size);
                Marshal.StructureToPtr(diskFilter, pDiskFilter, false);
                _hDiskNotify = Win32Storage.RegisterDeviceNotification(hwnd, pDiskFilter, Win32Storage.DEVICE_NOTIFY_WINDOW_HANDLE);
                Marshal.FreeHGlobal(pDiskFilter);

                // Register for Volume Interface notifications
                var volFilter = new Win32Storage.DEV_BROADCAST_DEVICEINTERFACE
                {
                    dbcc_size = Marshal.SizeOf<Win32Storage.DEV_BROADCAST_DEVICEINTERFACE>(),
                    dbcc_devicetype = Win32Storage.DBT_DEVTYP_DEVICEINTERFACE,
                    dbcc_classguid = Win32Storage.GUID_DEVINTERFACE_VOLUME
                };
                IntPtr pVolFilter = Marshal.AllocHGlobal(volFilter.dbcc_size);
                Marshal.StructureToPtr(volFilter, pVolFilter, false);
                _hVolNotify = Win32Storage.RegisterDeviceNotification(hwnd, pVolFilter, Win32Storage.DEVICE_NOTIFY_WINDOW_HANDLE);
                Marshal.FreeHGlobal(pVolFilter);
            }
            catch
            {
                // Fall back gracefully to background polling timer
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32Storage.WM_DEVICECHANGE)
            {
                int eventCode = wParam.ToInt32();
                if (eventCode == Win32Storage.DBT_DEVICEARRIVAL || 
                    eventCode == Win32Storage.DBT_DEVICEREMOVECOMPLETE ||
                    eventCode == Win32Storage.DBT_DEVNODES_CHANGED ||
                    eventCode == Win32Storage.DBT_CONFIGCHANGED)
                {
                    // Debounce and trigger automated refresh
                    TriggerAutomatedScan(delayMs: 800);
                }
            }
            return IntPtr.Zero;
        }

        private async void OnPollTimerTick(object? sender, EventArgs e)
        {
            // Background check for disk topology changes (e.g. drive inserted or removed)
            if (_mainViewModel.IsScanning) return;

            try
            {
                int count = await Task.Run(() => _driveDetector.GetPhysicalDriveCount());
                if (_lastKnownDriveCount >= 0 && count != _lastKnownDriveCount)
                {
                    // Drive count changed -> automatically refresh
                    _lastKnownDriveCount = count;
                    await _mainViewModel.ScanDrivesAsync();
                }
                else
                {
                    _lastKnownDriveCount = count;
                }
            }
            catch
            {
                // Ignore transient polling exceptions
            }
        }

        private void TriggerAutomatedScan(int delayMs)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delayMs);
                await Dispatcher.InvokeAsync(async () =>
                {
                    await _mainViewModel.ScanDrivesAsync();
                });
            });
        }
    }
}
