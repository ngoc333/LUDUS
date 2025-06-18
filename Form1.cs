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
        private HeroNameOcrService _ocrSvc;
        private BattleAnalyzerService _battleSvc;


        public Form1() {
            InitializeComponent();

            // khởi tạo services
            _adb = new AdbService();
            _devMgr = new DeviceManager(_adb);
            _appCtrl = new AppController(_adb);
            _capSvc = new ScreenCaptureService();
            string xmlPath = System.IO.Path.Combine(Application.StartupPath, "Main_Screen", "regions.xml");
            string outFolder = System.IO.Path.Combine(Application.StartupPath, "Screenshots", "HeroNames");
            string regionsXml = Path.Combine(
                Application.StartupPath, "Main_Screen", "regions.xml");

            _ocrSvc = new HeroNameOcrService();
            _battleSvc = new BattleAnalyzerService(
                _adb, _capSvc, _ocrSvc, regionsXml, tapDelayMs: 100);

            // wiring
            btnConnect.Click += BtnConnect_Click;
            btnCapture.Click += (s, e) => {
                var dev = cmbDevices.SelectedItem as string;
                if (string.IsNullOrEmpty(dev)) { Log("Select device first."); return; }
                var img = _capSvc.Capture(dev);
                if (img != null) {
                    string outFile = Path.Combine(Application.StartupPath, "Screenshots", $"screen{DateTime.Now:HHmmss}.png");
                    img.Save(outFile);
                    Log($"Captured screenshot to {outFile}");
                }
                else {
                    Log("Capture failed.");
                }
            };
            btnOpenApp.Click += BtnOpenApp_Click;
            btnCloseApp.Click += BtnCloseApp_Click;
            btnAnalyzeBattle.Click += BtnAnalyzeBattle_Click;

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
            var dev = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(dev)) { Log("Select device."); return; }
            _battleSvc.AnalyzeBattle(dev, Log);

        }

        private void Log(string msg) {
            richTextBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            richTextBoxLog.ScrollToCaret();
        }
    }
}
