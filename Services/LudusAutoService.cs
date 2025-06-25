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
        private readonly string _packageName;
        private int _round = 1;
        private DateTime _lastDefaultScreenTime = DateTime.MinValue;
        private int _winCount = 0;
        private int _loseCount = 0;
        private DateTime _battleStartTime;
        private DateTime _battleEndTime;
        private Action<string> _resultLogger;
        private bool _shouldSurrenderNext = false;
        private bool _enableRoundDetection = true; // Bật/tắt chức năng tính round từ lifeEmpty

        public LudusAutoService(
            AdbService adb,
            AppController appController,
            ScreenDetectionService screenDetectionService,
            PvpNavigationService pvpNav,
            BattleAnalyzerService battleAnalyzerService,
            string packageName)
        {
            _adb = adb;
            _appCtrl = appController;
            _screenSvc = screenDetectionService;
            _pvpNav = pvpNav;
            _battleSvc = battleAnalyzerService;
            _packageName = packageName;
        }

        public void SetResultLogger(Action<string> resultLogger)
        {
            _resultLogger = resultLogger;
        }

        /// <summary>
        /// Bật/tắt chức năng tính round từ lifeEmpty
        /// </summary>
        /// <param name="enable">True để bật, False để tắt</param>
        public void EnableRoundDetection(bool enable)
        {
            _enableRoundDetection = enable;
        }

        /// <summary>
        /// Lấy trạng thái chức năng tính round
        /// </summary>
        /// <returns>True nếu đang bật</returns>
        public bool IsRoundDetectionEnabled()
        {
            return _enableRoundDetection;
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

                if (_screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log))
                {
                    log("Loading screen detected. Waiting for it to finish...");
                    await WaitForLoading(deviceId, ocrFunc, log, cancellationToken);
                    // After loading, restart loop to detect the new screen.
                    continue;
                }

                if (cancellationToken.IsCancellationRequested) break;

                string screen = _screenSvc.DetectScreen(deviceId, log);

                switch (screen)
                {
                    case "Main":
                        _lastDefaultScreenTime = DateTime.MinValue;
                        log("Main screen detected. Navigating to PVP.");
                        _pvpNav.GoToPvp(deviceId, log);
                        _round = 1; // Reset round when we start a new pvp flow
                        await Task.Delay(3000, cancellationToken);
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
                        int calculatedRound = _round; // Mặc định sử dụng _round nếu không bật detection
                        RoundInfo roundInfo = null;
                        
                        if (_enableRoundDetection)
                        {
                            roundInfo = await GetRoundInfoAndLog(deviceId, log);
                            calculatedRound = roundInfo.CalculatedRound;
                            
                            // Cập nhật _round nếu khác
                            if (calculatedRound != _round)
                            {
                                log($"🔄 Cập nhật round từ {_round} thành {calculatedRound} (Life1: {roundInfo.Life1EmptyCount}, Life2: {roundInfo.Life2EmptyCount})");
                                _round = calculatedRound;
                            }
                            else
                            {
                                log($"✅ Round {_round} khớp với thực tế (Life1: {roundInfo.Life1EmptyCount}, Life2: {roundInfo.Life2EmptyCount})");
                            }
                        }
                        else
                        {
                            log($"🔧 Chức năng tính round từ lifeEmpty đã tắt, sử dụng _round = {_round}");
                        }
                        
                        if (calculatedRound == 1) {
                            _battleStartTime = DateTime.Now; // Reset thời gian bắt đầu khi vào round 1
                            log($"Bắt đầu trận mới lúc: {_battleStartTime:HH:mm:ss}");
                        }
                        _lastDefaultScreenTime = DateTime.MinValue;
                        
                        string roundStatus = _enableRoundDetection 
                            ? $"ROUND {calculatedRound} (Tính từ lifeEmpty: {roundInfo?.TotalEmptyCount ?? 0})"
                            : $"ROUND {_round}";
                        log($">>> {roundStatus} <<<");
                        
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
                                    log("No more merges possible.");
                                }
                            }
                        }
                        await _battleSvc.ClickEndRound(deviceId, log);
                        _round = calculatedRound + 1; // Tăng round cho lần tiếp theo
                        await Task.Delay(3000, cancellationToken);
                        break;

                    case "EndBattle":
                        _lastDefaultScreenTime = DateTime.MinValue;
                        log("Battle ended. Resetting round count.");
                        _battleEndTime = DateTime.Now;
                        if (_battleStartTime == DateTime.MinValue)
                        {
                            _round = 1;
                            await Task.Delay(3000, cancellationToken);
                            break;
                        }
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
                        _round = 1;
                        await Task.Delay(3000, cancellationToken);
                        break;

                    case "ValorChest":
                        await _battleSvc.ClickClamContinue(deviceId, log);
                        _round = 1;
                        await Task.Delay(3000, cancellationToken);
                        break;
                    case "CombatBoosts":
                        await _battleSvc.ClickCombatBoosts(deviceId, log);
                        _round = 1;
                        await Task.Delay(3000, cancellationToken);
                        break;

                    case "WaitPvp":
                    case "PVP":
                        _lastDefaultScreenTime = DateTime.MinValue;
                        log("Waiting in PVP screen.");
                        await Task.Delay(3000, cancellationToken);
                        break;

                    case "unknown":
                        log("Unknown screen detected. Restarting app.");
                        RestartApp(deviceId, log);
                        _round = 1; // Reset rounds
                        _lastDefaultScreenTime = DateTime.MinValue; // Reset timer
                        break;

                    default:
                        if (_lastDefaultScreenTime == DateTime.MinValue)
                        {
                            _lastDefaultScreenTime = DateTime.UtcNow;
                            log($"Unhandled screen: '{screen}'. Starting 90s timeout.");
                        }
                        else if ((DateTime.UtcNow - _lastDefaultScreenTime).TotalSeconds > 90)
                        {
                            log($"Stuck on an unhandled screen for >90s. Restarting app. Last screen: '{screen}'");
                            RestartApp(deviceId, log);
                            _lastDefaultScreenTime = DateTime.MinValue;
                            _round = 1;
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
            RestartApp(deviceId, log);
        }
        
        private void RestartApp(string deviceId, Action<string> log)
        {
            _appCtrl.Close(deviceId, _packageName);
            Task.Delay(2000).Wait();
            _appCtrl.Open(deviceId, _packageName);
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
                var roundInfo = await _battleSvc.GetRoundInfo(deviceId, log);
                log($"📊 Thông tin Round: {roundInfo}");
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
    }
}