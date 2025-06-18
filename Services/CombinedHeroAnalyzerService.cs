// Services/CombinedHeroAnalyzerService.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using LUDUS.Utils;

namespace LUDUS.Services {
    public class CombinedHeroAnalyzerService : IDisposable {
        private readonly AdbService _adb;
        private readonly ScreenCaptureService _cap;
        private readonly List<RegionInfo> _cells;
        private readonly RegionInfo _nameRegion;
        private readonly string _outputFolder;
        private readonly int _tapDelayMs;

        public CombinedHeroAnalyzerService(
            AdbService adb,
            ScreenCaptureService cap,
            string regionsXmlPath,
            string outputFolder,
            int tapDelayMs = 600) {
            _adb = adb;
            _cap = cap;
            _tapDelayMs = tapDelayMs;
            _outputFolder = outputFolder;

            // 1) Sinh grid 4x5 từ duy nhất một region C1
            var baseCell = RegionLoader
                .LoadPresetRegions(regionsXmlPath)
                .FirstOrDefault(r => r.Group == "MySideCell");
            if (baseCell == null)
                throw new Exception("Không tìm thấy region MySideCell");

            int w = baseCell.Rect.Width, h = baseCell.Rect.Height;
            int sx = baseCell.Rect.X, sy = baseCell.Rect.Y;
            _cells = Enumerable
                .Range(0, 4).SelectMany(row =>
                    Enumerable.Range(0, 5).Select(col => new RegionInfo {
                        Group = "MySideCell",
                        Name = $"{row}_{col}",
                        Rect = new Rectangle(
                            sx + col * w,
                            sy + row * h,
                            w, h)
                    })
                ).ToList();

            // 2) HeroInfo/Name region
            _nameRegion = RegionLoader
                .LoadPresetRegions(regionsXmlPath)
                .FirstOrDefault(r => r.Group == "HeroInfo" && r.Name == "Name");
            if (_nameRegion == null)
                throw new Exception("Không tìm region HeroInfo/Name");

            if (!Directory.Exists(_outputFolder))
                Directory.CreateDirectory(_outputFolder);
        }

        /// <summary>
        /// 1) Capture once để detect presence  
        /// 2) Tap & fetch Name chỉ cho cell có hero  
        /// </summary>
        public void AnalyzeAll(string deviceId, Action<string> log) {
            // 1) Capture full để detect presence
            using (var full = Capture(deviceId)) {
                log("Captured full screen for presence check.");
                var heroes = new List<RegionInfo>();

                foreach (var cell in _cells) {
                    using (var bmp = Crop(full, cell.Rect)) {
                        if (IsEmptyCell(bmp))
                            log($"{cell.Name}: Empty");
                        else {
                            log($"{cell.Name}: HasHero");
                            heroes.Add(cell);
                        }
                    }
                }

                // 2) Với mỗi cell có hero, tap → capture → crop Name
                foreach (var cell in heroes)
                    FetchHeroName(deviceId, cell, log);
            }

            log("✅ AnalyzeAll complete.");
        }

        private Bitmap Capture(string deviceId) {
            // chụp màn qua ScreenCaptureService
            return (Bitmap)_cap.Capture(deviceId);
        }

        private Bitmap Crop(Bitmap src, Rectangle r) {
            // crop Bitmap
            var dst = new Bitmap(r.Width, r.Height);
            using (var g = Graphics.FromImage(dst)) {
                g.DrawImage(src,
                            new Rectangle(0, 0, dst.Width, dst.Height),
                            r,
                            GraphicsUnit.Pixel);
            }
            return dst;
        }

        private void FetchHeroName(string deviceId, RegionInfo cell, Action<string> log) {
            // tap vào giữa cell
            int tx = cell.Rect.X + cell.Rect.Width / 2;
            int ty = cell.Rect.Y + cell.Rect.Height / 2;
            _adb.Run($"-s {deviceId} shell input tap {tx} {ty}");
            log($"Tapped {cell.Name} at ({tx},{ty})");
            Thread.Sleep(_tapDelayMs);

            // capture màn mới để lấy info
            using (var full = Capture(deviceId))
            using (var nameBmp = Crop(full, _nameRegion.Rect)) {
                string fname = $"{cell.Name}_name.png";
                string fpath = Path.Combine(_outputFolder, fname);
                nameBmp.Save(fpath);
                log($"Saved name for {cell.Name}");
            }
        }

        /// <summary>
        /// Lấy patch 20×20 ở trung tâm, tính grayscale range.
        /// Nếu range &lt;20 => xem như trống.
        /// </summary>
        private bool IsEmptyCell(Bitmap bmp) {
            
            const int patch = 20;
            const int thresh = 20;
            int w = bmp.Width, h = bmp.Height;
            int cx = w / 2 - patch / 2, cy = h / 2 - patch / 2;
            if (cx < 0) cx = 0;
            if (cy < 0) cy = 0;

            int minB = 255, maxB = 0;
            for (int y = cy; y < cy + patch && y < h; y++) {
                for (int x = cx; x < cx + patch && x < w; x++) {
                    Color c = bmp.GetPixel(x, y);
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    if (gray < minB) minB = gray;
                    if (gray > maxB) maxB = gray;
                    if (maxB - minB >= thresh)
                        return false;  // có chi tiết → không trống
                }
            }

            return true; // toàn pixel gần giống nhau → trống
        }

        public void Dispose() { /* nothing to dispose */ }
    }
}
