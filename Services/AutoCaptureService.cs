using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using LUDUS.Utils;
using System.Linq;

namespace LUDUS.Services {
    public class AutoCaptureService {
        private readonly DeviceManager _devMgr;
        private readonly ScreenCaptureService _capSvc;
        private readonly ScreenRecognitionService _recSvc;
        private readonly string _folder;
        private Timer _timer;

        public event Action<string> OnLog;
        public event Action<string[]> OnRecognized;

        public AutoCaptureService(
            DeviceManager devMgr,
            ScreenCaptureService capSvc,
            ScreenRecognitionService recSvc,
            string folder) {
            _devMgr = devMgr;
            _capSvc = capSvc;
            _recSvc = recSvc;
            _folder = folder;
            if (!Directory.Exists(_folder))
                Directory.CreateDirectory(_folder);
        }

        public void Start(int intervalMs = 3000) {
            _timer = new Timer { Interval = intervalMs };
            _timer.Tick += (s, e) => CaptureAndRecognize();
            _timer.Start();
            Log("Auto capture started.");
        }

        public void Stop() {
            _timer?.Stop();
            Log("Auto capture stopped.");
        }

        private void CaptureAndRecognize() {
            var dev = _devMgr.CurrentDevice;
            if (string.IsNullOrEmpty(dev)) {
                Log("No device selected.");
                return;
            }

            string outFile = Path.Combine(_folder, "latest.png");
            try {
                // Capture
                var psi = new ProcessStartInfo("adb", $"-s {dev} exec-out screencap -p") {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                using (var fs = new FileStream(outFile, FileMode.Create, FileAccess.Write)) {
                    p.StandardOutput.BaseStream.CopyTo(fs);
                    p.WaitForExit();
                }
                Log($"Saved screenshot: {outFile}");

                // Recognize
                using (var bmp = new Bitmap(outFile)) {
                    List<string> found = _recSvc.Recognize(bmp).ToList();
                    if (found.Count == 0)
                        Log("No regions recognized.");
                    else
                        Log("Recognized: " + string.Join(", ", found));
                    OnRecognized?.Invoke(found.ToArray());
                }
            } catch (Exception ex) {
                Log("Error in CaptureAndRecognize: " + ex.Message);
            }
        }

        private void Log(string msg) => OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }
}
