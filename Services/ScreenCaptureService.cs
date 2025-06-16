using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace LUDUS.Services {
    public class ScreenCaptureService {
        private readonly string _adbPath;
        public ScreenCaptureService(string adbPath = "adb") => _adbPath = adbPath;

        public Image Capture(string deviceId) {
            var psi = new ProcessStartInfo {
                FileName = _adbPath,
                Arguments = $"-s {deviceId} exec-out screencap -p",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            using (var ms = new MemoryStream()) {
                proc.StandardOutput.BaseStream.CopyTo(ms);
                proc.WaitForExit();
                ms.Position = 0;
                return Image.FromStream(ms);
            }
        }
    }
}
