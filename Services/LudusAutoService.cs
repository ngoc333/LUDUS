using System;
using System.Drawing;
using System.Threading.Tasks;
using LUDUS.Services;
using System.Threading;
using System.IO;
using System.Linq;

namespace LUDUS.Logic {
    public class LudusAutoService {
        private readonly AdbService _adb;
        private readonly DeviceManager _deviceManager;
        private readonly AppController _appCtrl;
        private readonly ScreenDetectionService _screenSvc;
        private readonly PvpNavigationService _pvpNav;
        private readonly BattleService _battleSvc;
        private readonly ScreenCaptureService _captureSvc;
        private readonly string _packageName;
        private DateTime _lastDefaultScreenTime = DateTime.MinValue;
        private int _winCount = 0;
        private int _loseCount = 0;
        private DateTime _battleStartTime;
        private DateTime _battleEndTime;
        private DateTime _surrenderTime = DateTime.MinValue; // Thời điểm bỏ cuộc
        private Action<string> _resultLogger;
        private bool _shouldSurrenderNext = false;
        private bool _isSurrendered = false;
        private bool _loseMode = false;
        private int _targetLoseCount = 0;
        private bool _shouldSurrenderForTotalLose = false;
        private bool _isPVP = false;
        private int _restartCount = 0;
        private const int MaxRestartPerSession = 2;
        private int _surrenderAfterWinCount = 0; // Đếm số trận cần bỏ cuộc sau khi thắng

        // Cài đặt win/lose để điều khiển hành vi bỏ cuộc
        private int _winLoseWinCount = 0; // Số trận thắng trước khi bỏ cuộc
        private int _winLoseLoseCount = 0; // Số trận bỏ cuộc sau khi thắng
        private int _currentWinStreak = 0; // Đếm chuỗi thắng hiện tại

        // Theo dõi màn hình hiện tại để reset nếu hiển thị quá lâu
        private string _currentScreenName = null;
        private DateTime _currentScreenStartTime = DateTime.MinValue;

        // --- Chế độ chính khi ở màn hình Main ---
        private bool _preferPvp = false; // false = Astra, true = PVP

        /// <summary>
        /// Bật/tắt chế độ ưu tiên PVP (false = Astra)
        /// </summary>
        public void SetPreferPvp(bool enablePvp) {
            _preferPvp = enablePvp;
        }

        // Định nghĩa các loại màn hình để tránh lặp string literal và lỗi chính tả
        private enum ScreenType {
            Main,
            Loading,
            ToBattle,
            Battle,
            EndBattle,
            EndBattle2,
            Chest,
            CombatBoosts,
            WaitPvp,
            PVP,
            Unknown
        }

        /// <summary>
        /// Chuyển đổi chuỗi tên màn hình thành enum ScreenType (dùng switch-case C# 7.3 để tương thích .NET 4.8)
        /// </summary>
        private ScreenType ParseScreen(string screenName) {
            switch (screenName) {
                case "Main":
                    return ScreenType.Main;
                case "Loading":
                    return ScreenType.Loading;
                case "ToBattle":
                    return ScreenType.ToBattle;
                case "Battle":
                    return ScreenType.Battle;
                case "EndBattle":
                    return ScreenType.EndBattle;
                case "EndBattle2":
                    return ScreenType.EndBattle2;
                case "Chest":
                    return ScreenType.Chest;
                case "CombatBoosts":
                    return ScreenType.CombatBoosts;
                case "WaitPvp":
                    return ScreenType.WaitPvp;
                case "PVP":
                    return ScreenType.PVP;
                default:
                    return ScreenType.Unknown;
            }
        }

        public LudusAutoService(
            AdbService adb,
            DeviceManager deviceManager,
            AppController appController,
            ScreenDetectionService screenDetectionService,
            PvpNavigationService pvpNav,
            BattleService battleAnalyzerService,
            ScreenCaptureService captureService,
            string packageName) {
            _adb = adb;
            _deviceManager = deviceManager;
            _appCtrl = appController;
            _screenSvc = screenDetectionService;
            _pvpNav = pvpNav;
            _battleSvc = battleAnalyzerService;
            _captureSvc = captureService;
            _packageName = packageName;
        }

        public void SetResultLogger(Action<string> resultLogger) {
            _resultLogger = resultLogger;
        }

