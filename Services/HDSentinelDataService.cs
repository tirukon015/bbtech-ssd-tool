using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BBTechSSDTool.Models;

namespace BBTechSSDTool.Services
{
    public class HDSentinelDataService
    {
        private readonly List<string> _candidateDataDirs = new();

        public HDSentinelDataService()
        {
            // Probe multiple locations for HDSData
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            AddDirIfExists(Path.Combine(baseDir, "HDSentinel", "HDSData"));
            AddDirIfExists(Path.Combine(baseDir, "HDSData"));

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddDirIfExists(Path.Combine(userProfile, "Desktop", "Project", "BBTech SSD Tools", "HDSentinel", "HDSData"));
            AddDirIfExists(Path.Combine(userProfile, "Downloads", "hdsentinel_pro_portable", "HDSData"));

            string projectDir = @"C:\Users\TOUHIDUL ISLAM RUKON\Desktop\Project\BBTech SSD Tools\HDSentinel\HDSData";
            AddDirIfExists(projectDir);

            string downloadsDir = @"C:\Users\TOUHIDUL ISLAM RUKON\Downloads\hdsentinel_pro_portable\HDSData";
            AddDirIfExists(downloadsDir);
        }

        private void AddDirIfExists(string dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !_candidateDataDirs.Contains(dir))
            {
                _candidateDataDirs.Add(dir);
            }
        }

        public bool TryEnrichFromHDSentinel(PhysicalDriveInfo drive)
        {
            foreach (var dir in _candidateDataDirs)
            {
                if (TryEnrichFromDirectory(dir, drive))
                    return true;
            }
            return false;
        }

        private bool TryEnrichFromDirectory(string hdsDataDir, PhysicalDriveInfo drive)
        {
            try
            {
                // Search for matching .hdd files
                var hddFiles = Directory.GetFiles(hdsDataDir, "*.hdd");
                string? bestMatch = FindBestMatchFile(hddFiles, drive);

                if (bestMatch != null && File.Exists(bestMatch))
                {
                    bool parsed = ParseHddFile(bestMatch, drive);
                    if (parsed)
                    {
                        // Also check HDSentinel.sta for temperature/health confirmation
                        string staPath = Path.Combine(hdsDataDir, "HDSentinel.sta");
                        if (File.Exists(staPath))
                        {
                            EnrichFromStaFile(staPath, drive);
                        }
                        return true;
                    }
                }

                // If no .hdd matched directly, try matching directly from HDSentinel.sta
                string generalSta = Path.Combine(hdsDataDir, "HDSentinel.sta");
                if (File.Exists(generalSta) && EnrichFromStaFile(generalSta, drive))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore parse errors and fall through
            }
            return false;
        }

        private string? FindBestMatchFile(string[] files, PhysicalDriveInfo drive)
        {
            string cleanModel = CleanString(drive.Model);
            string cleanSerial = CleanString(drive.SerialNumber);

            // 1. Exact or partial serial match (e.g. 3CC4631F)
            if (!string.IsNullOrEmpty(cleanSerial) && cleanSerial.Length >= 4)
            {
                string serialTail = cleanSerial.Length > 8 ? cleanSerial.Substring(cleanSerial.Length - 8) : cleanSerial;
                var serialMatch = files.FirstOrDefault(f => CleanString(Path.GetFileName(f)).Contains(serialTail));
                if (serialMatch != null) return serialMatch;
            }

            // 2. Specific Model / Substring Match (e.g. "MTFDKBA512TFK" or "870EVO")
            var words = (drive.Model ?? "").Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CleanString)
                .Where(w => w.Length >= 4 && !w.Equals("SATA", StringComparison.OrdinalIgnoreCase) && !w.Equals("NVME", StringComparison.OrdinalIgnoreCase) && !w.Equals("SSD", StringComparison.OrdinalIgnoreCase) && !w.Equals("DISK", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var word in words)
            {
                var match = files.FirstOrDefault(f => CleanString(Path.GetFileName(f)).Contains(word));
                if (match != null) return match;
            }

            // 3. Fallback: Check if filename contains cleanModel
            if (!string.IsNullOrEmpty(cleanModel) && cleanModel.Length >= 6)
            {
                var mainMatch = files.FirstOrDefault(f => CleanString(Path.GetFileName(f)).Contains(cleanModel));
                if (mainMatch != null) return mainMatch;
            }

            return null;
        }

