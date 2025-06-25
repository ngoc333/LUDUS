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

        public async Task ClickSpell(string deviceId, int round, Action<string> log)
        {
            if (round < 1 || round > 5)
            {
                log?.Invoke($"No spell to use for round {round}.");
                return;
            }

            await ClickRegion("SpellsClick", deviceId, log);
            await Task.Delay(300);

            string spellToClick = $"Spell{round}";
            await ClickRegion(spellToClick, deviceId, log);
            await Task.Delay(300);

            await ClickRegion("CastClick", deviceId, log);
            log?.Invoke($"Used {spellToClick}.");
            await Task.Delay(1000); // Wait for spell animation
        }

        public async Task ClickCoin(string deviceId, int count, Action<string> log)
        {
            log?.Invoke($"Clicking Coin {count} times.");
            for (int i = 0; i < count; i++)
            {
                await ClickRegion("Coin", deviceId, log, false); // Don't log every single click
                await Task.Delay(150);
            }
        }

        public async Task<bool> IsInBattleScreen(string deviceId, Action<string> log)
        {
            string screen =  _screenDetectSvc.DetectScreen(deviceId, log);
            // In the new logic, "ToBattle" is named "Battle"
            return screen == "Battle" || screen == "ToBattle";
        }

        public async Task<bool> AnalyzeAndMerge(string deviceId, Action<string> log)
        {
            log?.Invoke("Analyzing board for merging...");
            var (merged, _) = await AnalyzeAndMergeWithCount(deviceId, log);
            log?.Invoke(merged ? "Merge was successful." : "No merge occurred.");
            return merged;
        }

        public async Task ClickEndRound(string deviceId, Action<string> log)
        {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("Battle", deviceId, log);
            log?.Invoke("End Round (Battle button).");
        }

        public async Task ClickClamContinue(string deviceId, Action<string> log) {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("ClamContinue", deviceId, log);
            log?.Invoke("Clam Continue");
        }

        public async Task ClickCombatBoosts(string deviceId, Action<string> log) {
            // The button to end the round is named "Battle" in regions.xml
          //  await ClickRegion("CombatBoostsClick", deviceId, log);
            await Task.Delay(500);
           // await ClickRegion("CombatBoostsClick2", deviceId, log);
            //log?.Invoke("Click Combat Boosts");
        }

        public async Task ClickLoseAndYes(string deviceId, Action<string> log)
        {
            await ClickRegion("Lose", deviceId, log);
            await Task.Delay(500);
            await ClickRegion("Yes", deviceId, log);
            await Task.Delay(500);
        }

        private async Task ClickRegion(string regionName, string deviceId, Action<string> log, bool verbose = true) {
            // Ưu tiên tìm region theo group MySideCell trước, nếu không có thì lấy theo tên
            var reg = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == regionName)
                    ?? _regions.FirstOrDefault(r => r.Name == regionName);
            if (reg == null) {
                log?.Invoke($"Region '{regionName}' not found.");
                return;
            }
            int x = reg.Rect.X + reg.Rect.Width / 2;
            int y = reg.Rect.Y + reg.Rect.Height / 2;
            _adb.Run($"-s {deviceId} shell input tap {x} {y}");
            if (verbose)
            {
                log?.Invoke($"Clicked {regionName} at ({x},{y})");
            }
            await Task.CompletedTask;
        }

        private async Task<(bool merged, int heroCount)> AnalyzeAndMergeWithCount(string deviceId, Action<string> log) {
            var baseCell = _regions.First(r => r.Group == "MySideCell" && r.Name == "C1").Rect;
            const int rows = 4;
            var cellRects = new List<Rectangle>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < GridCols; c++)
                    cellRects.Add(new Rectangle(baseCell.X + c * baseCell.Width,
                                               baseCell.Y + r * baseCell.Height,
                                               baseCell.Width,
                                               baseCell.Height));
            var heroInfo = _regions.Where(r => r.Group == "HeroInfo").ToList();
            var rectCheck = heroInfo.First(r => r.Name == "HeroCheck").Rect;
            var rectName = heroInfo.First(r => r.Name == "Name").Rect;
            var rectLv = heroInfo.First(r => r.Name == "LV").Rect;
            
            using(Bitmap screenshot = _capture.Capture(deviceId) as Bitmap)
            {
                var results = new List<CellResult>();
                try {
                    for (int i = 0; i < cellRects.Count; i++) {
                        var rect = cellRects[i];
                        using (var bmpCell = screenshot.Clone(rect, screenshot.PixelFormat)) {
                            if (IsEmptyCell(bmpCell)) continue;
                        }
                        var px = rect.X + rect.Width / 2;
                        var py = rect.Y + rect.Height / 2;
                        _adb.Run($"-s {deviceId} shell input tap {px} {py}");
                        await Task.Delay(100);
                        
                        using (var newScreenshot = _capture.Capture(deviceId) as Bitmap)
                        {
                            using (var bmpCheck = newScreenshot.Clone(rectCheck, newScreenshot.PixelFormat))
                            using (var tpl = new Bitmap(Path.Combine(_templateBasePath, "Battle", "HeroCheck.png"))) {
                                if (!ImageCompare.AreSame(bmpCheck, tpl)) continue;
                            }
                            string name;
                            using (var bmpName = newScreenshot.Clone(rectName, newScreenshot.PixelFormat)) {
                                var folder = Path.Combine(_templateBasePath, "Battle", "HeroName");
                                name = TryMatchTemplate(bmpName, folder);
                                if (string.IsNullOrEmpty(name)) {
                                    name = _ocr.Recognize(bmpName)?.Trim();
                                    if (!string.IsNullOrEmpty(name) && !File.Exists(Path.Combine(folder, name + ".png")))
                                        bmpName.Save(Path.Combine(folder, name + ".png"));
                                }
                            }
                            string lv;
                            using (var bmpLv = newScreenshot.Clone(rectLv, newScreenshot.PixelFormat)) {
                                var folderLv = Path.Combine(_templateBasePath, "Battle", "LV");
                                lv = TryMatchTemplate(bmpLv, folderLv);
                            }
                            results.Add(new CellResult { Index = i, HeroName = name, Level = lv, CellRect = rect });
                        }
                    }
                } finally {
                    // No need to dispose screenshot here due to the outer using block
                }
                
                if (results.Count < 2)
                {
                    return (false, results.Count);
                }

                bool didMerge = _mergeService.MergeHeroes(deviceId, results, GridCols, log);
                return (didMerge, results.Count);
            }
        }

        private bool IsEmptyCoin(string deviceId, Action<string> log) {
            var reg = _regions.FirstOrDefault(r => r.Name == "EmptyCoin");
            if (reg == null) return false;
            using (var bmp = _capture.Capture(deviceId) as Bitmap)
            using (var crop = bmp.Clone(reg.Rect, bmp.PixelFormat))
            using (var tpl = new Bitmap(Path.Combine(_templateBasePath, "Battle", "EmptyCoin.png"))) {
                bool same = ImageCompare.AreSame(crop, tpl);
                log?.Invoke($"Is Coin Empty? {(same ? "Yes" : "No")}");
                return same;
            }
        }

        private bool IsEmptyCell(Bitmap bmp) {
            const int PATCH = 20, TH = 20;
            int w = bmp.Width, h = bmp.Height;
            int cx = Math.Max(0, w / 2 - PATCH / 2);
            int cy = Math.Max(0, h / 2 - PATCH / 2);
            int minB = 255, maxB = 0;
            for (int y = cy; y < cy + PATCH && y < h; y++)
                for (int x = cx; x < cx + PATCH && x < w; x++) {
                    var c = bmp.GetPixel(x, y);
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    minB = Math.Min(minB, gray);
                    maxB = Math.Max(maxB, gray);
                    if (maxB - minB >= TH) {
                        bmp.Dispose();
                        return false;
                    }
                }
            bmp.Dispose();
            return true;
        }

        private string TryMatchTemplate(Bitmap crop, string folderPath) {
            if (!Directory.Exists(folderPath)) return null;
            foreach (var file in Directory.GetFiles(folderPath, "*.png")) {
                using (var tpl = new Bitmap(file)) {
                    if (ImageCompare.AreSame(crop, tpl))
                        return Path.GetFileNameWithoutExtension(file);
                }
            }
            return null;
        }

        /// <summary>
        /// Kiểm tra có phải round 1 hay không
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu là round 1</returns>
        public async Task<bool> IsRound1(string deviceId, Action<string> log)
        {
            return await _roundDetectionSvc.IsRound1(deviceId, log);
        }

        /// <summary>
        /// Lấy thông tin chi tiết về round hiện tại
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>Thông tin chi tiết về round</returns>
        public async Task<RoundInfo> GetRoundInfo(string deviceId, Action<string> log)
        {
            return await _roundDetectionSvc.GetRoundInfo(deviceId, log);
        }

        /// <summary>
        /// Lưu file hình của Life1 và Life2 để kiểm tra
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu lưu thành công</returns>
        public async Task<bool> SaveLifeRegions(string deviceId, Action<string> log)
        {
            return await _roundDetectionSvc.SaveLifeRegions(deviceId, log);
        }

        /// <summary>
        /// Lưu template lifeEmpty.png để debug
        /// </summary>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu lưu thành công</returns>
        public async Task<bool> SaveTemplateForDebug(Action<string> log)
        {
            return await _roundDetectionSvc.SaveTemplateForDebug(log);
        }
    }
}