        public void SetLoseMode(bool enabled, int targetLoseCount) {
            _loseMode = enabled;
            _targetLoseCount = targetLoseCount;

            // Nếu bật chế độ thua, ngay lập tức bỏ cuộc trận đầu tiên
            if (enabled && targetLoseCount > 0) {
                _shouldSurrenderForTotalLose = true;
            }
            else {
                _shouldSurrenderForTotalLose = false;
            }
        }

        public void CheckAndUpdateLoseMode(int currentTargetLoseCount) {
            // Kiểm tra xem user có thay đổi số lượng trận thua không
            if (currentTargetLoseCount != _targetLoseCount) {
                SetLoseMode(currentTargetLoseCount > 0, currentTargetLoseCount);
            }
        }

        public void UpdateLoseModeFromUI(int targetLoseCount) {
            // Cập nhật chế độ thua từ UI
            SetLoseMode(targetLoseCount > 0, targetLoseCount);
        }

        public void UpdateWinLoseSettings(int winCount, int loseCount) {
            _winLoseWinCount = winCount;
            _winLoseLoseCount = loseCount;
            _resultLogger?.Invoke($"Cập nhật cài đặt Win/Lose: {winCount} thắng -> {loseCount} bỏ cuộc");
        }

        public async Task RunAsync(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log, CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                if (cancellationToken.IsCancellationRequested) break;

                string screenName = await _screenSvc.DetectScreenAsync(deviceId, log);

                // Kiểm tra xem màn hình có hiển thị quá 120s hay không
                await CheckScreenTimeout(deviceId, screenName, log, cancellationToken);

                ScreenType screen = ParseScreen(screenName);

                // Tách xử lý thành các hàm riêng để code gọn hơn
                switch (screen) {
                    case ScreenType.Main:
                        await HandleMainScreen(deviceId, log, cancellationToken);
                        break;
                    case ScreenType.Loading:
                        await HandleLoadingScreen(log, cancellationToken);
                        break;
                    case ScreenType.ToBattle:
                    case ScreenType.Battle:
                        await HandleBattleScreen(deviceId, log, cancellationToken);
                        break;
                    case ScreenType.EndBattle2:
                    case ScreenType.EndBattle:
                        await HandleEndBattleScreen(deviceId, log, cancellationToken);
                        break;
                    case ScreenType.Chest:
                        await HandleChestScreen(deviceId, log, cancellationToken);
                        break;
                    case ScreenType.CombatBoosts:
                        await HandleCombatBoostsScreen(deviceId, log, cancellationToken);
                        break;
                    case ScreenType.WaitPvp:
                    case ScreenType.PVP:
                        await HandleWaitPvpScreen(deviceId, log, cancellationToken);
                        break;

                    case ScreenType.Unknown:
                        if (!EnsureAppRunning(deviceId, log)) {
                            log("App is not running. Opening...");
                            _appCtrl.Open(deviceId, _packageName);
                            log("App opened. Waiting 45s for loading...");
                            await Task.Delay(45000, cancellationToken);
                            return;
                        }
                        await HandleUnknownScreen(deviceId, screenName, log, cancellationToken);
                        break;

                    default:
                        await HandleUnknownScreen(deviceId, screenName, log, cancellationToken);
                        break;
                }

            }
        }

        /// <summary>
        /// Kiểm tra thời gian hiển thị của màn hình hiện tại; nếu >120s thì reset app.
        /// </summary>
        private async Task CheckScreenTimeout(string deviceId, string screenName, Action<string> log, CancellationToken ct) {
            if (string.IsNullOrEmpty(screenName)) return;

            if (_currentScreenName == null || _currentScreenName != screenName) {
                _currentScreenName = screenName;
                _currentScreenStartTime = DateTime.UtcNow;
                return;
            }

            double elapsed = (DateTime.UtcNow - _currentScreenStartTime).TotalSeconds;
            if (elapsed > 120) {
                log($"⏰ Màn hình '{screenName}' hiển thị liên tục {elapsed:F0}s (>120s). Đang reset app...");
                await RestartAppSafe(deviceId, log, ct);
                _currentScreenName = null;
                _currentScreenStartTime = DateTime.MinValue;
            }
        }

