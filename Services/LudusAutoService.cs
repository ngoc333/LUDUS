using System;
using System.Drawing;
using System.Threading.Tasks;
using LUDUS.Services;
using System.Threading;

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
                        _lastDefaultScreenTime = DateTime.MinValue;
                        log($">>> ROUND {_round} <<<");
                        await _battleSvc.ClickSpell(deviceId, _round, log);

                        if (_round == 1)
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
                        _round++;
                        await Task.Delay(3000, cancellationToken);
                        break;

                    case "EndBattle":
                        _lastDefaultScreenTime = DateTime.MinValue;
                        log("Battle ended. Resetting round count.");
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
    }
}