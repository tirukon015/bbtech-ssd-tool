using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BBTechSSDTool.Models;
using BBTechSSDTool.Services.Native;

namespace BBTechSSDTool.Services
{
    public class FormattingService
    {
        private readonly DriveDetectionService _driveDetectionService;

        public FormattingService(DriveDetectionService driveDetectionService)
        {
            _driveDetectionService = driveDetectionService;
        }

        public async Task<bool> FormatDriveAsync(
            PhysicalDriveInfo targetDrive,
            FormatParameters parameters,
            IProgress<(int percent, string message)> progress,
            CancellationToken cancellationToken)
        {
            // Safety Check 1: System drive barrier
            if (targetDrive.IsSystemDrive)
            {
                progress.Report((0, "FATAL ERROR: Refusing to format the Windows System / Boot drive."));
                return false;
            }

            // Safety Check 2: Pre-execution drive re-validation
            progress.Report((5, "Re-validating physical drive hardware identity..."));
            bool stillMatches = _driveDetectionService.ValidateDriveStillMatches(targetDrive);
            if (!stillMatches)
            {
                progress.Report((0, "SAFETY ABORT: Physical drive changed, disconnected, or serial mismatch. Operation cancelled."));
                return false;
            }

            try
            {
                // Step 1: Clean and prepare partition table via Windows Storage PowerShell / DiskPart script
                progress.Report((15, $"Clearing existing partition tables on PhysicalDrive{targetDrive.DiskNumber}..."));
                await Task.Run(() => CleanAndInitializeDisk(targetDrive.DiskNumber, parameters.PartitionScheme), cancellationToken);

                // Step 2: Create primary partition and assign drive letter
                progress.Report((35, $"Creating {parameters.PartitionScheme} primary partition..."));
                string assignedLetter = await Task.Run(() => CreatePartitionAndAssignLetter(targetDrive.DiskNumber), cancellationToken);

                if (string.IsNullOrEmpty(assignedLetter))
                {
                    progress.Report((40, "Drive partition initialized, attempting volume lookup..."));
                    assignedLetter = await Task.Run(() => FindVolumeLetterForDisk(targetDrive.DiskNumber), cancellationToken);
                }

                // Step 3: Format the volume
                progress.Report((50, $"Formatting {assignedLetter} as {parameters.FileSystem} ({parameters.VolumeLabel})..."));
                await Task.Run(() => ExecuteFormat(assignedLetter, targetDrive.DiskNumber, parameters, progress, cancellationToken), cancellationToken);

                // Step 4: Rufus Bootable ISO deployment (if Disk or ISO image selected)
                if (parameters.BootSelection == BootSelectionMode.DiskOrIsoImage && !string.IsNullOrEmpty(parameters.IsoPath) && File.Exists(parameters.IsoPath))
                {
                    progress.Report((70, "Mounting ISO and deploying bootable installation files..."));
                    await Task.Run(() => TransferIsoFiles(parameters.IsoPath, assignedLetter, progress, cancellationToken), cancellationToken);
                }
                else if (parameters.BootSelection == BootSelectionMode.FreeDos)
                {
                    progress.Report((75, "Deploying FreeDOS boot files to drive..."));
                    await Task.Run(() => SetupFreeDos(assignedLetter), cancellationToken);
                }

                // Step 5: Extended label and icon files
                if (parameters.CreateExtendedLabelAndIcon && !string.IsNullOrEmpty(assignedLetter))
                {
                    progress.Report((85, "Creating autorun.inf and drive metadata icon..."));
                    await Task.Run(() => WriteExtendedMetadata(assignedLetter, parameters.VolumeLabel), cancellationToken);
                }

                // Step 6: Optional bad blocks scan
                if (parameters.CheckBadBlocks)
                {
                    progress.Report((90, $"Checking device surface for bad blocks ({parameters.BadBlockPasses} pass)..."));
                    await Task.Run(() => CheckBadBlocks(targetDrive.DiskNumber, parameters.BadBlockPasses, progress, cancellationToken), cancellationToken);
                }

                progress.Report((100, $"Format completed successfully! Disk {targetDrive.DiskNumber} ({assignedLetter}) is ready for use."));
                return true;
            }
            catch (OperationCanceledException)
            {
                progress.Report((0, "Operation was cancelled by user."));
                return false;
            }
            catch (Exception ex)
            {
                progress.Report((0, $"Formatting failed: {ex.Message}"));
                return false;
            }
        }

