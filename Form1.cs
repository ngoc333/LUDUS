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
                _adb, _appCtrl, _screenSvc, _pvpNav, _battleSvc, _packageName
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
            //btnCheckRound1.Click += BtnCheckRound1_Click;
            //btnSaveLifeRegions.Click += BtnSaveLifeRegions_Click;
            //btnDebugTemplate.Click += BtnDebugTemplate_Click;
            //btnToggleRoundDetection.Click += BtnToggleRoundDetection_Click;



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

        private void BtnScreenDetect_Click(object sender, EventArgs e) {
            var dev = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(dev)) {
                Log("Select device first.");
                return;
            }
            // Gọi DetectScreen và log kết quả
            string screen = _screenSvc.DetectScreen(dev, Log);
            Log($"Detected screen: {screen}");
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

        private void BtnAnalyzeBattle_Click(object sender, EventArgs e) {
            var dev = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(dev)) { Log("Select device."); return; }
            Log("The 'Analyze Battle' button is for legacy testing and is now disabled.");
            // The old _battleSvc.AnalyzeBattle method was removed.
        }

        private async void BtnCheckRound1_Click(object sender, EventArgs e) {
            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId)) {
                Log("Vui lòng chọn thiết bị trước.");
                return;
            }

            try {
                Log("Đang kiểm tra round 1...");
                bool isRound1 = await _battleSvc.IsRound1(deviceId, Log);
                
                if (isRound1) {
                    Log("✅ Kết quả: Đây là ROUND 1");
                } else {
                    Log("❌ Kết quả: Không phải ROUND 1");
                }

                // Lấy thông tin chi tiết
                var roundInfo = await _battleSvc.GetRoundInfo(deviceId, Log);
                Log($"📊 Thông tin chi tiết: {roundInfo}");
            }
            catch (Exception ex) {
                Log($"❌ Lỗi khi kiểm tra round 1: {ex.Message}");
            }
        }

        private async void BtnSaveLifeRegions_Click(object sender, EventArgs e) {
            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId)) {
                Log("Vui lòng chọn thiết bị trước.");
                return;
            }

            try {
                Log("Đang lưu file hình Life1 và Life2...");
                bool success = await _battleSvc.SaveLifeRegions(deviceId, Log);
                
                if (success) {
                    Log("✅ Đã lưu thành công file hình Life1 và Life2");
                    Log("📁 Kiểm tra thư mục LifeRegions trong thư mục chương trình");
                } else {
                    Log("❌ Lỗi khi lưu file hình");
                }
            }
            catch (Exception ex) {
                Log($"❌ Lỗi khi lưu file hình: {ex.Message}");
            }
        }

        private async void BtnDebugTemplate_Click(object sender, EventArgs e) {
            try {
                Log("Đang debug template lifeEmpty.png...");
                bool success = await _battleSvc.SaveTemplateForDebug(Log);
                
                if (success) {
                    Log("✅ Đã lưu thành công template debug");
                    Log("📁 Kiểm tra thư mục Debug trong thư mục chương trình");
                } else {
                    Log("❌ Lỗi khi debug template");
                }
            }
            catch (Exception ex) {
                Log($"❌ Lỗi khi debug template: {ex.Message}");
            }
        }

        private void Log(string msg) {
            if (richTextBoxLog.InvokeRequired) {
                richTextBoxLog.Invoke(new Action(() =>
                {
                    richTextBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                    richTextBoxLog.ScrollToCaret();
                }));
                return;
            }
            richTextBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            richTextBoxLog.ScrollToCaret();
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
