using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LUDUS.Services;

namespace LUDUS {
    public partial class Form1 : Form {
        private readonly AdbService _adb;
        private readonly DeviceManager _devMgr;
        private readonly AppController _appCtrl;
        private readonly ScreenCaptureService _capSvc;
       // private readonly BattleAnalyzerService _battleAnalyzer;

        public Form1() {
            InitializeComponent();

            // khởi tạo services
            _adb = new AdbService();
            _devMgr = new DeviceManager(_adb);
            _appCtrl = new AppController(_adb);
            _capSvc = new ScreenCaptureService();
            string xmlPath = System.IO.Path.Combine(Application.StartupPath, "Main_Screen", "regions.xml");
            string outFolder = System.IO.Path.Combine(Application.StartupPath, "Screenshots", "Cells");
            //_battleAnalyzer = new BattleAnalyzerService(xmlPath, outFolder);

            // wiring
            btnConnect.Click += BtnConnect_Click;
            btnOpenApp.Click += BtnOpenApp_Click;
            btnCloseApp.Click += BtnCloseApp_Click;
            btnAnalyzeBattle.Click += BtnAnalyzeBattle_Click;
            btnCompare.Click += BtnCompareCells_Click;

            // load devices
            LoadDevices();
        }


        private void LoadDevices() {
            _devMgr.Refresh();
            cmbDevices.Items.Clear();
            cmbDevices.Items.AddRange(_devMgr.Devices.ToArray());
            if (_devMgr.Devices.Count > 0)
                cmbDevices.SelectedItem = _devMgr.CurrentDevice;
            Log("Devices loaded.");
        }

        private void BtnConnect_Click(object sender, EventArgs e) {
            var dev = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(dev)) { Log("Select device first."); return; }
            if (_devMgr.Connect(dev)) Log($"Connected to {dev}");
            else Log($"Failed to connect to {dev}");
            LoadDevices();
        }

        private void BtnOpenApp_Click(object sender, EventArgs e) {
            var dev = _devMgr.CurrentDevice;
            if (dev != null && _appCtrl.Open(dev, "com.studion.mergearena"))
                Log("App opened.");
            else
                Log("Open app failed.");
        }

        private void BtnCloseApp_Click(object sender, EventArgs e) {
            var dev = _devMgr.CurrentDevice;
            if (dev != null && _appCtrl.Close(dev, "com.studion.mergearena"))
                Log("App closed.");
            else
                Log("Close app failed.");
        }

        private void BtnAnalyzeBattle_Click(object sender, EventArgs e) {
            var dev = _devMgr.CurrentDevice;
            if (string.IsNullOrEmpty(dev)) {
                Log("⚠️ Chưa chọn thiết bị.");
                return;
            }

            try {
                using (var screenshot = (Bitmap)_capSvc.Capture(dev))
                using (var analyzer = new BattleAnalyzerService(
                    regionsXmlPath: Path.Combine(Application.StartupPath, "Main_Screen", "regions.xml"),
                    templatesFolder: Path.Combine(Application.StartupPath, "Templates"),
                    outputFolder: Path.Combine(Application.StartupPath, "Screenshots", "Cells"),
                    matchThreshold: 0.85)) {
                    var results = analyzer.AnalyzeBattleScreen(screenshot);

                    foreach (var kv in results) {
                        if (kv.Value.Count == 1 && kv.Value[0] == "Empty")
                            Log($"{kv.Key}: ⚪ Empty");
                        else if (kv.Value.Count == 0)
                            Log($"{kv.Key}: ❌ No match");
                        else
                            Log($"{kv.Key}: ✅ {string.Join(", ", kv.Value)}");
                    }
                }
            } catch (Exception ex) {
                Log($"❌ Error in AnalyzeBattle: {ex.Message}");
            }
        }


        private void BtnCompareCells_Click(object sender, EventArgs e) {
            // 1) Load tất cả cell_X_Y.png từ folder RawCells
            string rawDir = Path.Combine(Application.StartupPath,
                                "Screenshots", "Cells", "RawCells");
            if (!Directory.Exists(rawDir)) {
                Log("❌ Không tìm thư mục RawCells.");
                return;
            }

            var cellBitmaps = new Dictionary<string, Bitmap>();
            foreach (string file in Directory.GetFiles(rawDir, "cell_*.png")) {
                string key = Path.GetFileNameWithoutExtension(file);
                Bitmap bmp = new Bitmap(file);
                cellBitmaps[key] = bmp;
            }

            if (cellBitmaps.Count < 2) {
                Log("⚠️ Ít hơn 2 ô, không thể so sánh.");
                foreach (var bmp in cellBitmaps.Values)
                    bmp.Dispose();
                return;
            }

            // 2) So sánh
            List<Tuple<string, string, double>> matches;
            using (var comparer = new CellComparisonService(
                                        similarityThreshold: 0.25,
                                        distanceThreshold: 30,
                                        maxFeatures: 500)) {
                matches = comparer.Compare(cellBitmaps);
            }

            // 3) Log kết quả
            if (matches.Count == 0) {
                Log("🔍 Không tìm thấy ô giống nhau.");
            }
            else {
                foreach (var t in matches) {
                    Log(string.Format("✔ {0} ≈ {1} (sim={2:F2})",
                                      t.Item1, t.Item2, t.Item3));
                }
            }

            // 4) Giải phóng bitmap
            foreach (var bmp in cellBitmaps.Values)
                bmp.Dispose();
        }

        private void Log(string msg) {
            richTextBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            richTextBoxLog.ScrollToCaret();
        }
    }
}
