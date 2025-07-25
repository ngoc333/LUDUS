using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LUDUS.Utils;
using System.IO;

namespace LUDUS.Services {
    // Model for each cell analysis result
    public class CellResult {
        public int Index { get; set; }
        public string HeroName { get; set; }
        public string Level { get; set; }
        public Rectangle CellRect { get; set; }
    }

    // Service to analyze battle cells
    public class BattleService {
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

        // Lưu trữ trạng thái bàn cờ suốt trận
        private readonly List<CellResult> _boardState = new List<CellResult>();

        // Reset trạng thái bàn cờ khi bắt đầu trận mới hoặc kết thúc trận
        public void ResetBoardState() {
            _boardState.Clear();
            _failedMergePairs.Clear();
        }

        public BattleService(
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
            if (round == 2 ) await Task.Delay(3000); // Wait for spell animation
        }

        public async Task ClickCoin(string deviceId, int count, Action<string> log) {
            log?.Invoke($"Coin x{count}");
            for (int i = 0; i < count; i++) {
                await ClickRegion("Coin", deviceId, log, false);
                await Task.Delay(100);
            }
        }

        public void CaptureAfterCoinClick(string deviceId, int round, Action<string> log)
        {
            string screenshotDir = Path.Combine(System.Windows.Forms.Application.StartupPath, "Screenshoot");
            if (!Directory.Exists(screenshotDir)) Directory.CreateDirectory(screenshotDir);
            try {
                var img = _capture.Capture(deviceId);
                if (img != null) {
                    string fileName = $"screen_round{round}_{DateTime.Now:yyyyMMddHHmmss}.png";
                    string outFile = Path.Combine(screenshotDir, fileName);
                    img.Save(outFile);
                    log?.Invoke($"Đã chụp màn hình round {round} sau khi click coin: {outFile}");
                }
            } catch (Exception ex) {
                log?.Invoke($"Lỗi khi chụp màn hình: {ex.Message}");
            }
        }

        public async Task<bool> IsInBattleScreen(string deviceId, Action<string> log, CancellationToken ct) {
            string screen = await _screenDetectSvc.DetectScreenAsync(deviceId, log, ct);
            // In the new logic, "ToBattle" is named "Battle"
            return screen == "Battle" || screen == "ToBattle";
        }
        
        public async Task<bool> AnalyzeAndMerge(string deviceId, Action<string> log, int currentRound) {
            var (merged, _) = await AnalyzeAndMergeWithCount(deviceId, log, null, currentRound);
            if (merged) log?.Invoke("Merge ✓");
            return merged;
        }

        public void ResetFailedMergePairs() {
            _failedMergePairs.Clear();
        }

        public async Task ClickEndRound(string deviceId, Action<string> log) {
            // The button to end the round is named "Battle" in regions.xml
            await ClickRegion("ToBattle", deviceId, log);
            await ClickRegion("ToBattleOk", deviceId, log);
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

        private async Task<(bool merged, int heroCount)> AnalyzeAndMergeWithCount(string deviceId, Action<string> log, List<int> specificCells = null, int currentRound = 1) {
            const int rows = 4;
            // Sử dụng các hàm tiện ích từ BattleAnalyzerUtils
            var cellRects = BattleAnalyzerUtils.GenerateCellRects(_regions, rows, GridCols, log);
            var (rectCheck, rectName, rectLv) = BattleAnalyzerUtils.GetHeroInfoRects(_regions, log);

            // 1. Xác định cells cần scan
            List<int> cellsToScan;
            if (currentRound == 1 || _boardState.Count == 0) {
                // Trận mới hoặc chưa có dữ liệu - quét toàn bộ 20 ô
                cellsToScan = specificCells ?? Enumerable.Range(0, cellRects.Count).ToList();
                log?.Invoke($"[ROUND-{currentRound}] Scan toàn bộ {cellsToScan.Count} cells");
            } else {
                // Round tiếp theo - chỉ quét ô trống, stone, fail
                cellsToScan = GetCellsToRescan(_boardState, cellRects.Count, log);
                if (cellsToScan.Count == 0) {
                    log?.Invoke($"[ROUND-{currentRound}] Không có ô nào cần rescan, dùng dữ liệu cũ");
                } else {
                    log?.Invoke($"[ROUND-{currentRound}] Rescan {cellsToScan.Count} cells: [{string.Join(", ", cellsToScan)}]");
                }
            }

            // 2. Scan và cập nhật board state
            var scanResults = await BattleAnalyzerUtils.ScanBoardAsync(
                _capture, _adb, _ocr, _templateBasePath, _regions, deviceId, cellRects, rectCheck, rectName, rectLv, log, cellsToScan);

            // Cập nhật _boardState với kết quả scan mới
            foreach (var cell in scanResults) {
                _boardState.RemoveAll(c => c.Index == cell.Index);
                _boardState.Add(cell);
            }

            // Reset failedMergePairs khi bắt đầu round mới (sau khi scan board)
            if (currentRound == 1) {
                log?.Invoke("[RESET-FAILED-PAIRS] Reset failedMergePairs khi bắt đầu round mới");
                ResetFailedMergePairs();
            }

            // Kiểm tra có đủ cells để merge không
            if (_boardState.Count < 2) {
                return (false, _boardState.Count);
            }

            // 3. Merge liên tục trong bộ nhớ, ưu tiên vị trí trung tâm
            bool anyMergeHappened = false;
            bool didMerge;
            do {
                didMerge = await BattleMergeUtils.TryMergeAsync(
                    _capture, _adb, _templateBasePath, deviceId, _failedMergePairs, GridCols, _boardState, rows, log,
                    (name, count, logger) => ClickCoin(deviceId, count, logger), 
                    currentRound == 1); // Enable debug log cho round đầu tiên
                if (didMerge)
                    anyMergeHappened = true;
            } while (didMerge);

            return (anyMergeHappened, _boardState.Count);
        }
        
        private List<int> GetCellsToRescan(List<CellResult> currentResults, int totalCells, Action<string> log) {
            var cellsToRescan = new List<int>();
            
            // Tạo danh sách tất cả cells (0-19)
            var allCells = Enumerable.Range(0, totalCells).ToList();
            
            // Lấy danh sách cells hiện tại có dữ liệu
            var currentCellIndices = currentResults.Select(c => c.Index).ToList();
            
            // Tìm những ô trống (không có trong currentResults)
            var emptyCells = allCells.Except(currentCellIndices).ToList();
            
            // Tìm những ô có Stone hoặc merge fail
            var stoneAndFailedCells = currentResults
                .Where(c => c.HeroName == "Stone" || c.Level == "-1" || 
                           _failedMergePairs.Any(fp => fp.Contains(c.Index.ToString())))
                .Select(c => c.Index)
                .ToList();
            
            // Kết hợp danh sách
            cellsToRescan.AddRange(emptyCells);
            cellsToRescan.AddRange(stoneAndFailedCells);
            cellsToRescan = cellsToRescan.Distinct().OrderBy(x => x).ToList();
            
            log?.Invoke($"[GetCellsToRescan] Empty cells: [{string.Join(", ", emptyCells)}]");
            log?.Invoke($"[GetCellsToRescan] Stone/Failed cells: [{string.Join(", ", stoneAndFailedCells)}]");
            log?.Invoke($"[GetCellsToRescan] Total cells to rescan: {cellsToRescan.Count}");
            
            return cellsToRescan;
        }

    }
}
