using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LUDUS.Services;
using LUDUS.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Utils {
    public static class BattleMergeUtils {
        public static async Task<bool> TryMergeAsync(
            ScreenCaptureService capture, AdbService adb, string templateBasePath, string deviceId,
            HashSet<string> failedMergePairs, int gridCols, List<CellResult> results, int rows, Action<string> log,
            Func<string, int, Action<string>, Task> clickCoin) {
            // Moved logic from BattleAnalyzerUtils.TryMergeAsync (unchanged)
            try {
                // Hiển thị bản đồ grid trước khi merge
                log?.Invoke("[MERGE-START] Bắt đầu merge với trạng thái grid hiện tại:");
                LogGridMap(results, gridCols, rows, log);

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
                                var sorted = list
                                    .OrderBy(c => Math.Abs((c.Index % gridCols) - centerCol) + Math.Abs((c.Index / gridCols) - centerRow))
                                    .ThenBy(c => c.Index / gridCols).ToList();
                                for (int i = 0; i < sorted.Count; i++) {
                                    for (int j = i + 1; j < sorted.Count; j++) {
                                        var first = sorted[i];
                                        var second = sorted[j];
                                        string mergeKey = $"{first.Index}-{second.Index}-{name}-{level}";
                                        if (failedMergePairs.Contains(mergeKey)) {
                                            var firstPos2 = GetCellPosition(first.Index, gridCols);
                                            var secondPos2 = GetCellPosition(second.Index, gridCols);
                                            log?.Invoke($"[MERGE-SKIP] Bỏ qua cặp đã thất bại: {name} lvl{level} {secondPos2}->{firstPos2}");
                                            continue;
                                        }
                                        // Thực hiện swipe từ second vào first
                                        var p1 = new System.Drawing.Point(first.CellRect.X + first.CellRect.Width / 2, first.CellRect.Y + first.CellRect.Height / 2);
                                        var p2 = new System.Drawing.Point(second.CellRect.X + second.CellRect.Width / 2, second.CellRect.Y + second.CellRect.Height / 2);
                                        bool mergeSuccess = false;
                                        int mergeTry = 0;
                                        for (; mergeTry < 2; mergeTry++) {
                                            adb.RunShellPersistent($"input swipe {p2.X} {p2.Y} {p1.X} {p1.Y} 100");
                                            await Task.Delay(200);
                                            using (var checkScreenshot = capture.Capture(deviceId) as Bitmap) {
                                                if (checkScreenshot == null) {
                                                    await Task.Delay(200);
                                                    continue;
                                                }
                                                if (second.CellRect.Right > checkScreenshot.Width || second.CellRect.Bottom > checkScreenshot.Height) {
                                                    await Task.Delay(200);
                                                    continue;
                                                }
                                                using (var bmpSource = checkScreenshot.Clone(second.CellRect, checkScreenshot.PixelFormat)) {
                                                    if (BattleAnalyzerUtils.IsEmptyCell(bmpSource, log, true)) {
                                                        mergeSuccess = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            await Task.Delay(200);
                                        }
                                        if (!mergeSuccess) {
                                            failedMergePairs.Add(mergeKey);
                                            continue;
                                        }
                                        var firstPos = GetCellPosition(first.Index, gridCols);
                                        var secondPos = GetCellPosition(second.Index, gridCols);
                                        log?.Invoke($"Merged {name} lvl{level}: cell {secondPos}->{firstPos}");
                                        results.RemoveAll(c => c.Index == first.Index || c.Index == second.Index);
                                        results.Add(new CellResult { Index = first.Index, HeroName = name, Level = (level + 1).ToString(), CellRect = first.CellRect });
                                        if (results.Count < gridCols * rows) {
                                            if (clickCoin != null) await clickCoin("Coin", 1, log);
                                            await Task.Delay(200);
                                        }
                                        didMerge = true;
                                        mergedAny = true;

                                        // Hiển thị bản đồ sau khi merge thành công
                                       // log?.Invoke($"[MERGE-PROGRESS] Grid sau khi merge {name} lvl{level} (Cells count: {results.Count}):");
                                        //LogGridMap(results, gridCols, rows, log);
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

                // Log tất cả vị trí còn lại sau khi hết khả năng merge
                // if (results.Count > 0) {
                //     log?.Invoke($"[MERGE-COMPLETE] Còn lại {results.Count} vị trí:");
                //     foreach (var cell in results.OrderBy(c => c.Index)) {
                //         int row = cell.Index / gridCols;
                //         int col = cell.Index % gridCols;
                //         log?.Invoke($"[POSITION] Cell {row}_{col} - Hero: {cell.HeroName}, Level: {cell.Level}");
                //         log?.Invoke($"[COORDINATES] Cell {row}_{col}: X={cell.CellRect.X}, Y={cell.CellRect.Y}, W={cell.CellRect.Width}, H={cell.CellRect.Height}");
                //     }
                // } else {
                //     log?.Invoke("[MERGE-COMPLETE] Không còn vị trí nào");
                // }

                // Hiển thị bản đồ grid trực quan
                //LogGridMap(results, gridCols, rows, log);

                return didMerge;
            } catch (Exception ex) {
                log?.Invoke($"[BattleMergeUtils.TryMergeAsync] Lỗi: {ex.Message}");
                throw;
            }
        }

        private static void LogGridMap(List<CellResult> results, int gridCols, int rows, Action<string> log) {
            try {
                log?.Invoke($"[GRID-MAP] Bản đồ grid sau khi merge (Total cells: {results.Count}):");
                log?.Invoke("[GRID-MAP] " + new string('-', gridCols * 8 + 1));

                for (int row = 0; row < rows; row++) {
                    var rowLine = "[GRID-MAP] |";
                    for (int col = 0; col < gridCols; col++) {
                        int index = row * gridCols + col;
                        var cell = results.FirstOrDefault(c => c.Index == index);

                        if (cell != null) {
                            string heroDisplay = cell.HeroName.Length > 3 ? cell.HeroName.Substring(0, 3) : cell.HeroName.PadRight(3);
                            rowLine += $" {heroDisplay}{cell.Level} |";
                        }
                        else {
                            rowLine += "  EMPTY |";
                        }
                    }
                    log?.Invoke(rowLine);
                    log?.Invoke("[GRID-MAP] " + new string('-', gridCols * 8 + 1));
                }

                // // Debug: hiển thị tất cả cells có trong results
                // if (results.Count > 0) {
                //     log?.Invoke("[GRID-MAP-DEBUG] Danh sách tất cả cells:");
                //     foreach (var cell in results.OrderBy(c => c.Index)) {
                //         int row = cell.Index / gridCols;
                //         int col = cell.Index % gridCols;
                //         log?.Invoke($"[GRID-MAP-DEBUG] Index: {cell.Index}, Row: {row}, Col: {col}, Hero: {cell.HeroName}, Level: {cell.Level}");
                //     }
                // }
            } catch (Exception ex) {
                log?.Invoke($"[LogGridMap] Lỗi: {ex.Message}");
                log?.Invoke($"[LogGridMap] Stack trace: {ex.StackTrace}");
            }
        }

        private static string GetCellPosition(int index, int gridCols) {
            int row = index / gridCols;
            int col = index % gridCols;
            return $"{row + 1}_{col + 1}";
        }
    }
}