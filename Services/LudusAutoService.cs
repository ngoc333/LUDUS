using System;
using System.Drawing;
using System.Threading.Tasks;
using LUDUS.Services;
using System.Threading;
using System.IO;
using System.Linq;

namespace LUDUS.Logic
{
    public class LudusAutoService
    {
        private readonly AdbService _adb;
        private readonly DeviceManager _deviceManager;
        private readonly AppController _appCtrl;
        private readonly ScreenDetectionService _screenSvc;
        private readonly PvpNavigationService _pvpNav;
        private readonly BattleAnalyzerService _battleSvc;
        private readonly ScreenCaptureService _captureSvc;
        private readonly string _packageName;
        private DateTime _lastDefaultScreenTime = DateTime.MinValue;
        private int _winCount = 0;
        private int _loseCount = 0;
        private DateTime _battleStartTime;
        private DateTime _battleEndTime;
        private Action<string> _resultLogger;
        private bool _shouldSurrenderNext = false;
        private bool _isSurrendered = false;
        private bool _loseMode = false;
        private int _targetLoseCount = 0;
        private bool _shouldSurrenderForTotalLose = false;
        private int _restartCount = 0;
        private const int MaxRestartPerSession = 2;

        public LudusAutoService(
            AdbService adb,
            DeviceManager deviceManager,
            AppController appController,
            ScreenDetectionService screenDetectionService,
            PvpNavigationService pvpNav,
            BattleAnalyzerService battleAnalyzerService,
            ScreenCaptureService captureService,
            string packageName)
        {
            _adb = adb;
            _deviceManager = deviceManager;
            _appCtrl = appController;
            _screenSvc = screenDetectionService;
            _pvpNav = pvpNav;
            _battleSvc = battleAnalyzerService;
            _captureSvc = captureService;
            _packageName = packageName;
        }

        public void SetResultLogger(Action<string> resultLogger)
        {
            _resultLogger = resultLogger;
        }

        public void SetLoseMode(bool enabled, int targetLoseCount)
        {
            _loseMode = enabled;
            _targetLoseCount = targetLoseCount;
            
            // Nếu bật chế độ thua, ngay lập tức bỏ cuộc trận đầu tiên
            if (enabled && targetLoseCount > 0)
            {
                _shouldSurrenderForTotalLose = true;
            }
            else
            {
                _shouldSurrenderForTotalLose = false;
            }
        }

        public void CheckAndUpdateLoseMode(int currentTargetLoseCount)
        {
            // Kiểm tra xem user có thay đổi số lượng trận thua không
            if (currentTargetLoseCount != _targetLoseCount)
            {
                SetLoseMode(currentTargetLoseCount > 0, currentTargetLoseCount);
            }
        }

        public void UpdateLoseModeFromUI(int targetLoseCount)
        {
            // Cập nhật chế độ thua từ UI
            SetLoseMode(targetLoseCount > 0, targetLoseCount);
        }

