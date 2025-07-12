using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LUDUS.Logic;
using LUDUS.Services;
using LUDUS.Utils;
using System.Threading.Tasks;

namespace LUDUS {
    public partial class Form1 : Form {
        private readonly AdbService _adb;
        private readonly DeviceManager _devMgr;
        private readonly AppController _appCtrl;
        private readonly ScreenCaptureService _capSvc;
        private readonly HeroMergeService _mergeService;
        private readonly HeroNameOcrService _ocrSvc;
        private readonly BattleService _battleSvc;
        private readonly ScreenDetectionService _screenSvc;
        private readonly LudusAutoService _ludusAutoService;
        private readonly PvpNavigationService _pvpNav;
        private readonly string _packageName = "com.studion.mergearena";

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isAutoRunning = false;
        private bool _isAppRunning = false; // trạng thái app đóng/mở

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
            _battleSvc = new BattleService(
                 _capSvc, _adb, _ocrSvc, _mergeService, xmlPath, templatesFolder, _screenSvc);
            
            _ludusAutoService = new LudusAutoService(
                _adb, _devMgr, _appCtrl, _screenSvc, _pvpNav, _battleSvc, _capSvc, _packageName
            );
            _ludusAutoService.SetResultLogger(UpdateResultUI);

            // wiring
            btnCapture.Click += (s, e) => {
                var dev = cmbDevices.SelectedItem as string;
                if (string.IsNullOrEmpty(dev)) { 
                    Log("Không có thiết bị nào được chọn. Đang thử refresh devices...");
                    RefreshDevices();
                    return; 
                }
                var img = _capSvc.Capture(dev);
                if (img != null) {
                    string outFile = Path.Combine(Application.StartupPath, "Screenshots", $"screen{DateTime.Now:yyyyMMddHHmmss}.png");
                    img.Save(outFile);
                    Log($"Captured screenshot to {outFile}");
                }
                else {
                    Log("Capture failed. Có thể thiết bị đã ngắt kết nối. Đang thử refresh devices...");
                    RefreshDevices();
                }
            };
            btnOpenApp.Click += BtnOpenApp_Click;
            // Thiết lập nút đóng/mở app
            btnOpenClose.Text = "Open App";
            btnOpenClose.Click += BtnOpenClose_Click;
            btnStartAstra.Click += BtnStartAstra_Click;
            btnStartPvp.Click += BtnStarPvp_Click;

            // Thêm event handler cho numLoseCount
            numLoseCount.ValueChanged += (s, e) => {
                if (_ludusAutoService != null)
                {
                    _ludusAutoService.UpdateLoseModeFromUI((int)numLoseCount.Value);
                }
            };

            // Thêm event handler cho numWin và numLose
            numWin.ValueChanged += (s, e) => {
                if (_ludusAutoService != null)
                {
                    _ludusAutoService.UpdateWinLoseSettings((int)numWin.Value, (int)numLose.Value);
                }
            };

            numLose.ValueChanged += (s, e) => {
                if (_ludusAutoService != null)
                {
                    _ludusAutoService.UpdateWinLoseSettings((int)numWin.Value, (int)numLose.Value);
                }
            };

            // Khi người dùng thay đổi lựa chọn thiết bị, cập nhật trạng thái nút Open/Close App
            cmbDevices.SelectionChangeCommitted += (s, e) => {
                SyncAppRunningStatus();
            };

            // load devices
            LoadDevices();
            SyncAppRunningStatus();
        }

        private Button _runningButton = null; // nút đang chạy hiện tại