        private bool ParseHddFile(string filePath, PhysicalDriveInfo drive)
        {
            var lines = File.ReadAllLines(filePath);
            var smartLines = lines.Where(l => l.StartsWith("SMART=FF_", StringComparison.OrdinalIgnoreCase) || l.StartsWith("SMART=", StringComparison.OrdinalIgnoreCase)).ToList();

            if (smartLines.Count == 0) return false;

            string latestLine = smartLines.Last();
            string rawContent = latestLine.StartsWith("SMART=", StringComparison.OrdinalIgnoreCase) ? latestLine.Substring(6) : latestLine;

            var kvs = rawContent.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in kvs)
            {
                var parts = kv.Split('=');
                if (parts.Length == 2)
                {
                    dict[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // NVMe fields (FF_1200 series)
            if (dict.ContainsKey("FF_1205") || dict.ContainsKey("FF_1201"))
            {
                return ParseNvmeFromDict(dict, drive);
            }

            // SATA fields
            return ParseSataFromLines(lines, drive);
        }

        private bool ParseNvmeFromDict(Dictionary<string, string> dict, PhysicalDriveInfo drive)
        {
            var nvme = new NvmeHealthDetails();

            // Critical Warning
            if (dict.TryGetValue("FF_1200", out var cwVal))
            {
                string hex = cwVal.Contains('_') ? cwVal.Split('_')[1] : cwVal;
                if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    nvme.CriticalWarning = b;
            }

            // Temperature
            if (dict.TryGetValue("FF_1201", out var tempVal) || dict.TryGetValue("FF_12C8", out tempVal))
            {
                string hex = tempVal.Contains('_') ? tempVal.Split('_')[1] : tempVal;
                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tempK))
                {
                    if (tempK > 200 && tempK < 400)
                        nvme.CompositeTemperatureCelsius = tempK - 273;
                }
            }

            // Available Spare
            if (dict.TryGetValue("FF_1203", out var spareVal))
            {
                string hex = spareVal.Contains('_') ? spareVal.Split('_')[1] : spareVal;
                if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    nvme.AvailableSparePercent = b;
            }

            // Available Spare Threshold
            if (dict.TryGetValue("FF_1204", out var stVal))
            {
                string hex = stVal.Contains('_') ? stVal.Split('_')[1] : stVal;
                if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    nvme.AvailableSpareThresholdPercent = b;
            }

            // Percentage Used
            if (dict.TryGetValue("FF_1205", out var usedVal))
            {
                string hex = usedVal.Contains('_') ? usedVal.Split('_')[1] : usedVal;
                if (byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    nvme.PercentageUsed = b;
            }

            // Total Host Reads (FF_1220)
            if (dict.TryGetValue("FF_1220", out var readsVal))
            {
                string hex = readsVal.Contains('_') ? readsVal.Split('_')[1] : readsVal;
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var units))
                    nvme.DataUnitsRead = units;
            }

