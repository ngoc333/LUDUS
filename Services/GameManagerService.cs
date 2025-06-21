using System;
using System.Drawing;
using System.Threading.Tasks;
using LUDUS.Utils;

namespace LUDUS.Services {
    public class GameManagerService {
        private readonly AdbService _adb;
        private readonly AppController _appCtrl;
        private readonly ScreenDetectionService _screenSvc;
        private readonly PvpNavigationService _pvpNav;
        private readonly BattleAnalyzerService _battleSvc;
        private readonly string _packageName;

        public GameManagerService(
            AdbService adbService,
            AppController appController,
            ScreenDetectionService screenDetectionService,
            PvpNavigationService pvpNav,
            BattleAnalyzerService battleAnalyzerService,
            string packageName) {
            _adb = adbService;
            _appCtrl = appController;
            _screenSvc = screenDetectionService;
            _pvpNav = pvpNav;
            _battleSvc = battleAnalyzerService;
            _packageName = packageName;
        }

        public async Task EnsureGameAndDetectAsync(
            string deviceId,
            Func<Bitmap, string> ocrFunc,
            Action<string> log) {
            bool isRunning = _adb.IsAppRunning(deviceId, _packageName);
            log?.Invoke(isRunning ? "App is running." : "App not running.");

            if (!isRunning) {
                log?.Invoke("Launching app...");
                _appCtrl.Open(deviceId, _packageName);
                log?.Invoke("Waiting 45s for app to launch...");
                await Task.Delay(45000);
                WaitForLoading(deviceId, ocrFunc, log);
            }
            else {
                bool isLoading = _screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log);
                if (isLoading) {
                    log?.Invoke("App is loading, waiting...");
                    WaitForLoading(deviceId, ocrFunc, log);
                }
                else {
                    string screen = _screenSvc.DetectScreen(deviceId, log);
                    log?.Invoke($"Detected screen: {screen}");
                    if (screen == "Main") {
                        log?.Invoke("At Main screen, auto into PVP...");
                        bool ok = _pvpNav.GoToPvp(deviceId, log);
                        if (ok) {
                            log?.Invoke("Auto vào PVP thành công.");
                            bool toBattle = await WaitForScreenAsync(deviceId, "ToBattle", log);
                            if (toBattle)
                            {
                                log?.Invoke("Đã vào màn hình ToBattle, bắt đầu tự động PVP...");
                                await _battleSvc.AnalyzeBattle(deviceId, log);
                            }
                            else
                            {
                                log?.Invoke("Không vào được ToBattle sau khi click PVP, sẽ restart app.");
                                RestartAppAndWait(deviceId, ocrFunc, log);
                            }
                            return;
                        }
                        else {
                            log?.Invoke("Không tìm thấy/click được PVP, sẽ restart app.");
                            RestartAppAndWait(deviceId, ocrFunc, log);
                            return;
                        }
                    }
                    else if (string.IsNullOrEmpty(screen) || screen == "unknown") {
                        log?.Invoke("Unknown screen, restarting app...");
                        RestartAppAndWait(deviceId, ocrFunc, log);
                        return;
                    }
                }
            }
            // Sau mọi bước, kiểm tra lại lần cuối
            string finalScreen = _screenSvc.DetectScreen(deviceId, log);
            log?.Invoke($"Detected screen after loading/restart: {finalScreen}");
            if (finalScreen == "Main") {
                log?.Invoke("At Main screen, auto into PVP...");
                bool ok = _pvpNav.GoToPvp(deviceId, log);
                if (ok)
                {
                    log?.Invoke("Auto vào PVP thành công.");
                    bool toBattle = await WaitForScreenAsync(deviceId, "ToBattle", log);
                    if (toBattle)
                    {
                        log?.Invoke("Đã vào màn hình ToBattle, bắt đầu tự động PVP...");
                        await _battleSvc.AnalyzeBattle(deviceId, log);
                    }
                    else
                    {
                        log?.Invoke("Không vào được ToBattle sau khi click PVP, sẽ restart app.");
                        RestartAppAndWait(deviceId, ocrFunc, log);
                    }
                }
                else
                    log?.Invoke("Auto vào PVP thất bại sau khi restart app.");
            }
            else if (finalScreen == "ToBattle")
            {
                log?.Invoke("Đang ở trong battle, bắt đầu tự động PVP...");
                await _battleSvc.AnalyzeBattle(deviceId, log);
            }
        }

        private async void WaitForLoading(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log) {
            int timeoutMs = 90000, intervalMs = 3000, waited = 0;
            bool isLoading = true;
            while (isLoading && waited < timeoutMs) {
                await Task.Delay(intervalMs);
                waited += intervalMs;
                isLoading = _screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log);
                if (isLoading)
                    log?.Invoke("Still in loading screen...");
            }
            if (!isLoading)
            {
                log?.Invoke("Loading finished.");
                string screen = _screenSvc.DetectScreen(deviceId, log);
                log?.Invoke($"Detected screen after loading: {screen}");
                if (screen == "Main")
                {
                    log?.Invoke("At Main screen, auto into PVP...");
                    bool ok = _pvpNav.GoToPvp(deviceId, log);
                    if (ok)
                        log?.Invoke("Auto vào PVP thành công.");
                    else
                        log?.Invoke("Auto vào PVP thất bại sau khi loading.");
                }
                else if (string.IsNullOrEmpty(screen) || screen == "unknown")
                {
                    log?.Invoke("Unknown screen sau loading, restarting app...");
                    RestartAppAndWait(deviceId, ocrFunc, log);
                }
            }
        }

        private async void RestartAppAndWait(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log) {
            _appCtrl.Close(deviceId, _packageName);
            await Task.Delay(2000);
            _appCtrl.Open(deviceId, _packageName);
            log?.Invoke("Waiting 30s for app to launch...");
            await Task.Delay(45000);
            WaitForLoading(deviceId, ocrFunc, log);
        }

        private async Task<bool> WaitForScreenAsync(string deviceId, string expectedScreen, Action<string> log, int timeoutMs = 90000, int intervalMs = 3000)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                string screen = _screenSvc.DetectScreen(deviceId, log);
                log?.Invoke($"Đang kiểm tra màn hình: {screen}");
                if (screen == expectedScreen)
                {
                    log?.Invoke($"Đã vào màn hình {expectedScreen}!");
                    return true;
                }
                await Task.Delay(intervalMs);
                waited += intervalMs;
            }
            log?.Invoke($"Quá thời gian chờ, chưa vào được màn hình {expectedScreen}!");
            return false;
        }
    }
}