        public async Task RunAsync(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (!EnsureAppRunning(deviceId, log))
                    {
                        log("App is not running. Opening...");
                        _appCtrl.Open(deviceId, _packageName);
                        log("App opened. Waiting 45s for loading...");
                        await Task.Delay(45000, cancellationToken);
                        // After opening, we should re-check everything from the start.
                        continue;
                    }
                    
                    if (cancellationToken.IsCancellationRequested) break;

                    //if (_screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log))
                    //{
                    //    log("Loading screen detected. Waiting for it to finish...");
                    //    await WaitForLoading(deviceId, ocrFunc, log, cancellationToken);
                    //    // After loading, restart loop to detect the new screen.
                    //    continue;
                    //}

                    if (cancellationToken.IsCancellationRequested) break;

                    string screen = _screenSvc.DetectScreen(deviceId, log);

                    switch (screen)
                    {
                        case "Main":
                            _lastDefaultScreenTime = DateTime.MinValue;
                            log("Main screen detected. Navigating to PVP.");
                            
                            // Kiểm tra và bật chế độ thua trước khi vào PVP
                            if (_loseMode && _targetLoseCount > 0)
                            {
                                log($"Chế độ thua đang bật: {_targetLoseCount} lượt còn lại");
                                _shouldSurrenderForTotalLose = true;
                            }
                            
                            _pvpNav.GoToPvp(deviceId, log);
                            await Task.Delay(3000, cancellationToken);
                            break;
                        case "Loading":
                            log("Loading screen detected");
                            await Task.Delay(3000, cancellationToken);
                            break;
                        case "ToBattle":
                        case "Battle":
                            if (_shouldSurrenderNext || _shouldSurrenderForTotalLose)
                            {
                                log("Sẽ tự động bỏ cuộc trận này!");
                                _isSurrendered = true;
                                await _battleSvc.ClickLoseAndYes(deviceId, log);
                                _battleStartTime = DateTime.Now; // Reset thời gian bắt đầu khi vào round 1
                                await Task.Delay(3000, cancellationToken);
                                break;
                            }
                            
                            // Reset flag surrender khi bắt đầu trận mới
                            _isSurrendered = false;
                            
                            // Tính round từ lifeEmpty
                            int calculatedRound = 1; // Giá trị mặc định
                            RoundInfo roundInfo = null;
                            
                            roundInfo = await GetRoundInfoAndLog(deviceId, log);
                            calculatedRound = roundInfo.CalculatedRound;
                            // Nếu đang vào giữa trận mà _battleStartTime chưa có, thì gán luôn để tránh lặp EndBattle
                            if (_battleStartTime == DateTime.MinValue && calculatedRound != 1)
                            {
                                _battleStartTime = DateTime.Now;
                                log($"Đồng bộ _battleStartTime khi vào giữa trận: {calculatedRound}");
                            }
                            
                            if (calculatedRound == 1) {
                                _battleStartTime = DateTime.Now; // Reset thời gian bắt đầu khi vào round 1
                                log($"Bắt đầu trận mới lúc: {_battleStartTime:HH:mm:ss}");
                            }
                            _lastDefaultScreenTime = DateTime.MinValue;
                            
                            log($">>> ROUND {calculatedRound} <<<");
                            
                            await _battleSvc.ClickSpell(deviceId, calculatedRound, log);

                            if (calculatedRound == 1)
                            {
                                await _battleSvc.ClickCoin(deviceId, 20, log);
                            }
                            else
                            {
                                if (await _battleSvc.IsInBattleScreen(deviceId, log))
                                {
                                    await _battleSvc.ClickCoin(deviceId, 10, log);
                                    bool merged = await _battleSvc.AnalyzeAndMerge(deviceId, log);
                                    if (!merged)
                                    {
                                        log("No merge");
                                    }
                                }
                            }
                            await Task.Delay(1000, cancellationToken);
                            await _battleSvc.ClickEndRound(deviceId, log);
                            await Task.Delay(3000, cancellationToken);
                            
                            // Kiểm tra thêm 1 lần nữa sau khi click EndRound
                            //if (await _battleSvc.IsInBattleScreen(deviceId, log))
                            //{
                            //    log("Vẫn còn ở màn hình Battle, kiểm tra merge thêm 1 lần nữa...");
                            //    bool merged = await _battleSvc.AnalyzeAndMerge(deviceId, log);
                            //    if (merged)
                            //    {
                            //        log("Merge thành công, click EndRound lần cuối...");
                            //        await _battleSvc.ClickEndRound(deviceId, log);
                            //        await Task.Delay(3000, cancellationToken);
                            //    }
                            //    else
                            //    {
                            //        log("No merge, chuyển sang round tiếp theo");
                            //    }
                            //}
                            //else
                            //{
                            //    log("Đã chuyển khỏi màn hình Battle");
                            //}
                            break;

                        case "EndBattle":
                            _lastDefaultScreenTime = DateTime.MinValue; 
                            _battleEndTime = DateTime.Now;
                            bool isWin = _screenSvc.DetectVictoryResult(deviceId, log);
                            
                            if (isWin)
                            {
                                _winCount++;
                                // Chức năng thua sau trận PVP thắng (giữ nguyên)
                                _shouldSurrenderNext = true;
                            }
                            else
                            {
                                // Chỉ tính thua nếu không phải surrender (thua thật sự)
                                if (!_isSurrendered)
                                {
                                    _loseCount++;
                                    
                                    // Chức năng thua theo tổng số lượt (mới) - chỉ tính thua thật sự
                                    if (_loseMode)
                                    {
                                        _targetLoseCount--; // Giảm số trận thua còn lại
                                        if (_targetLoseCount <= 0)
                                        {
                                            log("Đã đạt đủ trận thua thật sự. Tắt chế độ thua liên tục.");
                                            _loseMode = false;
                                            _shouldSurrenderForTotalLose = false;
                                            // Không reset _shouldSurrenderNext vì chức năng thua sau PVP thắng vẫn hoạt động
                                        }
                                        else
                                        {
                                            log($"Thua thật sự, {_targetLoseCount} lượt còn lại.");
                                            _shouldSurrenderForTotalLose = true; // Bỏ cuộc trận tiếp theo
                                        }
                                    }
                                }
                                else
                                {
                                    // Nếu đã surrender (bỏ cuộc chủ động), KHÔNG tính vào _loseCount
                                    // Nhưng vẫn tính vào tổng số lượt cho chức năng thua theo tổng số lượt
                                    if (_loseMode)
                                    {
                                        _targetLoseCount--; // Vẫn giảm vì đây là tổng số lượt
                                        if (_targetLoseCount <= 0)
                                        {
                                            log("Đã đạt đủ tổng số lượt. Tắt chế độ thua liên tục.");
                                            _loseMode = false;
                                            _shouldSurrenderForTotalLose = false;
                                        }
                                        else
                                        {
                                            log($"Đã bỏ cuộc, {_targetLoseCount} lượt còn lại.");
                                            _shouldSurrenderForTotalLose = true;
                                        }
                                    }
                                }
                                
                                // Reset _shouldSurrenderNext sau khi thua (chức năng thua sau PVP thắng)
                                _shouldSurrenderNext = false;
                                // Không reset _shouldSurrenderForTotalLose vì nó được xử lý riêng trong logic trên
                            }
                            
                            string result;
                            if (_isSurrendered)
                            {
                                result = "Bỏ cuộc";
                            }
                            else
                            {
                                result = isWin ? "Thắng" : "Thua";
                            }
                            
                            TimeSpan duration = (_battleStartTime != DateTime.MinValue) ? (_battleEndTime - _battleStartTime) : TimeSpan.Zero;
                            string durationStr = duration != TimeSpan.Zero ? $"{(int)duration.TotalMinutes} phút {duration.Seconds} giây" : "Không xác định";
                            string timeLog = $"Kết quả: {result} | Bắt đầu: {_battleStartTime:HH:mm:ss} | Kết thúc: {_battleEndTime:HH:mm:ss} | Thời gian: {durationStr}";
                            _resultLogger?.Invoke(timeLog);
                            SaveResultLogToFile(timeLog);
                            _battleStartTime = DateTime.MinValue;
                            _isSurrendered = false; // Reset flag surrender
                            await Task.Delay(3000, cancellationToken);
                            break;

                        case "ValorChest":
                            await _battleSvc.ClickClamContinue(deviceId, log);
                            await Task.Delay(3000, cancellationToken);
                            break;
                        case "CombatBoosts":
                            log("Phát hiện màn hình CombatBoosts - đang xử lý...");
                            await _battleSvc.ClickCombatBoosts(deviceId, log);
                            await Task.Delay(3000, cancellationToken);

                            // Kiểm tra xem đã thoát khỏi CombatBoosts chưa
                            if (!_screenSvc.IsCombatBoostsScreen(deviceId, log)) {

                                log("✅ Đã thoát khỏi CombatBoosts thành công");
                                break;
                            }
                            else {
                                log("Error Restart");
                                await RestartAppSafe(deviceId, log, cancellationToken);
                                break;
                            }

                        case "WaitPvp":
                        case "PVP":
                            _lastDefaultScreenTime = DateTime.MinValue;
                            log("Waiting in PVP screen.");
                            
                            // Kiểm tra và bật chế độ thua trong PVP
                            if (_loseMode && _targetLoseCount > 0 && !_shouldSurrenderForTotalLose)
                            {
                                log($"Bật chế độ thua trong PVP: {_targetLoseCount} lượt còn lại");
                                _shouldSurrenderForTotalLose = true;
                            }
                            
                            await Task.Delay(3000, cancellationToken);
                            break;

                        case "unknown":
                            log("Unknown screen detected. Capturing screenshot for analysis...");
                            await SaveUnknownScreenScreenshot(deviceId, log);
                            log("Restarting app.");
                            await RestartAppSafe(deviceId, log, cancellationToken);
                            _lastDefaultScreenTime = DateTime.MinValue;
                            break;

                        default:
                            if (_lastDefaultScreenTime == DateTime.MinValue)
                            {
                                _lastDefaultScreenTime = DateTime.UtcNow;
                                log($"Unhandled screen: '{screen}'. Starting 90s timeout.");
                            }
                            else if ((DateTime.UtcNow - _lastDefaultScreenTime).TotalSeconds > 90)
                            {
                                //log($"Stuck on an unhandled screen for >90s. Capturing screenshot and restarting app. Last screen: '{screen}'");
                                await SaveUnknownScreenScreenshot(deviceId, log);
                                await RestartAppSafe(deviceId, log, cancellationToken);
                                _lastDefaultScreenTime = DateTime.MinValue;
                            }
                            else
                            {
                                 //log($"Waiting on unhandled screen '{screen}'. Time elapsed: {(DateTime.UtcNow - _lastDefaultScreenTime).TotalSeconds:F0}s"); 
                                 await Task.Delay(3000, cancellationToken);
                            }
                            break;
                    }
                } catch (OperationCanceledException) {
                    log("Auto service was stopped by the user.");
                } 
                catch (Exception ex)
                {
                    log($"❌ Lỗi trong quá trình chạy: {ex.Message}");
                    log("Đang khởi động lại LDPlayer...");
                    
                    // Khởi động lại LDPlayer
                    await RestartLDPlayer(log);
                    
                    // Kiểm tra kết nối ADB và chờ giả lập khởi động
                    await WaitForLDPlayerReady(deviceId, log, cancellationToken);
                    
                    // Tiếp tục vòng lặp
                    continue;
                }
            }
        }

        private bool EnsureAppRunning(string deviceId, Action<string> log)
        {
            bool running = _adb.IsAppRunning(deviceId, _packageName);
            log(running ? "App is running." : "App is not running.");
            return running;
        }

        private async Task WaitForLoading(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log, CancellationToken cancellationToken)
        {
            int waited = 0;
            while (waited < 90000)
            {
                if (cancellationToken.IsCancellationRequested) return;

                if (!_screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log))
                {
                    log("Loading finished.");
                    return;
                }
                log("Still loading...");
                await Task.Delay(3000, cancellationToken);
                waited += 3000;
            }
            log("Timeout while loading - restarting.");
            await RestartAppSafe(deviceId, log, cancellationToken);
        }
        
        private async Task RestartAppSafe(string deviceId, Action<string> log, CancellationToken cancellationToken)
        {
            if (_restartCount >= MaxRestartPerSession)
            {
                log($"Đã restart app {_restartCount} lần liên tiếp, tạm dừng để tránh lặp vô hạn.");
                await Task.Delay(20000, cancellationToken); // Chờ lâu hơn nếu lặp
                _restartCount = 0;
            }
            _restartCount++;
            _appCtrl.Close(deviceId, _packageName);
            await Task.Delay(2000, cancellationToken);
            _appCtrl.Open(deviceId, _packageName);
            log("Đang chờ app khởi động lại...");
            await Task.Delay(15000, cancellationToken); // Chờ app load xong
            _lastDefaultScreenTime = DateTime.MinValue;
        }

        private async Task RestartLDPlayer(Action<string> log)
        {
            try
            {
                log("Đang tắt LDPlayer...");
                
                // Tắt tất cả process của LDPlayer
                var ldPlayerProcesses = System.Diagnostics.Process.GetProcessesByName("dnplayer");
                foreach (var process in ldPlayerProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch { }
                }
                
                await Task.Delay(3000); // Chờ tắt hoàn toàn
                
                log("Đang khởi động lại LDPlayer...");
                
                // Đường dẫn mặc định của LDPlayer
                string ldPlayerPath = @"C:\LDPlayer\LDPlayer9\dnplayer.exe";
                if (!File.Exists(ldPlayerPath))
                {
                    ldPlayerPath = @"C:\Program Files (x86)\LDPlayer\LDPlayer9\dnplayer.exe";
                }
                
                if (File.Exists(ldPlayerPath))
                {
                    // Khởi động LDPlayer
                    var process = System.Diagnostics.Process.Start(ldPlayerPath);
                    
                    // Chờ một chút để LDPlayer khởi động
                    await Task.Delay(2000);
                    
                    // Tìm và minimize cửa sổ LDPlayer
                    try
                    {
                        var ldPlayerProcesses2 = System.Diagnostics.Process.GetProcessesByName("dnplayer");
                        foreach (var proc in ldPlayerProcesses2)
                        {
                            if (proc.MainWindowHandle != IntPtr.Zero)
                            {
                                // Minimize cửa sổ
                                LUDUS.Utils.Win32.ShowWindow(proc.MainWindowHandle, LUDUS.Utils.Win32.SW_MINIMIZE);
                                log("Đã minimize cửa sổ LDPlayer.");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log($"Không thể minimize cửa sổ LDPlayer: {ex.Message}");
                    }
                    
                    log("Đã khởi động LDPlayer ở chế độ minimized");
                }
                else
                {
                    log("Không tìm thấy LDPlayer. Vui lòng khởi động thủ công.");
                }
            }
            catch (Exception ex)
            {
                log($"Lỗi khi khởi động lại LDPlayer: {ex.Message}");
            }
        }

        private async Task WaitForLDPlayerReady(string deviceId, Action<string> log, CancellationToken cancellationToken)
        {
            log("Đang chờ LDPlayer khởi động và kết nối ADB...");
            
            int maxWaitTime = 120; // Tối đa 2 phút
            int waited = 0;
            
            while (waited < maxWaitTime * 1000)
            {
                if (cancellationToken.IsCancellationRequested) return;
                
                try
                {
                    // Kiểm tra kết nối ADB bằng DeviceManager
                    _deviceManager.Refresh();
                    if (_deviceManager.Devices.Any(d => d.Contains(deviceId)))
                    {
                        log("✅ LDPlayer đã khởi động và kết nối ADB thành công");
                        
                        // Chờ thêm một chút để đảm bảo ổn định
                        await Task.Delay(5000, cancellationToken);
                        
                        // Mở app
                        log("Đang mở app...");
                        _appCtrl.Open(deviceId, _packageName);
                        log("App đã được mở. Chờ 30s để load...");
                        await Task.Delay(30000, cancellationToken);
                        
                        return;
                    }
                }
                catch { }
                
                log($"Chờ LDPlayer khởi động... ({waited / 1000}s/{maxWaitTime}s)");
                await Task.Delay(5000, cancellationToken);
                waited += 5000;
            }
            
            log("❌ Timeout chờ LDPlayer khởi động. Vui lòng kiểm tra thủ công.");
        }

        public int WinCount => _winCount;
        public int LoseCount => _loseCount;
        public int TargetLoseCount => _targetLoseCount;
        public int RemainingLoseCount => _targetLoseCount > 0 ? _targetLoseCount : 0;
        public bool IsLoseMode => _loseMode;

        /// <summary>
        /// Lấy thông tin round và log
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>Thông tin về round</returns>
        private async Task<RoundInfo> GetRoundInfoAndLog(string deviceId, Action<string> log)
        {
            try
            {
                // Tạo RoundDetectionService với các tham số cần thiết
                // Sử dụng cùng đường dẫn như ScreenDetectionService
                string regionsXmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "regions.xml");
                string templateBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                var roundDetectionSvc = new RoundDetectionService(_captureSvc, regionsXmlPath, templateBasePath);
                var roundInfo = await roundDetectionSvc.GetRoundInfo(deviceId, log);
                return roundInfo;
            }
            catch (Exception ex)
            {
                log($"❌ Lỗi khi lấy thông tin round: {ex.Message}");
                return new RoundInfo { IsRound1 = false, Life1EmptyCount = 0, Life2EmptyCount = 0, CalculatedRound = 1 };
            }
        }

        private void SaveResultLogToFile(string logLine)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, $"result_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {logLine}{Environment.NewLine}");
            }
            catch { }
        }

        private async Task SaveUnknownScreenScreenshot(string deviceId, Action<string> log)
        {
            try
            {
                using (var screenshot = _captureSvc.Capture(deviceId) as Bitmap)
                {
                    if (screenshot == null)
                    {
                        log("❌ Không thể chụp màn hình");
                        return;
                    }

                    // Tạo thư mục UnknownScreens nếu chưa có
                    string unknownDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnknownScreens");
                    if (!Directory.Exists(unknownDir)) Directory.CreateDirectory(unknownDir);
                    
                    string fileName = $"UnknownScreen_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string filePath = Path.Combine(unknownDir, fileName);
                    
                    screenshot.Save(filePath);
                    log($"📸 Đã lưu ảnh màn hình không xác định: {filePath}");
                }
            }
            catch (Exception ex)
            {
                log($"❌ Lỗi khi lưu ảnh màn hình không xác định: {ex.Message}");
            }
        }
    }
}