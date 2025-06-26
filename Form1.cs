using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LUDUS.Logic;
using LUDUS.Services;

namespace LUDUS {
    public partial class Form1 : Form {
        private readonly AdbService _adb;
        private readonly DeviceManager _devMgr;
        private readonly AppController _appCtrl;
        private readonly ScreenCaptureService _capSvc;
        private readonly HeroMergeService _mergeService;
        private readonly HeroNameOcrService _ocrSvc;
        private readonly BattleAnalyzerService _battleSvc;
        private readonly ScreenDetectionService _screenSvc;
        private readonly LudusAutoService _ludusAutoService;
        private readonly PvpNavigationService _pvpNav;
        private readonly string _packageName = "com.studion.mergearena";

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isAutoRunning = false;

        public Form1() {
            InitializeComponent();

            // khởi tạo services
            _adb = new AdbService();
            _devMgr = new DeviceManager(_adb);
            _appCtrl = new AppController(_adb);
            _capSvc = new ScreenCaptureService();
            _mergeService = new HeroMergeService(_adb);
            
            string xmlPath = Path.Combine(Application.StartupPath, "regions.xml");
            string outFolder = Path.Combine(Application.StartupPath, "Screenshots", "HeroNames");
            string templatesFolder = Path.Combine(Application.StartupPath, "Templates");

            _pvpNav = new PvpNavigationService(
                _adb, _capSvc, xmlPath, templatesFolder
            );

            _screenSvc = new ScreenDetectionService(
                _capSvc, _adb, xmlPath, templatesFolder
            );

            _ocrSvc = new HeroNameOcrService();
            _battleSvc = new BattleAnalyzerService(
                 _capSvc, _adb, _ocrSvc, _mergeService, xmlPath, templatesFolder, _screenSvc);
            
            _ludusAutoService = new LudusAutoService(
                _adb, _appCtrl, _screenSvc, _pvpNav, _battleSvc, _capSvc, _packageName
            );
            _ludusAutoService.SetResultLogger(UpdateResultUI);

            // wiring
            btnCapture.Click += (s, e) => {
                var dev = cmbDevices.SelectedItem as string;
                if (string.IsNullOrEmpty(dev)) { Log("Select device first."); return; }
                var img = _capSvc.Capture(dev);
                if (img != null) {
                    string outFile = Path.Combine(Application.StartupPath, "Screenshots", $"screen{DateTime.Now:yyyyMMddHHmmss}.png");
                    img.Save(outFile);
                    Log($"Captured screenshot to {outFile}");
                }
                else {
                    Log("Capture failed.");
                }
            };
            btnOpenApp.Click += BtnOpenApp_Click;
            btnCloseApp.Click += BtnCloseApp_Click;
            btnStart.Click += BtnStart_Click;

            // load devices
            LoadDevices();
        }

        private async void BtnStart_Click(object sender, EventArgs e) {
            if (_isAutoRunning)
            {
                // User wants to stop
                Log("Stop request received. Shutting down gracefully...");
                _cancellationTokenSource?.Cancel();
                btnStart.Enabled = false; // Disable until the task is fully cancelled
                return;
            }

            // User wants to start
            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId))
            {
                Log("Please select a device first.");
                return;
            }

            _isAutoRunning = true;
            btnStart.Text = "Stop";
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                Log("Starting auto service...");
                await _ludusAutoService.RunAsync(deviceId, _ocrSvc.Recognize, Log, _cancellationTokenSource.Token);
                Log("Auto service finished gracefully.");
            }
            catch (OperationCanceledException)
            {
                Log("Auto service was stopped by the user.");
            }
            catch (Exception ex)
            {
                Log($"An error occurred: {ex.Message}");
            }
            finally
            {
                _isAutoRunning = false;
                btnStart.Text = "Start";
                btnStart.Enabled = true;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void LoadDevices() {
            _devMgr.Refresh();
            cmbDevices.Items.Clear();
            cmbDevices.Items.AddRange(_devMgr.Devices.ToArray());
            if (_devMgr.Devices.Count > 0)
                cmbDevices.SelectedItem = _devMgr.CurrentDevice;
            Log("Devices loaded.");
        }

        private void BtnOpenApp_Click(object sender, EventArgs e) {
            var dev = _devMgr.CurrentDevice;
            if (dev != null && _appCtrl.Open(dev, _packageName))
                Log("App opened.");
            else
                Log("Open app failed.");
        }

        private void BtnCloseApp_Click(object sender, EventArgs e) {
            var dev = _devMgr.CurrentDevice;
            if (dev != null && _appCtrl.Close(dev, _packageName))
                Log("App closed.");
            else
                Log("Close app failed.");
        }

        private void Log(string msg) {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] {msg}";

            if (richTextBoxLog.InvokeRequired) {
                richTextBoxLog.Invoke(new Action(() =>
                {
                    richTextBoxLog.AppendText($"{logMessage}{Environment.NewLine}");
                    richTextBoxLog.ScrollToCaret();
                }));
            }
            else {
                richTextBoxLog.AppendText($"{logMessage}{Environment.NewLine}");
                richTextBoxLog.ScrollToCaret();
            }

            // Lưu vào file
            try {
                string logDir = Path.Combine(Application.StartupPath, "Log");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, $"debug_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, $"{logMessage}{Environment.NewLine}");
            } catch { }
        }

        private void UpdateResultUI(string resultLine)
        {
            if (richTextBoxResult.InvokeRequired)
            {
                richTextBoxResult.Invoke(new Action(() =>
                {
                    richTextBoxResult.AppendText($"{resultLine}{Environment.NewLine}");
                    richTextBoxResult.ScrollToCaret();
                    lblWin.Text = $"Thắng: {_ludusAutoService.WinCount}";
                    lblLose.Text = $"Thua: {_ludusAutoService.LoseCount}";
                }));
            }
            else
            {
                richTextBoxResult.AppendText($"{resultLine}{Environment.NewLine}");
                richTextBoxResult.ScrollToCaret();
                lblWin.Text = $"Thắng: {_ludusAutoService.WinCount}";
                lblLose.Text = $"Thua: {_ludusAutoService.LoseCount}";
            }
        }

    }
}
