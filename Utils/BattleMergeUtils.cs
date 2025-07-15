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
            Func<string, int, Action<string>, Task> clickCoin, bool enableDebugLog = false) {
            try {
                log?.Invoke("[MERGE-START] Bắt đầu merge với trạng thái grid hiện tại:");
                LogGridMap(results, gridCols, rows, log);

                bool didMerge = false;
                bool mergedAny;
                do {
                    mergedAny = false;
                    var heroGroups = results
                        .Where(c => !string.IsNullOrEmpty(c.HeroName) && c.HeroName != "Stone" && int.TryParse(c.Level, out var lv) && lv < 4)
                        .GroupBy(c => c.HeroName);

                    foreach (var group in heroGroups) {
                        var name = group.Key;
                        var levelMap = group.GroupBy(c => int.Parse(c.Level))
                            .ToDictionary(g => g.Key, g => g.ToList());
                        foreach (var level in levelMap.Keys.OrderBy(l => l)) {
                            var list = levelMap[level];
                            if (list.Count >= 2) {
                                var (edgeIndices, centerIndices) = GetEdgeAndCenterIndices(gridCols, rows);
                                var edgeCells = list.Where(c => edgeIndices.Contains(c.Index)).ToList();
                                var centerCells = list.Where(c => centerIndices.Contains(c.Index)).ToList();
                                bool mergedThisLevel = await TryMergeEdgeToCenter(edgeCells, centerCells, name, level, results, failedMergePairs, gridCols, rows, log, capture, adb, deviceId, clickCoin);
                                if (mergedThisLevel) { didMerge = true; mergedAny = true; break; }
                                // Fallback merge nếu không còn cặp edge-center
                                bool fallbackMerged = await TryMergeFallback(list, name, level, results, failedMergePairs, gridCols, rows, log, capture, adb, deviceId, clickCoin);
                                if (fallbackMerged) { didMerge = true; mergedAny = true; break; }
                            }
                            if (mergedAny) break;
                        }
                        if (mergedAny) break;
                    }
                } while (mergedAny);
                return didMerge;
            } catch (Exception ex) {
                log?.Invoke($"[BattleMergeUtils.TryMergeAsync] Lỗi: {ex.Message}");
                throw;
            }
        }

        private static (List<int> edgeIndices, List<int> centerIndices) GetEdgeAndCenterIndices(int gridCols, int rows) {
            var edgeIndices = new List<int>();
            var centerIndices = new List<int>();
            for (int idx = 0; idx < gridCols * rows; idx++) {
                int row = idx / gridCols;
                int col = idx % gridCols;
                if (row == 0 || row == rows - 1 || col == 0 || col == gridCols - 1) {
                    edgeIndices.Add(idx);
                }
                else {
                    centerIndices.Add(idx);
                }
            }
            return (edgeIndices, centerIndices);
        }

        private static async Task<bool> TryMergeEdgeToCenter(List<CellResult> edgeCells, List<CellResult> centerCells, string name, int level, List<CellResult> results, HashSet<string> failedMergePairs, int gridCols, int rows, Action<string> log, ScreenCaptureService capture, AdbService adb, string deviceId, Func<string, int, Action<string>, Task> clickCoin) {
            foreach (var edge in edgeCells) {
                foreach (var center in centerCells) {
                    if (!IsValidMergePair(edge, center, name, level, results)) continue;
                    bool merged = await DoMerge(center, edge, name, level, results, failedMergePairs, gridCols, log, capture, adb, deviceId, clickCoin, rows);
                    if (merged) return true;
                }
            }
            return false;
        }

        private static async Task<bool> TryMergeFallback(List<CellResult> list, string name, int level, List<CellResult> results, HashSet<string> failedMergePairs, int gridCols, int rows, Action<string> log, ScreenCaptureService capture, AdbService adb, string deviceId, Func<string, int, Action<string>, Task> clickCoin) {
            for (int i = 0; i < list.Count; i++) {
                for (int j = i + 1; j < list.Count; j++) {
                    var first = list[i];
                    var second = list[j];
                    if (!IsValidMergePair(first, second, name, level, results)) continue;
                    bool merged = await DoMerge(first, second, name, level, results, failedMergePairs, gridCols, log, capture, adb, deviceId, clickCoin, rows);
                    if (merged) return true;
                }
            }
            return false;
        }

        private static bool IsValidMergePair(CellResult a, CellResult b, string name, int level, List<CellResult> results) {
            if (a.Level != b.Level) return false;
            if (!int.TryParse(a.Level, out var aLevel) || !int.TryParse(b.Level, out var bLevel)) return false;
            if (aLevel != bLevel) return false;
            if (a.HeroName != name || b.HeroName != name) return false;
            if (a.Index == b.Index) return false;
            if (!results.Any(c => c.Index == a.Index && c.HeroName == name && c.Level == level.ToString())) return false;
            if (!results.Any(c => c.Index == b.Index && c.HeroName == name && c.Level == level.ToString())) return false;
            return true;
        }

        private static async Task<bool> DoMerge(CellResult first, CellResult second, string name, int level, List<CellResult> results, HashSet<string> failedMergePairs, int gridCols, Action<string> log, ScreenCaptureService capture, AdbService adb, string deviceId, Func<string, int, Action<string>, Task> clickCoin, int rows) {
            string mergeKey = $"{first.Index}-{second.Index}-{name}-{level}";
            if (failedMergePairs.Contains(mergeKey)) return false;
            bool firstExists = results.Any(c => c.Index == first.Index && c.HeroName == name && c.Level == level.ToString());
            bool secondExists = results.Any(c => c.Index == second.Index && c.HeroName == name && c.Level == level.ToString());
            if (!firstExists || !secondExists) { failedMergePairs.Remove(mergeKey); return false; }
            var p1 = new System.Drawing.Point(first.CellRect.X + first.CellRect.Width / 2, first.CellRect.Y + first.CellRect.Height / 2);
            var p2 = new System.Drawing.Point(second.CellRect.X + second.CellRect.Width / 2, second.CellRect.Y + second.CellRect.Height / 2);
            bool mergeSuccess = false;
            int mergeTry = 0;
            for (; mergeTry < 3; mergeTry++) {
                adb.RunShellPersistent($"input swipe {p2.X} {p2.Y} {p1.X} {p1.Y} 100");
                await Task.Delay(300);
                using (var checkScreenshot = capture.Capture(deviceId) as Bitmap) {
                    if (checkScreenshot == null) { await Task.Delay(300); continue; }
                    if (second.CellRect.Right > checkScreenshot.Width || second.CellRect.Bottom > checkScreenshot.Height) { await Task.Delay(300); continue; }
                    using (var bmpSource = checkScreenshot.Clone(second.CellRect, checkScreenshot.PixelFormat)) {
                        if (BattleAnalyzerUtils.IsEmptyCell(bmpSource, log, true)) { mergeSuccess = true; break; }
                    }
                }
                await Task.Delay(300);
            }
            if (!mergeSuccess) { failedMergePairs.Add(mergeKey); return false; }
            failedMergePairs.Remove(mergeKey);
            var firstPos = GetCellPosition(first.Index, gridCols);
            var secondPos = GetCellPosition(second.Index, gridCols);
            log?.Invoke($"Merged {name} lvl{level}: cell {secondPos}->{firstPos}");
            results.RemoveAll(c => c.Index == first.Index || c.Index == second.Index);
            results.Add(new CellResult { Index = first.Index, HeroName = name, Level = (level + 1).ToString(), CellRect = first.CellRect });
            if (results.Count < gridCols * rows) { if (clickCoin != null) await clickCoin("Coin", 1, log); await Task.Delay(300); }
            return true;
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