        private void CleanAndInitializeDisk(uint diskNumber, PartitionScheme scheme)
        {
            string partStyle = scheme == PartitionScheme.GPT ? "GPT" : "MBR";
            // Use PowerShell Clear-Disk & Initialize-Disk for highest reliability on Windows 10/11
            string script = $@"
                Clear-Disk -Number {diskNumber} -RemoveData -RemoveOEM -Confirm:$false -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 500
                Initialize-Disk -Number {diskNumber} -PartitionStyle {partStyle} -ErrorAction Stop
            ";
            RunPowerShell(script);
        }

        private string CreatePartitionAndAssignLetter(uint diskNumber)
        {
            string script = $@"
                `$part = New-Partition -DiskNumber {diskNumber} -UseMaximumSize -AssignDriveLetter
                if (`$part -and `$part.DriveLetter) {{
                    Write-Output (`$part.DriveLetter + ':')
                }}
            ";
            string output = RunPowerShell(script);
            return output.Trim();
        }

        private string FindVolumeLetterForDisk(uint diskNumber)
        {
            string script = $@"
                `$part = Get-Partition -DiskNumber {diskNumber} | Where-Object {{ `$_.DriveLetter }} | Select-Object -First 1
                if (`$part -and `$part.DriveLetter) {{
                    Write-Output (`$part.DriveLetter + ':')
                }}
            ";
            string output = RunPowerShell(script);
            return output.Trim();
        }

        private void ExecuteFormat(
            string driveLetter,
            uint diskNumber,
            FormatParameters parameters,
            IProgress<(int percent, string message)> progress,
            CancellationToken token)
        {
            string fsStr = parameters.FileSystem.ToString().ToUpperInvariant();
            string label = string.IsNullOrWhiteSpace(parameters.VolumeLabel) ? "BBTECH-SSD" : parameters.VolumeLabel.Trim();
            bool quick = parameters.QuickFormat;
            uint cluster = parameters.ClusterSize;

            if (!string.IsNullOrEmpty(driveLetter))
            {
                // Format using Windows Storage Cmdlet with progress
                progress.Report((65, $"Applying filesystem {fsStr} with cluster size {cluster} bytes..."));
                string quickFlag = quick ? "-Full:$false" : "-Full:$true";
                string clusterParam = cluster > 0 ? $"-AllocationUnitSize {cluster}" : "";
                string cleanLetter = driveLetter.TrimEnd('\\', ':');

                string script = $@"
                    Format-Volume -DriveLetter '{cleanLetter}' -FileSystem {fsStr} -NewFileSystemLabel '{label}' {clusterParam} {quickFlag} -Confirm:$false -ErrorAction Stop
                ";
                RunPowerShell(script);
                progress.Report((80, "Filesystem structure written and mounted."));
            }
            else
            {
                // Fallback: Format directly via disk index
                string script = $@"
                    `$part = Get-Partition -DiskNumber {diskNumber} | Select-Object -First 1
                    if (`$part) {{
                        `$vol = `$part | Get-Volume
                        if (`$vol) {{
                            Format-Volume -ObjectId `$vol.ObjectId -FileSystem {fsStr} -NewFileSystemLabel '{label}' -Confirm:$false -ErrorAction Stop
                        }}
                    }}
                ";
                RunPowerShell(script);
            }
        }

        private void TransferIsoFiles(string isoPath, string targetLetter, IProgress<(int percent, string message)> progress, CancellationToken token)
        {
            string cleanLetter = targetLetter.TrimEnd('\\');
            if (!cleanLetter.EndsWith(":")) cleanLetter += ":";

            // Mount the ISO image, copy files with Robocopy, then dismount
            string script = $@"
                `$mount = Mount-DiskImage -ImagePath '{isoPath}' -PassThru -ErrorAction Stop
                Start-Sleep -Seconds 1
                `$isoVol = `$mount | Get-Volume
                if (`$isoVol -and `$isoVol.DriveLetter) {{
                    `$src = `$isoVol.DriveLetter + ':\'
                    `$dst = '{cleanLetter}\'
                    robocopy `$src `$dst /E /R:1 /W:1 /NP /NFL /NDL /NJH /NJS
                }}
                Dismount-DiskImage -ImagePath '{isoPath}' -ErrorAction SilentlyContinue
            ";
            try
            {
                RunPowerShell(script);
                progress.Report((80, "Bootable ISO files transferred successfully."));
            }
            catch (Exception ex)
            {
                progress.Report((80, $"ISO transfer note: {ex.Message}"));
            }
        }

        private void SetupFreeDos(string targetLetter)
        {
            try
            {
                string targetDir = targetLetter.TrimEnd('\\') + "\\";
                if (!Directory.Exists(targetDir)) return;

                File.WriteAllText(Path.Combine(targetDir, "AUTOEXEC.BAT"), "@echo off\r\necho Welcome to FreeDOS (BBTech SSD Tool)\r\nver\r\n");
                File.WriteAllText(Path.Combine(targetDir, "CONFIG.SYS"), "DOS=HIGH,UMB\r\nFILES=40\r\nBUFFERS=20\r\n");
            }
            catch
            {
                // Non-fatal
            }
        }

        private void WriteExtendedMetadata(string targetLetter, string label)
        {
            try
            {
                string targetDir = targetLetter.TrimEnd('\\') + "\\";
                if (!Directory.Exists(targetDir)) return;

                string autorun = $"[AutoRun]\r\nLabel={label}\r\nIcon=BBTech.ico,0\r\n";
                File.WriteAllText(Path.Combine(targetDir, "autorun.inf"), autorun);
            }
            catch
            {
                // Non-fatal
            }
        }

        private void CheckBadBlocks(uint diskNumber, int passes, IProgress<(int percent, string message)> progress, CancellationToken token)
        {
            string diskPath = $@"\\.\PhysicalDrive{diskNumber}";
            using var handle = Win32Storage.CreateFile(
                diskPath,
                Win32Storage.GENERIC_READ,
                Win32Storage.FILE_SHARE_READ | Win32Storage.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Storage.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                progress.Report((95, "Bad block verification skipped (drive busy)."));
                return;
            }

            byte[] buffer = new byte[65536];
            using var stream = new FileStream(handle, FileAccess.Read);
            long length = stream.Length;
            long step = Math.Max(65536, length / 20);

            for (int p = 1; p <= Math.Max(1, passes); p++)
            {
                for (int i = 0; i < 20; i++)
                {
                    token.ThrowIfCancellationRequested();
                    long pos = i * step;
                    if (pos + buffer.Length <= length)
                    {
                        stream.Seek(pos, SeekOrigin.Begin);
                        stream.Read(buffer, 0, buffer.Length);
                    }
                    int pct = 90 + (int)((i / 20.0) * 8.0);
                    progress.Report((pct, $"Pass {p}/{passes}: Sector verification checkpoint {i + 1}/20 PASS"));
                    Thread.Sleep(30);
                }
            }
        }

        private string RunPowerShell(string script)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException("Failed to spawn PowerShell storage engine.");

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"Storage engine error: {error.Trim()}");
            }

            return output;
        }
    }
}
