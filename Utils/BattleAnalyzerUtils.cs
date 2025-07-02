using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LUDUS.Services;

namespace LUDUS.Utils {
    public static class BattleAnalyzerUtils {
        public static bool IsEmptyCell(Bitmap bmp, Action<string> log) {
            try {
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
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.IsEmptyCell] Lỗi: {ex.Message}");
                throw;
            }
        }

        public static string TryMatchTemplate(Bitmap crop, string folderPath, Action<string> log) {
            try {
                if (!Directory.Exists(folderPath)) return null;
                foreach (var file in Directory.GetFiles(folderPath, "*.png")) {
                    using (var tpl = new Bitmap(file)) {
                        if (ImageCompare.AreSame(crop, tpl))
                            return Path.GetFileNameWithoutExtension(file);
                    }
                }
                return null;
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.TryMatchTemplate] Lỗi: {ex.Message}");
                throw;
            }
        }

        public static List<Rectangle> GenerateCellRects(List<RegionInfo> regions, int rows, int gridCols, Action<string> log) {
            try {
                var baseCell = regions.First(r => r.Group == "MySideCell" && r.Name == "C1").Rect;
                var cellRects = new List<Rectangle>();
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < gridCols; c++)
                        cellRects.Add(new Rectangle(baseCell.X + c * baseCell.Width,
                                                   baseCell.Y + r * baseCell.Height,
                                                   baseCell.Width,
                                                   baseCell.Height));
                return cellRects;
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.GenerateCellRects] Lỗi: {ex.Message}");
                throw;
            }
        }

        public static (Rectangle rectCheck, Rectangle rectName, Rectangle rectLv) GetHeroInfoRects(List<RegionInfo> regions, Action<string> log) {
            try {
                var heroInfo = regions.Where(r => r.Group == "HeroInfo").ToList();
                var rectCheck = heroInfo.First(r => r.Name == "HeroCheck").Rect;
                var rectName = heroInfo.First(r => r.Name == "Name").Rect;
                var rectLv = heroInfo.First(r => r.Name == "LV").Rect;
                return (rectCheck, rectName, rectLv);
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.GetHeroInfoRects] Lỗi: {ex.Message}");
                throw;
            }
        }

        public static async Task<List<CellResult>> ScanBoardAsync(
            ScreenCaptureService capture, AdbService adb, HeroNameOcrService ocr,
            string templateBasePath, List<RegionInfo> regions,
            string deviceId, List<Rectangle> cellRects, Rectangle rectCheck, Rectangle rectName, Rectangle rectLv, Action<string> log) {
            try {
                List<CellResult> results = new List<CellResult>();
                using (Bitmap screenshot = capture.Capture(deviceId) as Bitmap) {
                    for (int i = 0; i < cellRects.Count; i++) {
                        var rect = cellRects[i];
                        using (var bmpCell = screenshot.Clone(rect, screenshot.PixelFormat)) {
                            if (IsEmptyCell(bmpCell, log)) continue;
                        }
                        // Click để lấy thông tin hero
                        var px = rect.X + rect.Width / 2;
                        var py = rect.Y + rect.Height / 2;
                        adb.RunShellPersistent($"input tap {px} {py}");
                        await Task.Delay(100);
                        using (var newScreenshot = capture.Capture(deviceId) as Bitmap) {
                            using (var bmpCheck = newScreenshot.Clone(rectCheck, newScreenshot.PixelFormat))
                            using (var tpl = new Bitmap(Path.Combine(templateBasePath, "Battle", "HeroCheck.png"))) {
                                if (!ImageCompare.AreSame(bmpCheck, tpl)) {
                                    log?.Invoke($"[CHECK HERO] Index: {i}, Name: Stone, Level: -1");
                                    results.Add(new CellResult { Index = i, HeroName = "Stone", Level = "-1", CellRect = rect });
                                    continue;
                                }
                            }
                            string name;
                            using (var bmpName = newScreenshot.Clone(rectName, newScreenshot.PixelFormat)) {
                                var folder = Path.Combine(templateBasePath, "Battle", "HeroName");
                                name = TryMatchTemplate(bmpName, folder, log);
                                if (string.IsNullOrEmpty(name)) {
                                    name = ocr.Recognize(bmpName)?.Trim();
                                    if (!string.IsNullOrEmpty(name) && !File.Exists(Path.Combine(folder, name + ".png")))
                                        bmpName.Save(Path.Combine(folder, name + ".png"));
                                }
                            }
                            string lv;
                            using (var bmpLv = newScreenshot.Clone(rectLv, newScreenshot.PixelFormat)) {
                                var folderLv = Path.Combine(templateBasePath, "Battle", "LV");
                                lv = TryMatchTemplate(bmpLv, folderLv, log);
                            }
                            log?.Invoke($"[CHECK HERO] Index: {i}, Name: {name}, Level: {lv}");
                            results.Add(new CellResult { Index = i, HeroName = name, Level = lv, CellRect = rect });
                        }
                    }
                }
                return results;
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.ScanBoardAsync] Lỗi: {ex.Message}");
                throw;
            }
        }

        public static async Task<bool> TryMergeAsync(
    ScreenCaptureService capture, AdbService adb, string templateBasePath,
    string deviceId,
    HashSet<string> failedMergePairs, int gridCols, List<CellResult> results, int rows, Action<string> log,
    Func<string, int, Action<string>, Task> clickCoin) {
            try {
                bool didMerge = false;
                bool mergedAny;
                do {
                    mergedAny = false;
                    // Nhóm theo tên hero, chỉ lấy level < 4
                    var heroGroups = results
                        .Where(c => !string.IsNullOrEmpty(c.HeroName) && c.HeroName != "Stone" && int.TryParse(c.Level, out var lv) && lv < 4)
                        .GroupBy(c => c.HeroName);

                    foreach (var group in heroGroups) {
                        var name = group.Key;
                        // Nhóm tiếp theo level
                        var levelMap = group.GroupBy(c => int.Parse(c.Level))
                            .ToDictionary(g => g.Key, g => g.ToList());
                        foreach (var level in levelMap.Keys.OrderBy(l => l)) {
                            var list = levelMap[level];
                            if (list.Count >= 2) {
                                // Ưu tiên các cặp gần trung tâm trước
                                var centerCol = gridCols / 2.0;
                                var centerRow = rows / 2.0;
                                //var sorted = list.OrderBy(c => Math.Abs((c.Index % gridCols) - centerCol) + Math.Abs((c.Index / gridCols) - centerRow)).ToList();
                                var sorted = list
    .OrderBy(c => Math.Abs((c.Index % gridCols) - centerCol) + Math.Abs((c.Index / gridCols) - centerRow)) // Ưu tiên gần trung tâm nhất
    .ThenBy(c => c.Index / gridCols) // Nếu bằng nhau thì ưu tiên row nhỏ (phía trên)
    .ToList();
                                for (int i = 0; i < sorted.Count; i++) {
                                    for (int j = i + 1; j < sorted.Count; j++) {
                                        var first = sorted[i];
                                        var second = sorted[j];
                                        string mergeKey = $"{first.Index}-{second.Index}-{name}-{level}";
                                        if (failedMergePairs.Contains(mergeKey)) {
                                            log?.Invoke($"[MERGE-SKIP] Bỏ qua cặp đã thất bại: {name} lvl{level} {second.Index}->{first.Index}");
                                            continue;
                                        }
                                        // Thực hiện swipe từ second vào first
                                        var p1 = new System.Drawing.Point(
                                            first.CellRect.X + first.CellRect.Width / 2,
                                            first.CellRect.Y + first.CellRect.Height / 2);
                                        var p2 = new System.Drawing.Point(
                                            second.CellRect.X + second.CellRect.Width / 2,
                                            second.CellRect.Y + second.CellRect.Height / 2);
                                        bool mergeSuccess = false;
                                        int mergeTry = 0;
                                        for (; mergeTry < 2; mergeTry++) {
                                            adb.RunShellPersistent($"input swipe {p2.X} {p2.Y} {p1.X} {p1.Y} 100");
                                            await Task.Delay(200); // Đợi thao tác merge
                                                                   // Kiểm tra lại ô nguồn (second.Index) có trống không
                                            using (var checkScreenshot = capture.Capture(deviceId) as Bitmap)
                                            using (var bmpSource = checkScreenshot.Clone(second.CellRect, checkScreenshot.PixelFormat)) {
                                                if (IsEmptyCell(bmpSource, log)) {
                                                    mergeSuccess = true;
                                                    break;
                                                }
                                            }
                                            log?.Invoke($"[MERGE] Ô nguồn {second.Index} chưa trống, thử lại lần {mergeTry + 1}");
                                            await Task.Delay(200);
                                        }
                                        if (!mergeSuccess) {
                                            log?.Invoke($"[MERGE-FAIL] Merge thất bại tại cell {second.Index}->{first.Index}, bỏ qua cặp này!");
                                            failedMergePairs.Add(mergeKey);
                                            continue;
                                        }
                                        log?.Invoke($"Merged {name} lvl{level}: cell {second.Index}->{first.Index}");
                                        // Cập nhật lại danh sách hero trong bộ nhớ
                                        results.RemoveAll(c => c.Index == first.Index || c.Index == second.Index);
                                        // Thêm hero mới vào vị trí first với level+1
                                        results.Add(new CellResult {
                                            Index = first.Index,
                                            HeroName = name,
                                            Level = (level + 1).ToString(),
                                            CellRect = first.CellRect
                                        });
                                        // Sau mỗi lần merge, thử click coin để roll thêm hero nếu còn slot trống
                                        if (results.Count < gridCols * rows) {
                                            if (clickCoin != null)
                                                await clickCoin("Coin", 1, log);
                                            await Task.Delay(200); // Đợi hero mới xuất hiện
                                        }
                                        didMerge = true;
                                        mergedAny = true;
                                        // Sau khi merge thành công, break để cập nhật lại danh sách hero và thử lại từ đầu
                                        break;
                                    }
                                    if (mergedAny) break;
                                }
                                if (mergedAny) break;
                            }
                            if (mergedAny) break;
                        }
                        if (mergedAny) break;
                    }
                } while (mergedAny);
                return didMerge;
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.TryMergeAsync] Lỗi: {ex.Message}");
                throw;
            }
        }



    }
}