        private async Task HandleMainScreen(string deviceId, Action<string> log, CancellationToken cancellationToken) {
            _lastDefaultScreenTime = DateTime.MinValue;
            _isSurrendered = false; // Reset flag khi về màn hình chính
            _surrenderTime = DateTime.MinValue; // Reset thời gian bỏ cuộc
            log("Main screen detected.");

            // Kiểm tra và bật chế độ thua trước khi vào PVP
            if (_loseMode && _targetLoseCount > 0) {

                log($"Chế độ thua đang bật: {_targetLoseCount} lượt còn lại");
                _shouldSurrenderForTotalLose = true;
                _isPVP = true;
                _pvpNav.GoToPvp(deviceId, log);
                await Task.Delay(1000, cancellationToken);
                return;

            }
            if (_surrenderAfterWinCount > 0 || _shouldSurrenderNext || _shouldSurrenderForTotalLose) {

                log($"Chế độ thua đang bật: {_targetLoseCount} lượt còn lại");
                //_shouldSurrenderForTotalLose = true;
                log("Navigating to PVP.");
                _isPVP = true;
                _pvpNav.GoToPvp(deviceId, log);
                await Task.Delay(1000, cancellationToken);
                return;

            }
            // Điều hướng theo cấu hình UI (PVP hoặc Astra)
            if (_preferPvp) {
                _isPVP = true;
                _pvpNav.GoToPvp(deviceId, log);
            }
            else {
                _isPVP = false;
                _pvpNav.GoToAstra(deviceId, log);
            }
            await Task.Delay(1000, cancellationToken);
        }

        private bool EnsureAppRunning(string deviceId, Action<string> log) {
            bool running = _adb.IsAppRunning(deviceId, _packageName);
            log(running ? "App is running." : "App is not running.");
            return running;
        }

        private async Task RestartAppSafe(string deviceId, Action<string> log, CancellationToken cancellationToken) {
            if (_restartCount >= MaxRestartPerSession) {
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
        public int TargetLoseCount => _targetLoseCount;
        public int RemainingLoseCount => _targetLoseCount > 0 ? _targetLoseCount : 0;
        public bool IsLoseMode => _loseMode;

        /// <summary>
        /// Lấy thông tin round và log
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>Thông tin về round</returns>
        private async Task<RoundInfo> GetRoundInfoAndLog(string deviceId, Action<string> log) {
            try {
                // Tạo RoundDetectionService với các tham số cần thiết
                // Sử dụng cùng đường dẫn như ScreenDetectionService
                string regionsXmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "regions.xml");
                string templateBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                var roundDetectionSvc = new RoundDetectionService(_captureSvc, regionsXmlPath, templateBasePath);
                var roundInfo = await roundDetectionSvc.GetRoundInfo(deviceId, log);
                return roundInfo;
            } catch (Exception ex) {
                log($"❌ Lỗi khi lấy thông tin round: {ex.Message}");
                return new RoundInfo { IsRound1 = false, Life1EmptyCount = 0, Life2EmptyCount = 0, CalculatedRound = 1 };
            }
        }

        private void SaveResultLogToFile(string logLine) {
            try {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, $"result_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {logLine}{Environment.NewLine}");
            } catch { }
        }

