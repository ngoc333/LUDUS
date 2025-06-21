using System.Diagnostics;

namespace LUDUS.Services {
    public class AdbService {
        private readonly string _adbPath;
        public AdbService(string adbPath = "adb") => _adbPath = adbPath;

        public (string Output, string Error, int ExitCode) Run(string args) {
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
                proc.WaitForExit();
                return (output, error, proc.ExitCode);
            }
        }

        public bool IsAppRunning(string deviceId, string packageName) {
            // Trả về true nếu app đang chạy (bằng pidof, chỉ hỗ trợ API 21+)
            string output = RunWithOutput($"-s {deviceId} shell pidof {packageName}");
            return !string.IsNullOrWhiteSpace(output);
        }

        // Nếu không có RunWithOutput thì có thể dùng lệnh shell và đọc output (ProcessStartInfo)
        public string RunWithOutput(string args) {
            var psi = new System.Diagnostics.ProcessStartInfo {
                FileName = "adb",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var proc = System.Diagnostics.Process.Start(psi)) {
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return output;
            }
        }
    }
}
