using System;
using System.Drawing;
using System.Threading.Tasks;
using LUDUS.Services;

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

        public async Task RunAsync(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log)
        {
            while (true)
            {
                if (!EnsureAppRunning(deviceId, log))
                {
                    _appCtrl.Open(deviceId, _packageName);
                    log("App opened. Waiting 45s...");
                    await Task.Delay(45000);
                }

                if (_screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log))
                {
                    log("Loading screen detected...");
                    await WaitForLoading(deviceId, ocrFunc, log);
                }

                string screen = _screenSvc.DetectScreen(deviceId, log);

                switch (screen)
                {
                    case "Main":
                        if (_pvpNav.GoToPvp(deviceId, log))
                            await WaitForToBattle(deviceId, log);
                        break;

                    case "ToBattle":
                        await HandleBattle(deviceId, log);
                        break;

                    case "EndBattle":
                        log("Battle ended.");
                        break;

                    case "WaitPvp":
                    case "PVP":
                        await Task.Delay(3000);
                        continue;

                    case "Other":
                        log("Other screen - wait and detect again.");
                        await Task.Delay(3000);
                        continue;

                    case "unknown":
                    default:
                        log("Unknown screen - restarting app.");
                        RestartApp(deviceId, log);
                        await Task.Delay(45000);
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

        private async Task WaitForLoading(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log)
        {
            int waited = 0;
            while (waited < 90000)
            {
                await Task.Delay(3000);
                waited += 3000;
                if (!_screenSvc.IsScreenLoadingByOcr(deviceId, ocrFunc, log))
                {
                    log("Loading finished.");
                    return;
                }
                log("Still loading...");
            }
            log("Timeout while loading - restarting.");
            RestartApp(deviceId, log);
        }

        private async Task WaitForToBattle(string deviceId, Action<string> log)
        {
            int waited = 0;
            while (waited < 90000)
            {
                string s = _screenSvc.DetectScreen(deviceId, log);
                if (s == "ToBattle") return;
                await Task.Delay(3000);
                waited += 3000;
            }
            log("Failed to reach ToBattle - restarting.");
            RestartApp(deviceId, log);
        }

        private void RestartApp(string deviceId, Action<string> log)
        {
            _appCtrl.Close(deviceId, _packageName);
            Task.Delay(2000).Wait();
            _appCtrl.Open(deviceId, _packageName);
        }

        private async Task HandleBattle(string deviceId, Action<string> log)
        {
            int round = 1;
            while (true)
            {
                log($">>> ROUND {round} <<<");

                if (round == 1)
                {
                    await _battleSvc.ClickSpell(deviceId, log);
                    await _battleSvc.ClickEmptyCoin(deviceId, 20, log);
                }

                DateTime start = DateTime.Now;
                while ((DateTime.Now - start).TotalSeconds < 90)
                {
                    if (!_battleSvc.IsInBattleScreen(deviceId, log)) break;

                    await _battleSvc.ClickEmptyCoin(deviceId, 10, log);
                    bool merged = await _battleSvc.AnalyzeAndMerge(deviceId, log);
                    if (!merged) break;

                    await Task.Delay(1000);
                }

                await _battleSvc.ClickEndRound(deviceId, log);
                await Task.Delay(3000);
                round++;
            }
        }
    }
}