        private async Task SaveUnknownScreenScreenshot(string deviceId, Action<string> log) {
            try {
                using (var screenshot = _captureSvc.Capture(deviceId) as Bitmap) {
                    if (screenshot == null) {
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
            } catch (Exception ex) {
                log($"❌ Lỗi khi lưu ảnh màn hình không xác định: {ex.Message}");
            }
        }

        // ===== Các hàm handler tách riêng =====

        private async Task HandleLoadingScreen(Action<string> log, CancellationToken ct) {
            _isSurrendered = false; // Reset flag khi loading
            _surrenderTime = DateTime.MinValue; // Reset thời gian bỏ cuộc
            log("Loading screen detected");
            await Task.Delay(5000, ct);
        }

        private async Task HandleBattleScreen(string deviceId, Action<string> log, CancellationToken ct) {
            // Nếu đã bỏ cuộc trong lần gọi trước, chỉ chờ màn hình kết thúc
            if (_isSurrendered) {
                log("Đã bỏ cuộc, chờ màn hình kết thúc trận...");
                await Task.Delay(2000, ct);
                
                // Nếu vẫn ở màn hình ToBattle sau 10 giây, có thể có vấn đề
                if (_surrenderTime != DateTime.MinValue && (DateTime.Now - _surrenderTime).TotalSeconds > 10) {
                    log("Đã bỏ cuộc quá lâu, reset flag để tránh stuck...");
                    _isSurrendered = false;
                    _surrenderTime = DateTime.MinValue;
                    _battleStartTime = DateTime.MinValue;
                }
                return;
            }

            if (_isPVP) {
                if (_surrenderAfterWinCount > 0 || _shouldSurrenderNext || _shouldSurrenderForTotalLose) {
                    log("Sẽ tự động bỏ cuộc trận này!");
                    _isSurrendered = true;
                    _surrenderTime = DateTime.Now;
                    await _battleSvc.ClickLoseAndYes(deviceId, log);
                    _battleStartTime = DateTime.Now;
                    await Task.Delay(1000, ct);
                    if (_surrenderAfterWinCount > 0) _surrenderAfterWinCount--;
                    return;
                }
            }

            _isSurrendered = false;

            RoundInfo roundInfo = await GetRoundInfoAndLog(deviceId, log);
            int calculatedRound = roundInfo.CalculatedRound;

            if (_battleStartTime == DateTime.MinValue && calculatedRound != 1) {
                _battleStartTime = DateTime.Now;
                log($"Đồng bộ _battleStartTime khi vào giữa trận: {calculatedRound}");
                _battleSvc.ResetBoardState(); // Reset khi vào giữa trận
            }

            if (calculatedRound == 1) {
                _battleStartTime = DateTime.Now;
                log($"Bắt đầu trận mới lúc: {_battleStartTime:HH:mm:ss}");
                _battleSvc.ResetBoardState(); // Reset khi bắt đầu trận mới
            }
            


            _lastDefaultScreenTime = DateTime.MinValue;

            log($">>> ROUND {calculatedRound} <<<");

            await _battleSvc.ClickSpell(deviceId, calculatedRound, log);

            // Logic đặc biệt cho round 5: nếu life1 = 0 và life2 = 4 thì thực hiện như round 1
           // bool isSpecialRound5 = (calculatedRound == 5 && roundInfo.Life1EmptyCount == 0 && roundInfo.Life2EmptyCount == 4);
            
            if (calculatedRound == 1 ) {
                await _battleSvc.ClickCoin(deviceId, 15, log);
                await Task.Delay(3000, ct);
               // _battleSvc.CaptureAfterCoinClick(deviceId, calculatedRound, log);
            }
            else {
                if (await _battleSvc.IsInBattleScreen(deviceId, log, ct)) {
                    await _battleSvc.ClickCoin(deviceId, 10, log);
                    await Task.Delay(3000, ct); 
                  //  _battleSvc.CaptureAfterCoinClick(deviceId, calculatedRound, log);
                    
                    // Logic rescan cho round 2 và 3: scan 2 lần
                    if (calculatedRound == 3) {
                        log($"Round {calculatedRound}: Thực hiện scan và merge lần 1");
                        bool merged1 = await _battleSvc.AnalyzeAndMerge(deviceId, log, calculatedRound);
                        if (!merged1) log("No merge lần 1");
                        
                        // Scan lần 2 chỉ những ô trống, stone, hoặc merge fail
                        log($"Round {calculatedRound}: Thực hiện scan và merge lần 2");
                        bool merged2 = await _battleSvc.AnalyzeAndMerge(deviceId, log, calculatedRound);
                        if (!merged2) log("No merge lần 2");
                    } else {
                        // Các round khác: scan 1 lần
                        bool merged = await _battleSvc.AnalyzeAndMerge(deviceId, log, calculatedRound);
                        if (!merged) log("No merge");
                    }
                }
            }

            await Task.Delay(500, ct);
            await _battleSvc.ClickCoin(deviceId, 5, log);
            await _battleSvc.ClickEndRound(deviceId, log);
            await Task.Delay(500, ct);
        }

        private async Task HandleEndBattleScreen(string deviceId, Action<string> log, CancellationToken ct) {
            _lastDefaultScreenTime = DateTime.MinValue;
            _battleEndTime = DateTime.Now;
            TimeSpan duration = (_battleStartTime != DateTime.MinValue) ? (_battleEndTime - _battleStartTime) : TimeSpan.Zero;
            bool isWin = _screenSvc.DetectVictoryResult(deviceId, log);
            if (duration != TimeSpan.Zero) {
                if (isWin) {
                    _winCount++;
                    _currentWinStreak++;
                    
                    // Kiểm tra xem có cần bỏ cuộc theo cài đặt win/lose không
                    if (_winLoseWinCount > 0 && _winLoseLoseCount > 0 && _currentWinStreak >= _winLoseWinCount) {
                        _surrenderAfterWinCount = _winLoseLoseCount;
                        log($"Đã thắng {_currentWinStreak} trận liên tiếp, sẽ bỏ cuộc {_winLoseLoseCount} trận tiếp theo");
                        _currentWinStreak = 0; // Reset chuỗi thắng
                    }
                }
                else {
                    if (!_isSurrendered) {
                        _loseCount++;
                        _currentWinStreak = 0; // Reset chuỗi thắng khi thua thật
                        if (_loseMode) {
                            _targetLoseCount--;
                            if (_targetLoseCount <= 0) {
                                log("Đã đạt đủ trận thua thật sự. Tắt chế độ thua liên tục.");
                                _loseMode = false;
                                _shouldSurrenderForTotalLose = false;
                            }
                            else {
                                log($"Thua thật sự, {_targetLoseCount} lượt còn lại.");
                                _shouldSurrenderForTotalLose = true;
                            }
                        }
                    }
                    else {
                        if (_loseMode) {
                            _targetLoseCount--;
                            if (_targetLoseCount <= 0) {
                                log("Đã đạt đủ tổng số lượt. Tắt chế độ thua liên tục.");
                                _loseMode = false;
                                _shouldSurrenderForTotalLose = false;
                            }
                            else {
                                log($"Đã bỏ cuộc, {_targetLoseCount} lượt còn lại.");
                                _shouldSurrenderForTotalLose = true;
                            }
                        }
                    }

                    // Reset chuỗi thắng khi bỏ cuộc
                    if (_isSurrendered) {
                        _currentWinStreak = 0;
                    }

                    _shouldSurrenderNext = false;
                }

                string result = _isSurrendered ? "Bỏ cuộc" : (isWin ? "Thắng" : "Thua");


                string durationStr = $"{(int)duration.TotalMinutes} phút {duration.Seconds} giây";
                string timeLog = $"Kết quả: {result} | Bắt đầu: {_battleStartTime:HH:mm:ss} | Kết thúc: {_battleEndTime:HH:mm:ss} | Thời gian: {durationStr}";
                _resultLogger?.Invoke(timeLog);
                SaveResultLogToFile(timeLog);
            }

            _battleStartTime = DateTime.MinValue;
            _isSurrendered = false;
            _surrenderTime = DateTime.MinValue;
            _battleSvc.ResetBoardState(); // Reset khi kết thúc trận
            await Task.Delay(1000, ct);
        }

        private async Task HandleChestScreen(string deviceId, Action<string> log, CancellationToken ct) {
            await _battleSvc.ClickClamContinue(deviceId, log);
            await Task.Delay(1000, ct);
        }

        private async Task HandleCombatBoostsScreen(string deviceId, Action<string> log, CancellationToken ct) {
            await _battleSvc.ClickCombatBoosts(deviceId, log);
            await Task.Delay(1000, ct);

            if (_screenSvc.IsCombatBoostsScreen(deviceId, log)) {
                log("CombatBoosts Error --> Restart");
                await RestartAppSafe(deviceId, log, ct);
            }
        }

        private async Task HandleWaitPvpScreen(string deviceId, Action<string> log, CancellationToken ct) {
            _lastDefaultScreenTime = DateTime.MinValue;

            if (_loseMode && _targetLoseCount > 0 && !_shouldSurrenderForTotalLose) {
                log($"Bật chế độ thua trong PVP: {_targetLoseCount} lượt còn lại");
                _shouldSurrenderForTotalLose = true;
            }

            await Task.Delay(3000, ct);
        }

        private async Task HandleUnknownScreen(string deviceId, string screenName, Action<string> log, CancellationToken ct) {
            if (_lastDefaultScreenTime == DateTime.MinValue) {
                _lastDefaultScreenTime = DateTime.UtcNow;
                log($"Unhandled screen: '{screenName}'. Starting 90s timeout.");
            }
            else if ((DateTime.UtcNow - _lastDefaultScreenTime).TotalSeconds > 90) {
                await SaveUnknownScreenScreenshot(deviceId, log);
                await RestartAppSafe(deviceId, log, ct);
                _lastDefaultScreenTime = DateTime.MinValue;
            }
            else {
                await Task.Delay(3000, ct);
            }
        }

    }
}