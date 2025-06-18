// Services/BattleAnalyzerService.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using LUDUS.Utils;

namespace LUDUS.Services {
    public class BattleAnalyzerService : IDisposable {
        private readonly AdbService _adb;
        private readonly ScreenCaptureService _cap;
        private readonly HeroNameOcrService _ocr;
        private readonly List<RegionInfo> _cells;
        private readonly RegionInfo _nameRegion;
        private readonly int _tapDelayMs;

        public BattleAnalyzerService(
            AdbService adb,
            ScreenCaptureService cap,
            HeroNameOcrService ocr,
            string regionsXmlPath,
            int tapDelayMs = 600) {
            _adb = adb;
            _cap = cap;
            _ocr = ocr;
            _tapDelayMs = tapDelayMs;

            // Sinh grid 4×5 từ 1 region C1
            var baseCell = RegionLoader
                .LoadPresetRegions(regionsXmlPath)
                .FirstOrDefault(r => r.Group == "MySideCell");
            if (baseCell == null)
                throw new Exception("Không tìm region MySideCell");

            int w = baseCell.Rect.Width, h = baseCell.Rect.Height;
            int sx = baseCell.Rect.X, sy = baseCell.Rect.Y;
            _cells = Enumerable
                .Range(0, 4).SelectMany(row =>
                  Enumerable.Range(0, 5).Select(col =>
                    new RegionInfo {
                        Group = "MySideCell",
                        Name = $"{row}_{col}",
                        Rect = new Rectangle(
                        sx + col * w,
                        sy + row * h,
                        w, h)
                    }))
                .ToList();

            // Load vùng Name
            _nameRegion = RegionLoader
                .LoadPresetRegions(regionsXmlPath)
                .FirstOrDefault(r => r.Group == "HeroInfo" && r.Name == "Name");
            if (_nameRegion == null)
                throw new Exception("Không tìm region HeroInfo/Name");
        }

        /// <summary>
        /// Với mỗi ô:
        /// - Nếu empty → log "cell: Empty"
        /// - Nếu có hero → tap → crop name → OCR → nếu OCR empty thì "Empty", ngược lại log tên
        /// </summary>
        public void AnalyzeBattle(string deviceId, Action<string> log) {
            foreach (var cell in _cells) {
                // 1) Capture và kiểm ô trống
                using (var full0 = (Bitmap)_cap.Capture(deviceId))
                using (var patch = Crop(full0, cell.Rect)) {
                    if (IsEmptyCell(patch)) {
                        log($"{cell.Name}: Empty");
                        continue;
                    }
                }

                // 2) Nếu có hero, tap và chờ hiển thị info
                int tx = cell.Rect.X + cell.Rect.Width / 2;
                int ty = cell.Rect.Y + cell.Rect.Height / 2;
                _adb.Run($"-s {deviceId} shell input tap {tx} {ty}");
                Thread.Sleep(_tapDelayMs);

                // 3) Chụp màn mới, crop vùng Name, OCR ngay
                string heroName;
                using (var full1 = (Bitmap)_cap.Capture(deviceId))
                using (var nameBmp = Crop(full1, _nameRegion.Rect)) {
                    heroName = _ocr.Recognize(nameBmp)?.Trim();
                }

                // 4) Log: nếu OCR không đọc ra thì coi là Empty
                if (string.IsNullOrWhiteSpace(heroName))
                    log($"{cell.Name}: Empty");
                else
                    log($"{cell.Name}: {heroName}");
            }

            log("✅ AnalyzeBattle complete.");
        }

        private Bitmap Crop(Bitmap src, Rectangle r) {
            var dst = new Bitmap(r.Width, r.Height);
            using (var g = Graphics.FromImage(dst))
                g.DrawImage(src,
                    new Rectangle(0, 0, dst.Width, dst.Height),
                    r, GraphicsUnit.Pixel);
            return dst;
        }

        private bool IsEmptyCell(Bitmap bmp) {
            const int PATCH = 20, THRESH = 20;
            int w = bmp.Width, h = bmp.Height;
            int cx = Math.Max(0, w / 2 - PATCH / 2);
            int cy = Math.Max(0, h / 2 - PATCH / 2);

            int minB = 255, maxB = 0;
            for (int y = cy; y < cy + PATCH && y < h; y++)
                for (int x = cx; x < cx + PATCH && x < w; x++) {
                    Color c = bmp.GetPixel(x, y);
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    if (gray < minB) minB = gray;
                    if (gray > maxB) maxB = gray;
                    if (maxB - minB >= THRESH)
                        return false;  // có chi tiết
                }
            return true; // gần như đồng màu
        }

        public void Dispose() => _ocr?.Dispose();
    }
}