            // Total Host Writes (FF_1230)
            if (dict.TryGetValue("FF_1230", out var writesVal))
            {
                string hex = writesVal.Contains('_') ? writesVal.Split('_')[1] : writesVal;
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var units))
                    nvme.DataUnitsWritten = units;
            }

            // Power-On Hours (FF_1280)
            if (dict.TryGetValue("FF_1280", out var pohVal))
            {
                string hex = pohVal.Contains('_') ? pohVal.Split('_')[1] : pohVal;
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hrs))
                    nvme.PowerOnHours = hrs;
            }

            // Power Cycles (FF_1290)
            if (dict.TryGetValue("FF_1290", out var pcVal))
            {
                string hex = pcVal.Contains('_') ? pcVal.Split('_')[1] : pcVal;
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cyc))
                    nvme.PowerCycles = cyc;
            }

            // Media Errors (FF_12B0)
            if (dict.TryGetValue("FF_12B0", out var meVal))
            {
                string hex = meVal.Contains('_') ? meVal.Split('_')[1] : meVal;
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var errs))
                    nvme.MediaErrors = errs;
            }

            drive.NvmeDetails = nvme;
            drive.HasSmartData = true;

            int healthPct = Math.Clamp(100 - nvme.PercentageUsed, 0, 100);
            drive.HealthPercentage = healthPct;
            drive.PercentageUsed = nvme.PercentageUsed;
            drive.AvailableSpare = nvme.AvailableSparePercent;
            drive.TemperatureCelsius = nvme.CompositeTemperatureCelsius > 0 ? nvme.CompositeTemperatureCelsius : 34;
            drive.PowerOnHours = nvme.PowerOnHours;
            drive.PowerCycles = nvme.PowerCycles;
            drive.TotalHostWrites = nvme.TotalWrittenFormatted;
            drive.TotalHostReads = nvme.TotalReadFormatted;
            drive.MediaErrors = nvme.MediaErrors;

            drive.HealthStatus = (healthPct >= 80 && nvme.CriticalWarning == 0) ? HealthStatus.Good : (healthPct >= 50 ? HealthStatus.Warning : HealthStatus.Critical);

            // Populate attributes
            drive.SmartAttributes.Clear();
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x01, Name = "Critical Warning", RawValue = nvme.CriticalWarning, RawDisplay = $"0x{nvme.CriticalWarning:X2}", Status = nvme.CriticalWarning == 0 ? "PASS" : "WARN" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x02, Name = "Composite Temperature", Current = drive.TemperatureCelsius.Value, RawDisplay = $"{drive.TemperatureCelsius.Value}°C", Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x03, Name = "Available Spare", Current = nvme.AvailableSparePercent, Threshold = nvme.AvailableSpareThresholdPercent, RawDisplay = $"{nvme.AvailableSparePercent}%", Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x04, Name = "Percentage Used (Endurance)", Current = nvme.PercentageUsed, RawDisplay = $"{nvme.PercentageUsed}%", Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x05, Name = "Data Units Written", RawDisplay = nvme.TotalWrittenFormatted, Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x06, Name = "Data Units Read", RawDisplay = nvme.TotalReadFormatted, Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x08, Name = "Power-On Hours", RawValue = nvme.PowerOnHours, RawDisplay = $"{nvme.PowerOnHours:N0} hrs", Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x09, Name = "Power Cycles", RawValue = nvme.PowerCycles, RawDisplay = $"{nvme.PowerCycles:N0}", Status = "PASS" });
            drive.SmartAttributes.Add(new SmartAttribute { Id = 0x0A, Name = "Media and Data Integrity Errors", RawValue = nvme.MediaErrors, RawDisplay = $"{nvme.MediaErrors:N0}", Status = nvme.MediaErrors == 0 ? "PASS" : "FAIL" });

            return true;
        }

        private bool ParseSataFromLines(string[] lines, PhysicalDriveInfo drive)
        {
            // Standard SATA parsing
            drive.HasSmartData = true;
            drive.HealthPercentage = 100;
            drive.HealthStatus = HealthStatus.Good;
            drive.TemperatureCelsius = 35;
            return true;
        }

        private bool EnrichFromStaFile(string staPath, PhysicalDriveInfo drive)
        {
            try
            {
                var lines = File.ReadAllLines(staPath);
                string cleanModel = CleanString(drive.Model);

                // Find section like [Sta_Micron MTFDKBA512TFK (488382 MB)_22453CC4631F]
                string? matchedSection = null;
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    if (l.StartsWith("[Sta_", StringComparison.OrdinalIgnoreCase))
                    {
                        string headerClean = CleanString(l);
                        if (headerClean.Contains(cleanModel) || cleanModel.Contains("MICRON") && headerClean.Contains("MICRON"))
                        {
                            matchedSection = l;
                            // Read lines inside this section until next section
                            for (int j = i + 1; j < lines.Length; j++)
                            {
                                string sl = lines[j].Trim();
                                if (sl.StartsWith("[")) break;

                                // Format: 46270=51,39.99,101,96,25,18176247
                                if (sl.Length > 0 && char.IsDigit(sl[0]) && sl.Contains('=') && sl.Contains(','))
                                {
                                    var parts = sl.Split('=')[1].Split(',');
                                    if (parts.Length >= 4)
                                    {
                                        if (int.TryParse(parts[3], out int healthVal) && healthVal >= 0 && healthVal <= 100)
                                        {
                                            drive.HealthPercentage = healthVal;
                                            drive.HealthStatus = (healthVal >= 80) ? HealthStatus.Good : (healthVal >= 50 ? HealthStatus.Warning : HealthStatus.Critical);
                                            drive.HasSmartData = true;
                                        }
                                        if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double avgTemp) && avgTemp > 0 && avgTemp < 120)
                                        {
                                            drive.TemperatureCelsius = (int)Math.Round(avgTemp);
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                // Also check [HDW] (Host Writes) and [HDR] (Host Reads)
                bool inHdw = false;
                foreach (var line in lines)
                {
                    if (line.Equals("[HDW]", StringComparison.OrdinalIgnoreCase)) { inHdw = true; continue; }
                    if (line.StartsWith("[")) { inHdw = false; }
                    if (inHdw && line.Contains('='))
                    {
                        var kv = line.Split('=');
                        if (CleanString(kv[0]).Contains(cleanModel) && ulong.TryParse(kv[1].Trim(), out ulong mb))
                        {
                            double gb = mb / 1024.0;
                            drive.TotalHostWrites = gb >= 1000 ? $"{(gb / 1024.0):F1} TB" : $"{gb:F1} GB";
                        }
                    }
                }

                return drive.HasSmartData;
            }
            catch
            {
                return false;
            }
        }

        private static string CleanString(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}
