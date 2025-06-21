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
        private readonly string _regionsXmlPath;
        private readonly string _templateBasePath;
        private readonly HeroMergeService _mergeService;
        private readonly ScreenDetectionService _screenDetectSvc;

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
            _regionsXmlPath = regionsXmlPath;
            _templateBasePath = templateBasePath;
            _screenDetectSvc = screenDetectSvc;
        }

        /// <summary>
        /// Analyze each battle cell in a 4x5 grid, then merge similar heroes.
        /// </summary>
        public async Task AnalyzeBattle(string deviceId, Action<string> log) {
            var regions = RegionLoader.LoadPresetRegions(_regionsXmlPath);
            int round = 1;
            bool inBattle = true;
            while (inBattle) {
                log?.Invoke($"=== ROUND {round} ===");
                // 1. Nếu round 1 thì click spell
                if (round == 1) {
                    await ClickRegion("SpellsClick", regions, deviceId, log);
                    await Task.Delay(300);
                    await ClickRegion("Spell1", regions, deviceId, log);
                    await Task.Delay(300);
                    await ClickRegion("CastClick", regions, deviceId, log);
                    log?.Invoke("Đã click spell round 1");
                    await Task.Delay(5000);
                    // Click EmptyCoin 20 lần
                    for (int i = 0; i < 20; i++) {
                        await ClickRegion("EmptyCoin", regions, deviceId, log);
                        await Task.Delay(150);
                    }
                }

                // 2. Thời gian merge: 90s hoặc cho đến khi hết coin và hết khả năng merge
                DateTime mergeStart = DateTime.Now;
                bool canMerge = true;
                bool lastCoin = false;
                int lastHeroCount = -1;
                while ((DateTime.Now - mergeStart).TotalSeconds < 90) {
                    log?.Invoke($"[Round {round}] Đang merge...");
                    var mergeResult = await AnalyzeAndMergeWithCount(deviceId, log, regions);
                    int heroCount = mergeResult.heroCount;
                    canMerge = mergeResult.merged;
                    bool emptyCoin = IsEmptyCoin(deviceId, regions, log);

                    if (emptyCoin && (!canMerge || heroCount == lastHeroCount)) {
                        log?.Invoke($"[Round {round}] Hết coin và không còn merge được nữa, kết thúc merge!");
                        break;
                    } else if (!emptyCoin) {
                        // Nếu còn coin, click 10 lần rồi merge tiếp
                        for (int i = 0; i < 10; i++) {
                            await ClickRegion("EmptyCoin", regions, deviceId, log);
                            await Task.Delay(150);
                        }
                    }
                    lastHeroCount = heroCount;
                    await Task.Delay(1000);
                }

                // 3. Click Battle để kết thúc lượt
                await ClickRegion("Battle", regions, deviceId, log);
                log?.Invoke($"[Round {round}] Đã click Battle, chờ sang round mới...");

                // 4. Kiểm tra màn hình mỗi 3s cho đến khi sang round mới hoặc endbattle
                while (true) {
                    await Task.Delay(3000);
                    string screen = DetectScreen(deviceId, log);
                    if (screen == "ToBattle") {
                        round++;
                        break;
                    } else if (screen == "EndBattle") {
                        log?.Invoke("Đã kết thúc trận đấu!");
                        inBattle = false;
                        break;
                    } else {
                        log?.Invoke($"Đang chờ sang round mới, màn hình hiện tại: {screen}");
                    }
                }
            }
        }

        private async Task ClickRegion(string regionName, List<RegionInfo> regions, string deviceId, Action<string> log) {
            var reg = regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == regionName);
            if (reg == null) {
                log?.Invoke($"Không tìm thấy region {regionName}");
                return;
            }
            int x = reg.Rect.X + reg.Rect.Width / 2;
            int y = reg.Rect.Y + reg.Rect.Height / 2;
            _adb.Run($"-s {deviceId} shell input tap {x} {y}");
            log?.Invoke($"Đã click {regionName} tại ({x},{y})");
            await Task.CompletedTask;
        }

        private async Task<(bool merged, int heroCount)> AnalyzeAndMergeWithCount(string deviceId, Action<string> log, List<RegionInfo> regions) {
            var baseCell = regions.First(r => r.Group == "MySideCell" && r.Name == "C1").Rect;
            const int rows = 4, cols = 5;
            var cellRects = new List<Rectangle>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    cellRects.Add(new Rectangle(baseCell.X + c * baseCell.Width,
                                               baseCell.Y + r * baseCell.Height,
                                               baseCell.Width,
                                               baseCell.Height));
            var heroInfo = regions.Where(r => r.Group == "HeroInfo").ToList();
            var rectCheck = heroInfo.First(r => r.Name == "HeroCheck").Rect;
            var rectName = heroInfo.First(r => r.Name == "Name").Rect;
            var rectLv = heroInfo.First(r => r.Name == "LV").Rect;
            Bitmap screenshot = _capture.Capture(deviceId) as Bitmap;
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
                    screenshot.Dispose();
                    screenshot = _capture.Capture(deviceId) as Bitmap;
                    using (var bmpCheck = screenshot.Clone(rectCheck, screenshot.PixelFormat))
                    using (var tpl = new Bitmap(Path.Combine(_templateBasePath, "Battle", "HeroCheck.png"))) {
                        if (!ImageCompare.AreSame(bmpCheck, tpl)) continue;
                    }
                    string name;
                    using (var bmpName = screenshot.Clone(rectName, screenshot.PixelFormat)) {
                        var folder = Path.Combine(_templateBasePath, "Battle", "HeroName");
                        name = TryMatchTemplate(bmpName, folder);
                        if (string.IsNullOrEmpty(name)) {
                            name = _ocr.Recognize(bmpName)?.Trim();
                            if (!string.IsNullOrEmpty(name))
                                bmpName.Save(Path.Combine(folder, name + ".png"));
                        }
                    }
                    string lv;
                    using (var bmpLv = screenshot.Clone(rectLv, screenshot.PixelFormat)) {
                        var folderLv = Path.Combine(_templateBasePath, "Battle", "LV");
                        lv = TryMatchTemplate(bmpLv, folderLv);
                    }
                    results.Add(new CellResult { Index = i, HeroName = name, Level = lv, CellRect = rect });
                }
            } finally {
                screenshot.Dispose();
            }
            int beforeMerge = results.Count;
            _mergeService.MergeHeroes(deviceId, results, cols, log);
            // Nếu số lượng hero sau merge giảm thì có merge được
            return (beforeMerge > 0, beforeMerge);
        }

        private bool IsEmptyCoin(string deviceId, List<RegionInfo> regions, Action<string> log) {
            var reg = regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "EmptyCoin");
            if (reg == null) return false;
            using (var bmp = _capture.Capture(deviceId) as Bitmap)
            using (var crop = bmp.Clone(reg.Rect, bmp.PixelFormat))
            using (var tpl = new Bitmap(Path.Combine(_templateBasePath, "Battle", "EmptyCoin.png"))) {
                bool same = ImageCompare.AreSame(crop, tpl);
                log?.Invoke($"So sánh EmptyCoin: {(same ? "Hết coin" : "Còn coin")}");
                return same;
            }
        }

        private string DetectScreen(string deviceId, Action<string> log) {
            return _screenDetectSvc.DetectScreen(deviceId, log);
        }

        private string GetPosition(int index, int cols) {
            int row = index / cols;
            int col = index % cols;
            return $"{row}_{col}";
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
    }
}
