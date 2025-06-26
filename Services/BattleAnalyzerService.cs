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

            await ClickRegion("SpellsClick", deviceId, log, false);
            await Task.Delay(300);

            string spellToClick = $"Spell{round}";
            await ClickRegion(spellToClick, deviceId, log, false);
            await Task.Delay(300);

            await ClickRegion("CastClick", deviceId, log, false);
            log?.Invoke($"Spell {round}");
            await Task.Delay(1000); // Wait for spell animation
        }

        public async Task ClickCoin(string deviceId, int count, Action<string> log)
        {
            log?.Invoke($"Coin x{count}");
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
            var (merged, _) = await AnalyzeAndMergeWithCount(deviceId, log);
            if (merged) log?.Invoke("Merge ✓");
            return merged;
        }

        public async Task ClickEndRound(string deviceId, Action<string> log)
        {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("ToBattle", deviceId, log);
        }

        public async Task ClickClamContinue(string deviceId, Action<string> log) {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("ClamContinue", deviceId, log);
        }

        public async Task ClickCombatBoosts(string deviceId, Action<string> log) {
            try
            {
                log?.Invoke("Đang xử lý CombatBoosts...");
                
                // Click vào button đầu tiên (nếu có)
                await ClickRegion("CombatBoostsClick", deviceId, log);
                await Task.Delay(3000);
                
                // Click vào button thứ hai (nếu có)
                await ClickRegion("CombatBoostsClick2", deviceId, log);
                await Task.Delay(500);
               
                log?.Invoke("Hoàn thành xử lý CombatBoosts");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Lỗi khi xử lý CombatBoosts: {ex.Message}");
            }
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
                if (verbose) log?.Invoke($"Region '{regionName}' not found.");
                return;
            }
            int x = reg.Rect.X;
            int y = reg.Rect.Y;
            _adb.Run($"-s {deviceId} shell input tap {x} {y}");
            if (verbose)
            {
                log?.Invoke($"Click: {regionName}");
            }
            await Task.CompletedTask;
        }

        private async Task<(bool merged, int heroCount)> AnalyzeAndMergeWithCount(string deviceId, Action<string> log)
        {
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

            // 1. Quét bàn cờ 1 lần duy nhất
            List<CellResult> results = new List<CellResult>();
            using (Bitmap screenshot = _capture.Capture(deviceId) as Bitmap)
            {
                for (int i = 0; i < cellRects.Count; i++)
                {
                    var rect = cellRects[i];
                    using (var bmpCell = screenshot.Clone(rect, screenshot.PixelFormat))
                    {
                        if (IsEmptyCell(bmpCell)) continue;
                    }
                    // Click để lấy thông tin hero
                    var px = rect.X + rect.Width / 2;
                    var py = rect.Y + rect.Height / 2;
                    _adb.Run($"-s {deviceId} shell input tap {px} {py}");
                    await Task.Delay(100);
                    using (var newScreenshot = _capture.Capture(deviceId) as Bitmap)
                    {
                        using (var bmpCheck = newScreenshot.Clone(rectCheck, newScreenshot.PixelFormat))
                        using (var tpl = new Bitmap(Path.Combine(_templateBasePath, "Battle", "HeroCheck.png")))
                        {
                            if (!ImageCompare.AreSame(bmpCheck, tpl)) {
                                log?.Invoke($"[CHECK HERO] Index: {i}, Name: Stone, Level: -1");
                                results.Add(new CellResult { Index = i, HeroName = "Stone", Level = "-1", CellRect = rect });
                                continue;
                            }
                        }
                        string name;
                        using (var bmpName = newScreenshot.Clone(rectName, newScreenshot.PixelFormat))
                        {
                            var folder = Path.Combine(_templateBasePath, "Battle", "HeroName");
                            name = TryMatchTemplate(bmpName, folder);
                            if (string.IsNullOrEmpty(name))
                            {
                                name = _ocr.Recognize(bmpName)?.Trim();
                                if (!string.IsNullOrEmpty(name) && !File.Exists(Path.Combine(folder, name + ".png")))
                                    bmpName.Save(Path.Combine(folder, name + ".png"));
                            }
                        }
                        string lv;
                        using (var bmpLv = newScreenshot.Clone(rectLv, newScreenshot.PixelFormat))
                        {
                            var folderLv = Path.Combine(_templateBasePath, "Battle", "LV");
                            lv = TryMatchTemplate(bmpLv, folderLv);
                        }
                        log?.Invoke($"[CHECK HERO] Index: {i}, Name: {name}, Level: {lv}");
                        results.Add(new CellResult { Index = i, HeroName = name, Level = lv, CellRect = rect });
                    }
                }
            }

            if (results.Count < 2)
            {
                return (false, results.Count);
            }

            // 2. Merge liên tục trong bộ nhớ, ưu tiên vị trí trung tâm
            bool anyMergeHappened = false;
            bool didMerge;
            do
            {
                didMerge = false;
                // Nhóm theo tên hero, chỉ lấy level < 4
                var heroGroups = results
                    .Where(c => !string.IsNullOrEmpty(c.HeroName) && c.HeroName != "Stone" && int.TryParse(c.Level, out var lv) && lv < 4)
                    .GroupBy(c => c.HeroName);

                foreach (var group in heroGroups)
                {
                    var name = group.Key;
                    // Nhóm tiếp theo level
                    var levelMap = group.GroupBy(c => int.Parse(c.Level))
                        .ToDictionary(g => g.Key, g => g.ToList());
                    foreach (var level in levelMap.Keys.OrderBy(l => l))
                    {
                        var list = levelMap[level];
                        if (list.Count >= 2)
                        {
                            // Ưu tiên merge 2 hero ở vị trí trung tâm hơn
                            var centerCol = GridCols / 2.0;
                            var centerRow = rows / 2.0;
                            var sorted = list.OrderBy(c => Math.Abs((c.Index % GridCols) - centerCol) + Math.Abs((c.Index / GridCols) - centerRow)).ToList();
                            var first = sorted[0];
                            var second = sorted[1];
                            // Thực hiện swipe từ second vào first
                            var p1 = new System.Drawing.Point(
                                first.CellRect.X + first.CellRect.Width / 2,
                                first.CellRect.Y + first.CellRect.Height / 2);
                            var p2 = new System.Drawing.Point(
                                second.CellRect.X + second.CellRect.Width / 2,
                                second.CellRect.Y + second.CellRect.Height / 2);
                            bool mergeSuccess = false;
                            int mergeTry = 0;
                            for (; mergeTry < 2; mergeTry++)
                            {
                                _adb.Run($"-s {deviceId} shell input swipe {p2.X} {p2.Y} {p1.X} {p1.Y} 100");
                                await Task.Delay(200); // Đợi thao tác merge
                                // Kiểm tra lại ô nguồn (second.Index) có trống không
                                using (var checkScreenshot = _capture.Capture(deviceId) as Bitmap)
                                using (var bmpSource = checkScreenshot.Clone(second.CellRect, checkScreenshot.PixelFormat))
                                {
                                    if (IsEmptyCell(bmpSource))
                                    {
                                        mergeSuccess = true;
                                        break;
                                    }
                                }
                                log?.Invoke($"[MERGE] Ô nguồn {second.Index} chưa trống, thử lại lần {mergeTry + 1}");
                                await Task.Delay(200);
                            }
                            if (!mergeSuccess)
                            {
                                log?.Invoke($"[MERGE-FAIL] Merge thất bại tại cell {second.Index}->{first.Index}, bỏ qua cặp này!");
                                continue;
                            }
                            log?.Invoke($"Merged {name} lvl{level}: cell {second.Index}->{first.Index}");

                            // Cập nhật lại danh sách hero trong bộ nhớ
                            results.RemoveAll(c => c.Index == first.Index || c.Index == second.Index);
                            // Thêm hero mới vào vị trí first với level+1
                            results.Add(new CellResult
                            {
                                Index = first.Index,
                                HeroName = name,
                                Level = (level + 1).ToString(),
                                CellRect = first.CellRect
                            });

                            // Sau mỗi lần merge, thử click coin để roll thêm hero nếu còn slot trống
                            if (results.Count < GridCols * rows)
                            {
                                await ClickCoin(deviceId, 1, log);
                                await Task.Delay(200); // Đợi hero mới xuất hiện
                                // Không quét lại bàn cờ, chỉ tăng số lượng hero nếu có
                            }

                            didMerge = true;
                            anyMergeHappened = true;
                            break; // Chỉ merge 1 cặp mỗi lần, sau đó lặp lại để cập nhật danh sách
                        }
                    }
                    if (didMerge) break;
                }
            } while (didMerge);

            return (anyMergeHappened, results.Count);
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
