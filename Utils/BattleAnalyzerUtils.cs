using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using LUDUS.Services;
using LUDUS.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Utils {
    public static class BattleAnalyzerUtils {
        // Cache template theo folderPath để tránh load lại nhiều lần
        private static readonly ConcurrentDictionary<string, List<(string Name, Mat Template)>> _templateCache = new ConcurrentDictionary<string, List<(string Name, Mat Template)>>();

        private static List<(string Name, Mat Template)> GetTemplates(string folderPath, Action<string> log) {
            // Trả về danh sách template đã preload; nếu chưa có thì load và cache
            return _templateCache.GetOrAdd(folderPath, fp => {
                var list = new List<(string Name, Mat Template)>();
                if (!Directory.Exists(fp)) return list;

                foreach (var file in Directory.GetFiles(fp, "*.png", SearchOption.AllDirectories)
                                           .OrderBy(f => Path.GetFileNameWithoutExtension(f))) {
                    try {
                        var tpl = Cv2.ImRead(file, ImreadModes.Color);
                        if (tpl.Empty()) {
                            log?.Invoke($"[TemplateCache] Không thể load template: {file}");
                            continue;
                        }
                        string name = Path.GetFileNameWithoutExtension(file);
                        list.Add((name, tpl));
                    } catch (Exception ex) {
                        log?.Invoke($"[TemplateCache] Lỗi khi load template {file}: {ex.Message}");
                    }
                }
               // log?.Invoke($"[TemplateCache] Loaded {list.Count} templates from {fp}");
                return list;
            });
        }

        /// <summary>
        /// Kiểm tra một vùng nhỏ trong bitmap có phải là stone (dark gray) không.
        /// </summary>
        public static bool IsStone(Bitmap bitmap) {
            Mat mat = null;
            Mat roi = null;
            Mat gray = null;
            Mat mask = null;

            try {
                if (bitmap == null) {
                    return false;
                }

                // Kiểm tra kích thước bitmap
                if (bitmap.Width <= 0 || bitmap.Height <= 0) {
                    return false;
                }

                // Convert Bitmap to Mat (BGR)
                mat = BitmapConverter.ToMat(bitmap);
                Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2BGR);

                // Crop region at (114, 80) with size (32, 26)
                var rect = new Rect(114, 80, 32, 26);
                if (rect.Right > mat.Width || rect.Bottom > mat.Height)
                    return false; // region out of bounds

                roi = new Mat(mat, rect);
                gray = new Mat();
                Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);

                // Tính tỷ lệ white pixels (>=230)
                int whiteCount = 0;
                int total = roi.Rows * roi.Cols;
                for (int y = 0; y < roi.Rows; y++) {
                    for (int x = 0; x < roi.Cols; x++) {
                        if (gray.At<byte>(y, x) >= 230)
                            whiteCount++;
                    }
                }
                double whiteRatio = (double)whiteCount / total;
                if (whiteRatio > 0.15)
                    return false; // Nếu vùng crop có quá nhiều trắng thì không phải stone

                // Create mask to exclude white pixels (brightness >= 230)
                mask = gray.LessThan(230);

                double sumR = 0, sumG = 0, sumB = 0;
                int count = 0;

                for (int y = 0; y < roi.Rows; y++) {
                    for (int x = 0; x < roi.Cols; x++) {
                        if (mask.At<byte>(y, x) == 0)
                            continue;

                        Vec3b color = roi.At<Vec3b>(y, x);
                        sumB += color.Item0;
                        sumG += color.Item1;
                        sumR += color.Item2;
                        count++;
                    }
                }

                if (count == 0)
                    return false;

                double avgR = sumR / count;
                double avgG = sumG / count;
                double avgB = sumB / count;
                double brightness = (avgR + avgG + avgB) / 3.0;

                // Check condition for "dark gray" region
                return brightness < 90 && avgB > avgG && avgG > avgR;
            } finally {
                if (mat != null) mat.Dispose();
                if (roi != null) roi.Dispose();
                if (gray != null) gray.Dispose();
                if (mask != null) mask.Dispose();
            }
        }

        public static bool IsEmptyCell(Bitmap bmp, Action<string> log, bool disposeBitmap = true) {
            try {
                if (bmp == null) {
                    log?.Invoke("[IsEmptyCell] Bitmap là null");
                    return true; // Coi như ô trống nếu không có bitmap
                }

                const int PATCH = 20, TH = 20;
                int w = bmp.Width, h = bmp.Height;

                // Kiểm tra kích thước bitmap
                if (w <= 0 || h <= 0) {
                    log?.Invoke($"[IsEmptyCell] Kích thước bitmap không hợp lệ: {w}x{h}");
                    if (disposeBitmap) bmp.Dispose();
                    return true;
                }

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
                            if (disposeBitmap) bmp.Dispose();
                            return false;
                        }
                    }
                if (disposeBitmap) bmp.Dispose();
                return true;
            } catch (Exception ex) {
                log?.Invoke($"[BattleAnalyzerUtils.IsEmptyCell] Lỗi: {ex.Message}");
                if (disposeBitmap) bmp?.Dispose();
                throw;
            }
        }

        public static string TryMatchTemplate(Bitmap crop, string folderPath, Action<string> log) {
            try {
                if (crop == null) {
                    log?.Invoke("[TryMatchTemplate] Bitmap crop là null");
                    return null;
                }

                // Lấy templates từ cache (hoặc load nếu chưa có)
                var templates = GetTemplates(folderPath, log);

                if (templates.Count == 0) return null;

                using (var mat0 = BitmapConverter.ToMat(crop))
                using (var mat = new Mat()) {
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                    foreach (var (templateName, tplMat) in templates) {
                        using (var result = new Mat()) {
                            Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                            if (maxVal >= 0.95) {
                                return templateName;
                            }
                        }
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
            string deviceId, List<Rectangle> cellRects, Rectangle rectCheck, Rectangle rectName, Rectangle rectLv, Action<string> log,
            List<int> specificCells = null) {
            try {
                List<CellResult> results = new List<CellResult>();
                using (Bitmap screenshot = capture.Capture(deviceId) as Bitmap) {
                    if (screenshot == null) {
                        log?.Invoke("[ScanBoardAsync] Không thể chụp màn hình");
                        return results;
                    }

                    // Xác định danh sách cells cần kiểm tra
                    var cellsToCheck = specificCells ?? Enumerable.Range(0, cellRects.Count).ToList();
                    
                    if (specificCells != null) {
                        log?.Invoke($"[ScanBoardAsync] Chỉ kiểm tra {specificCells.Count} cells: [{string.Join(", ", specificCells)}]");
                    } else {
                        log?.Invoke($"[ScanBoardAsync] Kiểm tra tất cả {cellRects.Count} cells");
                    }
                    
                    foreach (int i in cellsToCheck) {
                        if (i < 0 || i >= cellRects.Count) {
                            log?.Invoke($"[ScanBoardAsync] Bỏ qua index không hợp lệ: {i}");
                            continue;
                        }
                        var rect = cellRects[i];

                        // Kiểm tra rect có hợp lệ không
                        if (rect.X < 0 || rect.Y < 0 ||
                            rect.Right > screenshot.Width ||
                            rect.Bottom > screenshot.Height) {
                            log?.Invoke($"[ScanBoardAsync] Rect không hợp lệ tại index {i}: {rect}, Screenshot size: {screenshot.Width}x{screenshot.Height}");
                            continue;
                        }

                        using (var bmpCell = screenshot.Clone(rect, screenshot.PixelFormat)) {
                            if (IsEmptyCell(bmpCell, log, false))
                                continue;
                            // Không kiểm tra IsStone nữa
                            bmpCell.Dispose();
                        }
                        // Click để lấy thông tin hero
                        var px = rect.X + rect.Width / 2;
                        var py = rect.Y + rect.Height / 2 - 10;
                        adb.RunShellPersistent($"input tap {px} {py}");
                        await Task.Delay(100);
                        using (var newScreenshot = capture.Capture(deviceId) as Bitmap) {
                            if (newScreenshot == null) {
                                log?.Invoke($"[ScanBoardAsync] Không thể chụp màn hình sau khi click tại index {i}");
                                continue;
                            }

                            // Kiểm tra các rect có hợp lệ không
                            // if (rectCheck.X < 0 || rectCheck.Y < 0 ||
                            //     rectCheck.Right > newScreenshot.Width ||
                            //     rectCheck.Bottom > newScreenshot.Height) {
                            //     log?.Invoke($"[ScanBoardAsync] RectCheck không hợp lệ: {rectCheck}, Screenshot size: {newScreenshot.Width}x{newScreenshot.Height}");
                            //     continue;
                            // }

                            // using (var bmpCheck = newScreenshot.Clone(rectCheck, newScreenshot.PixelFormat))
                            // using (var tpl = new Bitmap(Path.Combine(templateBasePath, "Battle", "HeroCheck.png"))) {
                            //     if (!ImageCompare.AreSame(bmpCheck, tpl)) {
                            //       //  log?.Invoke($"[CHECK HERO] Index: {i}, Name: Stone, Level: -1");
                            //         results.Add(new CellResult { Index = i, HeroName = "Stone", Level = "-1", CellRect = rect });
                            //         continue;
                            //     }
                            // }
                            // string name;

                            // Kiểm tra rectLv có hợp lệ không
                            string lv;
                            if (rectLv.X < 0 || rectLv.Y < 0 ||
                                rectLv.Right > newScreenshot.Width ||
                                rectLv.Bottom > newScreenshot.Height) {
                                log?.Invoke($"[ScanBoardAsync] RectLv không hợp lệ: {rectLv}, Screenshot size: {newScreenshot.Width}x{newScreenshot.Height}");
                                lv = "";
                            }
                            else {
                                using (var bmpLv = newScreenshot.Clone(rectLv, newScreenshot.PixelFormat)) {
                                    var folderLv = Path.Combine(templateBasePath, "Battle", "LV");
                                    lv = TryMatchTemplate(bmpLv, folderLv, log);
                                }
                            }
                            if (string.IsNullOrEmpty(lv)) {
                                results.Add(new CellResult { Index = i, HeroName = "Stone", Level = "-1", CellRect = rect });
                                continue;
                            }

                            // Kiểm tra rectName có hợp lệ không
                            string name;
                            if (rectName.X < 0 || rectName.Y < 0 ||
                                rectName.Right > newScreenshot.Width ||
                                rectName.Bottom > newScreenshot.Height) {
                                log?.Invoke($"[ScanBoardAsync] RectName không hợp lệ: {rectName}, Screenshot size: {newScreenshot.Width}x{newScreenshot.Height}");
                                name = "";
                            }
                            else {
                                using (var bmpName = newScreenshot.Clone(rectName, newScreenshot.PixelFormat)) {
                                    var folder = Path.Combine(templateBasePath, "Battle", "HeroName");
                                    name = TryMatchTemplate(bmpName, folder, log);
                                    if (string.IsNullOrEmpty(name)) {
                                        name = ocr.Recognize(bmpName)?.Trim();
                                        if (!string.IsNullOrEmpty(name) && !File.Exists(Path.Combine(folder, name + ".png")))
                                            bmpName.Save(Path.Combine(folder, name + ".png"));
                                    }
                                }
                            }
                            

                            
                          //  log?.Invoke($"[CHECK HERO] Index: {i}, Name: {name}, Level: {lv}");


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
    }
}
