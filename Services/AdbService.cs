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
    }
}