        // Kiểm tra app đang chạy để cập nhật nút Close/Open
        private void SyncAppRunningStatus()
        {
            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId))
            {
                _isAppRunning = false;
                btnOpenClose.Text = "Open App";
                return;
            }

            try
            {
                _isAppRunning = _adb.IsAppRunning(deviceId, _packageName);
                btnOpenClose.Text = _isAppRunning ? "Close App" : "Open App";
            }
            catch (Exception ex)
            {
                Log($"Lỗi khi kiểm tra trạng thái app: {ex.Message}");
                _isAppRunning = false;
                btnOpenClose.Text = "Open App";
            }
        }

        private async void StartAutoMode(Button triggerButton) {
            if (_isAutoRunning) {
                // Nếu người dùng bấm nút đang chạy => dừng
                if (triggerButton != _runningButton) return; // Chỉ dừng khi nhấn đúng nút

                Log("Stop request received. Shutting down gracefully...");
                _cancellationTokenSource?.Cancel();
                if (_runningButton != null)
                    _runningButton.Enabled = false; // Disable until the task is fully cancelled
                _adb.StopShell(); // Dừng persistent shell khi stop
                try {
                    // Đợi một chút để task có thể cancel
                    await Task.Delay(100);
                } catch { }

                _isAutoRunning = false;
                ResetStartButtons();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                return;
            }

            // User wants to start
            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId)) {
                Log("Không có thiết bị nào được chọn. Đang thử refresh devices...");
                RefreshDevices();

                // Thử lấy lại thiết bị sau khi refresh
                deviceId = cmbDevices.SelectedItem as string;
                if (string.IsNullOrEmpty(deviceId)) {
                    Log("Vẫn không tìm thấy thiết bị nào. Vui lòng kiểm tra LDPlayer và thử lại.");
                    return;
                }
            }

            _isAutoRunning = true;
            _runningButton = triggerButton;
            _runningButton.Text = "Stop";

            // Disable nút còn lại trong khi chạy
            if (_runningButton == btnStartAstra) btnStartPvp.Enabled = false;
            else btnStartAstra.Enabled = false;

            _cancellationTokenSource = new CancellationTokenSource();

            _adb.StartShell(deviceId); // Khởi tạo persistent shell khi start

            // Khởi tạo cài đặt win/lose từ UI
            if (_ludusAutoService != null)
            {
                _ludusAutoService.UpdateWinLoseSettings((int)numWin.Value, (int)numLose.Value);
            }

        RESTART_AUTO:
            try {
                Log("Starting auto service...");
                await _ludusAutoService.RunAsync(deviceId, _ocrSvc.Recognize, Log, _cancellationTokenSource.Token);
                Log("Auto service finished gracefully.");
            } catch (OperationCanceledException) {
                Log("Auto service stopped by user.");
            } catch (Exception ex) {
                Log($"An error occurred: {ex.Message}");
                if (_isAutoRunning && (_cancellationTokenSource == null || !_cancellationTokenSource.IsCancellationRequested)) {
                    Log("Thử khởi động lại app...");
                    bool appOk = await RestartAppOnlyAsync(deviceId);
                    if (appOk) {
                        // đảm bảo shell active
                        _adb.StartShell(deviceId);
                        goto RESTART_AUTO;
                    }

                    Log("Không thể khởi động lại app, thử khởi động lại LDPlayer...");
                    _adb.StopShell();
                    string newDevice = await RestartEmulatorAndAppAsync();
                    if (!string.IsNullOrEmpty(newDevice))
                    {
                        deviceId = newDevice;
                        _adb.StartShell(deviceId);
                        goto RESTART_AUTO;
                    }
                    else
                    {
                        Log("Không thể khởi động lại LDPlayer/app. Dừng auto.");
                    }
                }
            } finally {
                _isAutoRunning = false;
                ResetStartButtons();
                _runningButton = null;
                _adb.StopShell(); // Đảm bảo dừng shell khi kết thúc
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void ResetStartButtons() {
            btnStartAstra.Text = "Start Astra";
            btnStartPvp.Text = "Start PVP";
            btnStartAstra.Enabled = true;
            btnStartPvp.Enabled = true;
        }

        private void LoadDevices() {
            _devMgr.Refresh();
            cmbDevices.Items.Clear();
            cmbDevices.Items.AddRange(_devMgr.Devices.ToArray());
            if (_devMgr.Devices.Count > 0)
                cmbDevices.SelectedItem = _devMgr.CurrentDevice;
            Log($"Devices loaded: {_devMgr.Devices.Count} thiết bị tìm thấy.");
            SyncAppRunningStatus();
        }

        private void RefreshDevices() {
            try
            {
                Log("Đang kiểm tra và load lại danh sách thiết bị...");
                
                // Refresh danh sách thiết bị
                _devMgr.Refresh();
                
                // Lưu thiết bị đang được chọn
                string currentSelectedDevice = cmbDevices.SelectedItem as string;
                
                // Clear và load lại combobox
                cmbDevices.Items.Clear();
                cmbDevices.Items.AddRange(_devMgr.Devices.ToArray());
                
                if (_devMgr.Devices.Count > 0)
                {
                    // Thử chọn lại thiết bị cũ nếu vẫn tồn tại
                    if (!string.IsNullOrEmpty(currentSelectedDevice) && _devMgr.Devices.Contains(currentSelectedDevice))
                    {
                        cmbDevices.SelectedItem = currentSelectedDevice;
                        Log($"✅ Đã tìm thấy {_devMgr.Devices.Count} thiết bị. Giữ nguyên thiết bị đã chọn: {currentSelectedDevice}");
                    }
                    else
                    {
                        // Chọn thiết bị đầu tiên
                        cmbDevices.SelectedItem = _devMgr.CurrentDevice;
                        Log($"✅ Đã tìm thấy {_devMgr.Devices.Count} thiết bị. Đã chọn: {cmbDevices.SelectedItem}");
                    }
                }
                else
                {
                    Log("❌ Không tìm thấy thiết bị nào. Vui lòng kiểm tra:");
                }

                // Sau khi refresh, đồng bộ trạng thái app
                SyncAppRunningStatus();
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi khi refresh devices: {ex.Message}");
            }
        }

        /// <summary>
        /// Phương thức public để refresh devices từ bên ngoài
        /// </summary>
        public void RefreshDevicesPublic()
        {
            RefreshDevices();
        }

        private async void BtnOpenApp_Click(object sender, EventArgs e) {
            try
            {
                Log("Đang khởi động LDPlayer...");
                
                // Đường dẫn mặc định của LDPlayer (có thể thay đổi theo cài đặt)
                string ldPlayerPath = @"C:\LDPlayer\LDPlayer9\dnplayer.exe";
                
                // Kiểm tra xem LDPlayer đã được cài đặt chưa
                if (!File.Exists(ldPlayerPath))
                {
                    // Thử đường dẫn khác
                    ldPlayerPath = @"C:\Program Files (x86)\LDPlayer\LDPlayer9\dnplayer.exe";
                    if (!File.Exists(ldPlayerPath))
                    {
                        Log("Không tìm thấy LDPlayer. Vui lòng kiểm tra đường dẫn cài đặt.");
                        return;
                    }
                }
                
                // Khởi động LDPlayer
                var process = System.Diagnostics.Process.Start(ldPlayerPath);
                
                // Chờ một chút để LDPlayer khởi động
                await Task.Delay(2000);
                
                // Tìm và minimize cửa sổ LDPlayer
                try
                {
                    var ldPlayerProcesses = System.Diagnostics.Process.GetProcessesByName("dnplayer");
                    foreach (var proc in ldPlayerProcesses)
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            // Minimize cửa sổ
                            Win32.ShowWindow(proc.MainWindowHandle, Win32.SW_MINIMIZE);
                            Log("Đã minimize cửa sổ LDPlayer.");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Không thể minimize cửa sổ LDPlayer: {ex.Message}");
                }
                
                Log("Đã khởi động LDPlayer. Đang chờ giả lập khởi động hoàn tất...");
                
                // Chờ và kiểm tra thiết bị
                await WaitForDeviceAndLoad();
            }
            catch (Exception ex)
            {
                Log($"Lỗi khi khởi động LDPlayer: {ex.Message}");
            }
        }

        private async Task WaitForDeviceAndLoad()
        {
            try
            {
                Log("Đang chờ LDPlayer khởi động và kết nối ADB...");
                
                int maxWaitTime = 60; // Tối đa 1 phút
                int waited = 0;
                int checkInterval = 3000; // Kiểm tra mỗi 3 giây
                
                while (waited < maxWaitTime * 1000)
                {
                    // Refresh danh sách thiết bị (off UI thread)
                    await Task.Run(() => _devMgr.Refresh());
                    
                    if (_devMgr.Devices.Count > 0)
                    {
                        Log($"✅ Tìm thấy {_devMgr.Devices.Count} thiết bị!");
                        
                        // Load devices vào combobox
                        LoadDevices();
                        
                        // Chọn thiết bị đầu tiên nếu chưa có thiết bị nào được chọn
                        if (cmbDevices.SelectedItem == null && cmbDevices.Items.Count > 0)
                        {
                            cmbDevices.SelectedIndex = 0;
                            Log($"Đã tự động chọn thiết bị: {cmbDevices.SelectedItem}");
                        }
                        
                        Log("LDPlayer đã sẵn sàng sử dụng!");
                        return;
                    }
                    
                    Log($"Chờ LDPlayer khởi động... ({waited / 1000}s/{maxWaitTime}s)");
                    await Task.Delay(checkInterval);
                    waited += checkInterval;
                }
                
                // Nếu timeout, vẫn load devices để kiểm tra
                Log("⚠️ Timeout chờ LDPlayer. Vẫn thử load devices...");
                LoadDevices();
                
                if (_devMgr.Devices.Count == 0)
                {
                    Log("❌ Không tìm thấy thiết bị nào. Vui lòng kiểm tra:");
                    Log("   - LDPlayer đã khởi động hoàn toàn chưa?");
                    Log("   - ADB đã được cài đặt và hoạt động chưa?");
                    Log("   - Có thể thử nhấn nút 'Load Devices' để kiểm tra lại.");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi khi chờ thiết bị: {ex.Message}");
                // Vẫn thử load devices
                LoadDevices();
            }
        }

        private void BtnStartAstra_Click(object sender, EventArgs e) {
            _ludusAutoService.SetPreferPvp(false);
            StartAutoMode(btnStartAstra);
        }

        private void BtnStarPvp_Click(object sender, EventArgs e) {
            _ludusAutoService.SetPreferPvp(true);
            StartAutoMode(btnStartPvp);
        }

        // Đóng/mở app theo nút btnOpenClose
        private async void BtnOpenClose_Click(object sender, EventArgs e)
        {
            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId))
            {
                Log("Không có thiết bị nào được chọn. Đang thử refresh devices...");
                RefreshDevices();
                deviceId = cmbDevices.SelectedItem as string;
                if (string.IsNullOrEmpty(deviceId))
                {
                    Log("Vẫn không tìm thấy thiết bị nào. Vui lòng kiểm tra LDPlayer và thử lại.");
                    return;
                }
            }

            if (_isAppRunning)
            {
                Log("Đang đóng app...");
                var closed = _appCtrl.Close(deviceId, _packageName);
                if (closed)
                {
                    Log("Đã đóng app thành công.");
                    _isAppRunning = false;
                    btnOpenClose.Text = "Open App";
                }
                else
                {
                    Log("Đóng app thất bại.");
                }
            }
            else
            {
                Log("Đang mở app...");
                var opened = _appCtrl.Open(deviceId, _packageName);
                if (opened)
                {
                    Log("Đã mở app thành công.");
                    // Sau khi mở, bắt đầu kiểm tra màn hình cho đến khi vào Main
                    btnOpenClose.Enabled = false; // khoá nút trong khi chờ
                    await WaitUntilMainScreenAsync(deviceId);
                    _isAppRunning = true;
                    btnOpenClose.Text = "Close App";
                    btnOpenClose.Enabled = true;
                }
                else
                {
                    Log("Mở app thất bại.");
                }
            }
        }

        private async Task WaitUntilMainScreenAsync(string deviceId)
        {
            // Đảm bảo shell được khởi tạo để _adb.RunShellPersistent hoạt động
            _adb.StartShell(deviceId);

            Log("🔍 Bắt đầu kiểm tra màn hình cho đến khi vào Main...");
            int waited = 0;
            int maxWait = 90000; // 120 giây
            int interval = 3000;

            while (waited < maxWait)
            {
                string screen = await _screenSvc.DetectScreenAsync(deviceId, Log);
                if (screen == "Main")
                {
                    Log("✅ Đã vào màn hình Main.");
                    return;
                }

                await Task.Delay(interval);
                waited += interval;
            }

            Log("⚠️ Quá thời gian chờ vào màn hình Main. Bạn có thể thử lại nếu cần.");
            // Dừng shell nếu không cần nữa
            _adb.StopShell();
        }

        // Khởi động lại LDPlayer và app, trả về deviceId mới nếu thành công
        private async Task<string> RestartEmulatorAndAppAsync()
        {
            Log("🔄 Đang tắt LDPlayer...");

            try
            {
                // Kill all dnplayer processes
                var processes = System.Diagnostics.Process.GetProcessesByName("dnplayer");
                foreach (var p in processes)
                {
                    try { p.Kill(); p.WaitForExit(5000); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"Lỗi khi tắt LDPlayer: {ex.Message}");
            }

            await Task.Delay(3000);

            Log("🚀 Khởi động LDPlayer...");

            // Default LDPlayer path
            string ldPlayerPath = @"C:\LDPlayer\LDPlayer9\dnplayer.exe";
            if (!System.IO.File.Exists(ldPlayerPath))
            {
                ldPlayerPath = @"C:\Program Files (x86)\LDPlayer\LDPlayer9\dnplayer.exe";
            }

            try
            {
                if (System.IO.File.Exists(ldPlayerPath))
                {
                    var proc = System.Diagnostics.Process.Start(ldPlayerPath);
                    await Task.Delay(2000);

                    // Minimize
                    try
                    {
                        var ldPlayerProcesses = System.Diagnostics.Process.GetProcessesByName("dnplayer");
                        foreach (var p in ldPlayerProcesses)
                        {
                            if (p.MainWindowHandle != IntPtr.Zero)
                            {
                                Win32.ShowWindow(p.MainWindowHandle, Win32.SW_MINIMIZE);
                                break;
                            }
                        }
                    }
                    catch { }
                }
                else
                {
                    Log("Không tìm thấy LDPlayer. Vui lòng kiểm tra đường dẫn cài đặt.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"Lỗi khi khởi động LDPlayer: {ex.Message}");
                return null;
            }

            // Wait for device available
            await WaitForDeviceAndLoad();

            var deviceId = cmbDevices.SelectedItem as string;
            if (string.IsNullOrEmpty(deviceId))
            {
                Log("❌ Không tìm thấy thiết bị sau khi khởi động lại LDPlayer.");
                return null;
            }

            // Open app
            Log("📱 Mở lại app...");
            var opened = _appCtrl.Open(deviceId, _packageName);
            if (!opened)
            {
                Log("Không thể mở app sau khi restart.");
                return null;
            }

            // Chờ vào main screen
            await WaitUntilMainScreenAsync(deviceId);

            Log("✅ LDPlayer và app đã sẵn sàng.");
            return deviceId;
        }

        private async Task<bool> RestartAppOnlyAsync(string deviceId)
        {
            Log("🔄 Đang khởi động lại app...");

            // Đảm bảo shell sẵn sàng
            _adb.StartShell(deviceId);

            // Thử đóng app
            try
            {
                _appCtrl.Close(deviceId, _packageName);
            }
            catch (Exception ex)
            {
                Log($"Lỗi khi đóng app: {ex.Message}");
            }

            await Task.Delay(2000);

            // Mở app lại
            bool opened = false;
            try
            {
                opened = _appCtrl.Open(deviceId, _packageName);
            }
            catch (Exception ex)
            {
                Log($"Lỗi khi mở lại app: {ex.Message}");
            }

            if (!opened)
            {
                Log("Không thể mở lại app.");
                return false;
            }

            // Chờ app vào Main
            _adb.StartShell(deviceId); // đảm bảo shell tồn tại khi detect
            int waited = 0;
            int maxWait = 60000;
            int interval = 3000;
            while (waited < maxWait)
            {
                string screen = await _screenSvc.DetectScreenAsync(deviceId, Log);
                if (screen == "Main")
                {
                    Log("✅ App đã vào màn hình Main.");
                    return true;
                }
                await Task.Delay(interval);
                waited += interval;
            }

            Log("⚠️ App không vào được màn hình Main sau khi restart.");
            return false;
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
                    UpdateWinLoseLabels();
                }));
            }
            else
            {
                richTextBoxResult.AppendText($"{resultLine}{Environment.NewLine}");
                richTextBoxResult.ScrollToCaret();
                UpdateWinLoseLabels();
            }
        }

        private void UpdateWinLoseLabels()
        {
            lblWin.Text = $"Thắng\n{_ludusAutoService.WinCount}";
            
            // Luôn cập nhật numLoseCount để hiển thị số trận thua còn lại
            int remainingLose = _ludusAutoService.RemainingLoseCount;
            if (numLoseCount.Value != remainingLose)
            {
                numLoseCount.Value = remainingLose;
            }
            
            lblLose.Text = $"Thua\n{_ludusAutoService.LoseCount}";
        }

        // Đảm bảo dừng shell khi đóng form
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _adb.StopShell();
            base.OnFormClosing(e);
        }
    }
}
