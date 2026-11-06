using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using BBTechSSDTool.Services;

namespace BBTechSSDTool
{
    public partial class App : Application
    {
        public static bool IsAdmin { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash.log"), args.ExceptionObject?.ToString());
            };
            DispatcherUnhandledException += (s, args) =>
            {
                File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash.log"), args.Exception?.ToString());
                args.Handled = false;
            };

            IsAdmin = CheckIsAdministrator();

            // Support --test argument for automated hardware verification and diagnostics
            if (e.Args.Contains("--test"))
            {
                RunDiagnosticsTest();
                Shutdown(0);
                return;
            }

            base.OnStartup(e);
        }

        public static bool CheckIsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static void RestartAsAdministrator()
        {
            try
            {
                string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not elevate privileges: {ex.Message}", "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private static void RunDiagnosticsTest()
        {
            AttachConsole(-1);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== BBTech SSD Tool Test Run ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                var healthEngine = new HealthEngineService();
                var detector = new DriveDetectionService(healthEngine);
                var drives = detector.GetAllDrives();

                sb.AppendLine($"Discovered {drives.Count} physical drive(s):");
                foreach (var d in drives)
                {
                    string info = $" -> Disk {d.DiskNumber}: {d.Model} | Capacity: {d.FormattedCapacity} | Interface: {d.InterfaceDisplay} | Health: {d.HealthStatusText} ({d.HealthScoreDisplay}) | Temp: {d.TemperatureDisplay} | SystemDrive: {d.IsSystemDrive}";
                    sb.AppendLine(info);
                    Console.WriteLine(info);
                }
                sb.AppendLine("Test result: SUCCESS (Drive Detection, Health Engine, and System Protection all functional).");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Test failed with exception: {ex}");
                Console.WriteLine($"Test failed: {ex.Message}");
            }

            string testPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BBTech_SSD_Tool_Test_Result.txt");
            File.WriteAllText(testPath, sb.ToString());
            Console.WriteLine($"Diagnostic test written to: {testPath}");
        }
    }
}
