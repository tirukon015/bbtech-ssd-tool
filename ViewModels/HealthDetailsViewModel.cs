using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using BBTechSSDTool.Models;

namespace BBTechSSDTool.ViewModels
{
    public class HealthDetailsViewModel : ViewModelBase
    {
        private readonly Action _onBackToDashboard;

        private PhysicalDriveInfo _drive;
        public PhysicalDriveInfo Drive
        {
            get => _drive;
            set => SetProperty(ref _drive, value);
        }

        public ICommand BackCommand { get; }
        public ICommand ExportReportCommand { get; }

        public HealthDetailsViewModel(PhysicalDriveInfo drive, Action onBackToDashboard)
        {
            _drive = drive;
            _onBackToDashboard = onBackToDashboard;

            BackCommand = new RelayCommand(() => _onBackToDashboard?.Invoke());
            ExportReportCommand = new RelayCommand(ExportReport);
        }

        private void ExportReport()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                      BBTECH SSD TOOL - HEALTH DIAGNOSTIC REPORT                ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("DRIVE IDENTIFICATION");
                sb.AppendLine($"  Model:            {Drive.Model}");
                sb.AppendLine($"  Capacity:         {Drive.FormattedCapacity} ({Drive.SizeBytes:N0} bytes)");
                sb.AppendLine($"  Disk Number:      Disk {Drive.DiskNumber}");
                sb.AppendLine($"  Serial Number:    {Drive.SerialNumber}");
                sb.AppendLine($"  Firmware:         {Drive.FirmwareRevision}");
                sb.AppendLine($"  Interface:        {Drive.InterfaceDisplay}");
                sb.AppendLine($"  Bus Type:         {Drive.BusType}");
                sb.AppendLine($"  Protocol:         {Drive.Protocol}");
                sb.AppendLine($"  System Drive:     {(Drive.IsSystemDrive ? "YES (Protected)" : "NO")}");
                sb.AppendLine();
                sb.AppendLine("HEALTH SUMMARY");
                sb.AppendLine($"  Health Status:    {Drive.HealthStatusText}");
                sb.AppendLine($"  Health Score:     {Drive.HealthScoreDisplay}");
                sb.AppendLine($"  Temperature:      {Drive.TemperatureDisplay}");
                sb.AppendLine($"  Power-On Hours:   {Drive.PowerOnHoursDisplay}");
                sb.AppendLine($"  Power Cycles:     {Drive.PowerCyclesDisplay}");
                sb.AppendLine($"  Total Host Reads: {Drive.TotalHostReads}");
                sb.AppendLine($"  Total Host Writes:{Drive.TotalHostWrites}");
                sb.AppendLine($"  Spare / Used:     Spare: {Drive.AvailableSpare?.ToString() ?? "--"}% | Used: {Drive.PercentageUsed?.ToString() ?? "--"}%");
                sb.AppendLine($"  Critical Warnings:{Drive.CriticalWarningsSummary}");
                sb.AppendLine($"  Media Errors:     {Drive.MediaErrors?.ToString() ?? "--"}");
                sb.AppendLine();

                if (Drive.SmartAttributes.Count > 0)
                {
                    sb.AppendLine("SMART / NVMe ATTRIBUTE DETAILS");
                    sb.AppendLine(string.Format("{0,-6} {1,-38} {2,-8} {3,-8} {4,-15} {5,-8}", "ID", "Attribute Name", "Current", "Worst", "Raw Value", "Status"));
                    sb.AppendLine(new string('-', 85));
                    foreach (var attr in Drive.SmartAttributes)
                    {
                        sb.AppendLine(string.Format("0x{0:X2}   {1,-38} {2,-8} {3,-8} {4,-15} {5,-8}",
                            attr.Id,
                            attr.Name.Length > 36 ? attr.Name.Substring(0, 36) : attr.Name,
                            attr.Current,
                            attr.Worst,
                            attr.RawDisplay,
                            attr.Status));
                    }
                }
                else if (!Drive.HasSmartData)
                {
                    sb.AppendLine("SMART TELEMETRY: UNAVAILABLE");
                    sb.AppendLine($"Reason: {Drive.SmartUnavailableReason}");
                }

                sb.AppendLine("================================================================================");

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string cleanModel = string.Concat(Drive.Model.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(desktopPath, $"BBTech_HealthReport_Disk{Drive.DiskNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(filePath, sb.ToString());

                MessageBox.Show($"Diagnostic report saved to Desktop:\n{filePath}", "Report Exported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
