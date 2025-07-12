using System.Diagnostics;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace LUDUS.Services {
    public class AdbService {
        private readonly string _adbPath;
        public AdbService(string adbPath = "adb") => _adbPath = adbPath;

        // Persistent shell process
        private Process _shellProc;
        private StreamWriter _shellStdin;
        private StreamReader _shellStdout;
        private readonly object _shellLock = new object();
        private bool _shellReady = false;

        // Khởi tạo persistent shell
        public void StartShell(string deviceId) {
            lock (_shellLock) {
                if (_shellProc != null && !_shellProc.HasExited) return;
                var psi = new ProcessStartInfo {
                    FileName = _adbPath,
                    Arguments = $"-s {deviceId} shell",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                _shellProc = Process.Start(psi);
                _shellStdin = _shellProc.StandardInput;
                _shellStdout = _shellProc.StandardOutput;
                _shellReady = true;
            }
        }

        // Đóng persistent shell
        public void StopShell() {
            lock (_shellLock) {
                _shellReady = false;
                try { _shellStdin?.WriteLine("exit"); } catch { }
                try { _shellProc?.Kill(); } catch { }
                try { _shellStdin?.Dispose(); } catch { }
                try { _shellStdout?.Dispose(); } catch { }
                try { _shellProc?.Dispose(); } catch { }
                _shellProc = null;
                _shellStdin = null;
                _shellStdout = null;
            }
        }

        // Gửi lệnh shell qua persistent process
        public string RunShellPersistent(string cmd, int timeoutMilliseconds = 3000) {
            lock (_shellLock) {
                if (!_shellReady || _shellProc == null || _shellProc.HasExited)
                    throw new InvalidOperationException("Shell chưa được khởi tạo hoặc đã dừng.");
                // Sinh một marker để biết đâu là kết thúc output
                string marker = $"__LUDUS_DONE_{Guid.NewGuid():N}";
                _shellStdin.WriteLine(cmd);
                _shellStdin.WriteLine($"echo {marker}");
                _shellStdin.Flush();
                var sb = new StringBuilder();
                string line;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMilliseconds) {
                    line = _shellStdout.ReadLine();
                    if (line == null) break;
                    if (line.Trim() == marker) break;
                    sb.AppendLine(line);
                }
                return sb.ToString().TrimEnd();
            }
        }

        public (string Output, string Error, int ExitCode) Run(string args, int timeoutMilliseconds = 3000) {
            var psi = new ProcessStartInfo {
                FileName = _adbPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi)) {
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();

                bool exited = proc.WaitForExit(timeoutMilliseconds);
                if (!exited) {
                    try { proc.Kill(); } catch { }
                    return (output, "Process timed out.", -1);
                }

                return (output, error, proc.ExitCode);
            }
        }

        public bool IsAppRunning(string deviceId, string packageName) {
            // Trả về true nếu app đang chạy (bằng pidof, chỉ hỗ trợ API 21+)
            string output = RunWithOutput($"-s {deviceId} shell pidof {packageName}");
            return !string.IsNullOrWhiteSpace(output);
        }

        // Nếu không có RunWithOutput thì có thể dùng lệnh shell và đọc output (ProcessStartInfo)
        public string RunWithOutput(string args, int timeoutMilliseconds = 3000) {
            var psi = new ProcessStartInfo {
                FileName = _adbPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi)) {
                string output = proc.StandardOutput.ReadToEnd();
                bool exited = proc.WaitForExit(timeoutMilliseconds);

                if (!exited) {
                    try { proc.Kill(); } catch { }
                    return $"Process timed out after {timeoutMilliseconds}ms.";
                }

                return output;
            }
        }
    }
}
