using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LUDUS.Services;

namespace LUDUS {
    public partial class Form1 : Form {
        private readonly AdbService _adbService;
        private readonly DeviceManager _devMgr;
        private readonly AppController _appCtrl;
        private readonly ScreenCaptureService _capSvc;
        private readonly ScreenRecognitionService _recSvc;
        private readonly AutoCaptureService _autoCap;

        public Form1() {
            InitializeComponent();

            // init services
            _adbService = new AdbService();
            _devMgr = new DeviceManager(_adbService);
            _appCtrl = new AppController(_adbService);
            _capSvc = new ScreenCaptureService();
            _recSvc = new ScreenRecognitionService(
                             Path.Combine(Application.StartupPath, "Main_Screen", "regions.xml"),
                             Path.Combine(Application.StartupPath, "screen"));
            _autoCap = new AutoCaptureService(
                             _devMgr, _capSvc, _recSvc,
                             Path.Combine(Application.StartupPath, "Screenshots"));

            // wire UI
            btnConnect.Click += (s, e) => Connect();
            btnOpenApp.Click += (s, e) => OpenApp();
            btnCloseApp.Click += (s, e) => CloseApp();
            _autoCap.OnLog += msg => richTextBoxLog.AppendText(msg + Environment.NewLine);

            // startup sequence
            Connect();
            OpenApp();
            _autoCap.Start(3000);
        }

        private void Connect() {
            _devMgr.Refresh();
            cmbDevices.Items.Clear();
            cmbDevices.Items.AddRange(_devMgr.Devices.ToArray());
            string dev = _devMgr.Devices.FirstOrDefault();
            if (dev != null && _devMgr.Connect(dev))
                Log($"Connected to {dev}");
        }

        private void OpenApp() {
            var dev = _devMgr.CurrentDevice;
            if (dev != null && _appCtrl.Open(dev, "com.studion.mergearena"))
                Log("App opened");
        }

        private void CloseApp() {
            var dev = _devMgr.CurrentDevice;
            if (dev != null && _appCtrl.Close(dev, "com.studion.mergearena"))
                Log("App closed");
        }

        private void Log(string msg) {
            richTextBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            richTextBoxLog.ScrollToCaret();
        }
    }
}
