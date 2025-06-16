using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LUDUS {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();

            btnConnect.Click += btnConnect_Click;
            btnCapture.Click += btnCapture_Click;
            btnLogin.Click += BtnLogin_Click;

            LoadDevices(); // Tự động load thiết bị khi mở form
        }

       

        private void Log(string text) {
            richTextBoxLog.AppendText($"[{DateTime.Now:dd-MMM HH:mm:ss}] {text}\n");
            richTextBoxLog.ScrollToCaret();
        }

        private void LoadDevices() {
            try {
                var psi = new ProcessStartInfo("adb", "devices") {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi)) {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    cmbDevices.Items.Clear();

                    foreach (var line in lines) {
                        if (line.Contains("device") && !line.StartsWith("List of devices")) {
                            var parts = line.Split('\t');
                            if (parts.Length >= 2 && parts[1].Trim() == "device") {
                                cmbDevices.Items.Add(parts[0].Trim());
                            }
                        }
                    }

                    if (cmbDevices.Items.Count > 0)
                        cmbDevices.SelectedIndex = 0;

                    Log("Đã tải danh sách thiết bị.");
                }
            } catch (Exception ex) {
                Log("ADB lỗi: " + ex.Message);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e) {
            try {
                
                var psi = new ProcessStartInfo("adb", $"connect {cmbDevices.SelectedItem}") {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi)) {
                    process.WaitForExit();
                }

                //LoadDevices(); // Reload sau khi connect
                Log("Đã kết nối LDPlayer.");
            } catch (Exception ex) {
                Log("Lỗi kết nối ADB: " + ex.Message);
            }
        }

        private void btnCapture_Click(object sender, EventArgs e) {
            if (cmbDevices.SelectedItem == null) {
                Log("Vui lòng chọn thiết bị.");
                return;
            }

            string deviceId = cmbDevices.SelectedItem.ToString();
            string fileName = "screen.png";

            try {
                var psi = new ProcessStartInfo {
                    FileName = "adb",
                    Arguments = $"-s {deviceId} exec-out screencap -p",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write)) {
                    process.StandardOutput.BaseStream.CopyTo(fs);
                    process.WaitForExit();
                }

                if (File.Exists(fileName)) {
                    byte[] bytes = File.ReadAllBytes(fileName);
                    using (var ms = new MemoryStream(bytes)) {
                        pictureBox1.Image = Image.FromStream(ms);
                    }

                    Log("Chụp màn hình thành công.");
                }
                else {
                    Log("Không tìm thấy file ảnh.");
                }
            } catch (Exception ex) {
                Log("Lỗi khi chụp ảnh: " + ex.Message);
            }
        }

        private void BtnLogin_Click(object sender, EventArgs e) {
            OpenAppOnLDPlayer(cmbDevices.SelectedItem.ToString(), "com.studion.mergearena");
        }

        private void OpenAppOnLDPlayer(string deviceId, string packageName) {
            var psi = new ProcessStartInfo {
                FileName = "adb",
                Arguments = $"-s {deviceId} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = psi };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Log("Output: " + output);
            Log("Error: " + error);
        }
    }
}
