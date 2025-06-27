using System.Diagnostics;

namespace LUDUS.Services {
    public class AdbService {
        private readonly string _adbPath;
        public AdbService(string adbPath = "adb") => _adbPath = adbPath;

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
