using System;
using System.Drawing;
using System.Threading.Tasks;
using LUDUS.Services;
using System.Threading;
using System.IO;

namespace LUDUS.Logic
{
    public class LudusAutoService
    {
        private readonly AdbService _adb;
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
        private int _restartCount = 0;
        private const int MaxRestartPerSession = 2;

        public LudusAutoService(
            AdbService adb,
            AppController appController,
            ScreenDetectionService screenDetectionService,
            PvpNavigationService pvpNav,
            BattleAnalyzerService battleAnalyzerService,
            ScreenCaptureService captureService,
            string packageName)
        {
            _adb = adb;
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

        public async Task RunAsync(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
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
                        _pvpNav.GoToPvp(deviceId, log);
                        await Task.Delay(3000, cancellationToken);
                        break;
                    case "Loading":
                        log("Loading screen detected");
                        await Task.Delay(10000, cancellationToken);
                        break;
                    case "ToBattle":
                    case "Battle":
                        if (_shouldSurrenderNext)
                        {
                            log("Sẽ tự động bỏ cuộc trận này!");
                            await _battleSvc.ClickLoseAndYes(deviceId, log);
                            _battleStartTime = DateTime.Now; // Reset thời gian bắt đầu khi vào round 1
                            await Task.Delay(2000, cancellationToken);
                            break;
                        }
                        
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
                        await _battleSvc.ClickEndRound(deviceId, log);
                        await Task.Delay(3000, cancellationToken);
                        
                        // Kiểm tra thêm 1 lần nữa sau khi click EndRound
                        if (await _battleSvc.IsInBattleScreen(deviceId, log))
                        {
                            log("Vẫn còn ở màn hình Battle, kiểm tra merge thêm 1 lần nữa...");
                            bool merged = await _battleSvc.AnalyzeAndMerge(deviceId, log);
                            if (merged)
                            {
                                log("Merge thành công, click EndRound lần cuối...");
                                await _battleSvc.ClickEndRound(deviceId, log);
                                await Task.Delay(3000, cancellationToken);
                            }
                            else
                            {
                                log("No merge, chuyển sang round tiếp theo");
                            }
                        }
                        else
                        {
                            log("Đã chuyển khỏi màn hình Battle");
                        }
                        break;

                    case "EndBattle":
                        _lastDefaultScreenTime = DateTime.MinValue; 
                        _battleEndTime = DateTime.Now;
                        bool isWin = _screenSvc.DetectVictoryResult(deviceId, log);
                        if (isWin)
                        {
                            _winCount++;
                            _shouldSurrenderNext = true;
                        }
                        else
                        {
                            _loseCount++;
                            _shouldSurrenderNext = false;
                        }
                        string result = isWin ? "Thắng" : "Thua";
                        TimeSpan duration = (_battleStartTime != DateTime.MinValue) ? (_battleEndTime - _battleStartTime) : TimeSpan.Zero;
                        string durationStr = duration != TimeSpan.Zero ? $"{(int)duration.TotalMinutes} phút {duration.Seconds} giây" : "Không xác định";
                        string timeLog = $"Kết quả: {result} | Bắt đầu: {_battleStartTime:HH:mm:ss} | Kết thúc: {_battleEndTime:HH:mm:ss} | Thời gian: {durationStr}";
                        _resultLogger?.Invoke(timeLog);
                        SaveResultLogToFile(timeLog);
                        _battleStartTime = DateTime.MinValue;
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
                            log($"Stuck on an unhandled screen for >90s. Capturing screenshot and restarting app. Last screen: '{screen}'");
                            await SaveUnknownScreenScreenshot(deviceId, log);
                            await RestartAppSafe(deviceId, log, cancellationToken);
                            _lastDefaultScreenTime = DateTime.MinValue;
                        }
                        else
                        {
                             log($"Waiting on unhandled screen '{screen}'. Time elapsed: {(DateTime.UtcNow - _lastDefaultScreenTime).TotalSeconds:F0}s"); 
                             await Task.Delay(3000, cancellationToken);
                        }
                        break;
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

        public int WinCount => _winCount;
        public int LoseCount => _loseCount;

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