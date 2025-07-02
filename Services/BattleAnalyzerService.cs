using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LUDUS.Services;
using LUDUS.Utils;

namespace LUDUS.Services {
    // Model for each cell analysis result
    public class CellResult {
        public int Index { get; set; }
        public string HeroName { get; set; }
        public string Level { get; set; }
        public Rectangle CellRect { get; set; }
    }

    public class HeroInfo {
        public int Row { get; set; }
        public int Col { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public Rectangle CellRect { get; set; }
    }

    // Service to analyze battle cells
    public class BattleAnalyzerService {
        private readonly ScreenCaptureService _capture;
        private readonly AdbService _adb;
        private readonly HeroNameOcrService _ocr;
        private readonly string _templateBasePath;
        private readonly HeroMergeService _mergeService;
        private readonly ScreenDetectionService _screenDetectSvc;
        private readonly RoundDetectionService _roundDetectionSvc;
        private readonly List<RegionInfo> _regions;
        private const int GridCols = 5;

        // Lưu trữ những cặp merge đã thất bại để tránh thử lại
        private HashSet<string> _failedMergePairs = new HashSet<string>();

        public BattleAnalyzerService(
            ScreenCaptureService captureService,
            AdbService adbService,
            HeroNameOcrService ocrService,
            HeroMergeService mergeService,
            string regionsXmlPath,
            string templateBasePath,
            ScreenDetectionService screenDetectSvc) {
            _capture = captureService;
            _adb = adbService;
            _ocr = ocrService;
            _mergeService = mergeService;
            _templateBasePath = templateBasePath;
            _screenDetectSvc = screenDetectSvc;
            _regions = RegionLoader.LoadPresetRegions(regionsXmlPath);
            _roundDetectionSvc = new RoundDetectionService(captureService, regionsXmlPath, templateBasePath);
        }

        public async Task ClickSpell(string deviceId, int round, Action<string> log) {
            if (round < 1 || round > 5) {
                log?.Invoke($"No spell to use for round {round}.");
                return;
            }

            await ClickRegion("SpellsClick", deviceId, log, false);
            await Task.Delay(100);

            string spellToClick = $"Spell{round}";
            await ClickRegion(spellToClick, deviceId, log, false);
            await Task.Delay(100);

            await ClickRegion("CastClick", deviceId, log, false);
            log?.Invoke($"Spell {round}");
            //await Task.Delay(1000); // Wait for spell animation
        }

        public async Task ClickCoin(string deviceId, int count, Action<string> log) {
            log?.Invoke($"Coin x{count}");
            for (int i = 0; i < count; i++) {
                await ClickRegion("Coin", deviceId, log, false); // Don't log every single click
                await Task.Delay(100);
            }
        }

        public async Task<bool> IsInBattleScreen(string deviceId, Action<string> log) {
            string screen = _screenDetectSvc.DetectScreen(deviceId, log);
            // In the new logic, "ToBattle" is named "Battle"
            return screen == "Battle" || screen == "ToBattle";
        }

        public async Task<bool> AnalyzeAndMerge(string deviceId, Action<string> log) {
            var (merged, _) = await AnalyzeAndMergeWithCount(deviceId, log);
            if (merged) log?.Invoke("Merge ✓");
            return merged;
        }

        public void ResetFailedMergePairs() {
            _failedMergePairs.Clear();
        }

        public async Task ClickEndRound(string deviceId, Action<string> log) {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("ToBattle", deviceId, log);
        }

        public async Task ClickClamContinue(string deviceId, Action<string> log) {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("ClamContinue", deviceId, log);
        }

        public async Task ClickCombatBoosts(string deviceId, Action<string> log) {
            try {
                log?.Invoke("Đang xử lý CombatBoosts...");

                // Click vào button đầu tiên (nếu có)
                await ClickRegion("CombatBoostsClick", deviceId, log, false);
                await Task.Delay(100);

                // Click vào button thứ hai (nếu có)
                await ClickRegion("CombatBoostsClick2", deviceId, log, false);
                await Task.Delay(100);

                log?.Invoke("Hoàn thành xử lý CombatBoosts");
            } catch (Exception ex) {
                log?.Invoke($"Lỗi khi xử lý CombatBoosts: {ex.Message}");
            }
        }

        public async Task ClickLoseAndYes(string deviceId, Action<string> log) {
            await ClickRegion("Lose", deviceId, log);
            await Task.Delay(500);
            await ClickRegion("Yes", deviceId, log);
            // await Task.Delay(500);
        }

        private async Task ClickRegion(string regionName, string deviceId, Action<string> log, bool verbose = true) {
            // Ưu tiên tìm region theo group MySideCell trước, nếu không có thì lấy theo tên
            var reg = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == regionName)
                    ?? _regions.FirstOrDefault(r => r.Name == regionName);
            if (reg == null) {
                if (verbose) log?.Invoke($"Region '{regionName}' not found.");
                return;
            }
            int x = reg.Rect.X;
            int y = reg.Rect.Y;
            _adb.RunShellPersistent($"input tap {x} {y}");
            if (verbose) {
                log?.Invoke($"Click: {regionName}");
            }
            await Task.CompletedTask;
        }

        private async Task<(bool merged, int heroCount)> AnalyzeAndMergeWithCount(string deviceId, Action<string> log) {
            const int rows = 4;
            // Sử dụng các hàm tiện ích từ BattleAnalyzerUtils
            var cellRects = BattleAnalyzerUtils.GenerateCellRects(_regions, rows, GridCols, log);
            var (rectCheck, rectName, rectLv) = BattleAnalyzerUtils.GetHeroInfoRects(_regions, log);

            // 1. Quét bàn cờ 1 lần duy nhất
            List<CellResult> results = await BattleAnalyzerUtils.ScanBoardAsync(
                _capture, _adb, _ocr, _templateBasePath, _regions, deviceId, cellRects, rectCheck, rectName, rectLv, log);

            if (results.Count < 2) {
                return (false, results.Count);
            }

            // 2. Merge liên tục trong bộ nhớ, ưu tiên vị trí trung tâm
            bool anyMergeHappened = false;
            bool didMerge;
            do {
                didMerge = await BattleAnalyzerUtils.TryMergeAsync(
                    _capture, _adb, _templateBasePath, deviceId, _failedMergePairs, GridCols, results, rows, log,
                    (name, count, logger) => ClickCoin(deviceId, count, logger));
                if (didMerge)
                    anyMergeHappened = true;
            } while (didMerge);

            return (anyMergeHappened, results.Count);
        }
    }
}
