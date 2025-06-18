using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using LUDUS.Utils;

namespace LUDUS.Services {
    public class BattleAnalyzerService : IDisposable {
        private readonly TemplateMatcherService _matcher;
        private readonly List<RegionInfo> _cells;
        private readonly string _outputFolder;

        public BattleAnalyzerService(
            string regionsXmlPath,
            string templatesFolder,
            string outputFolder,
            double matchThreshold = 0.8) {
            // Load định nghĩa ô của MySideCell
            _cells = RegionLoader
                .LoadPresetRegions(regionsXmlPath)
                .Where(r => r.Group == "MySideCell")
                .ToList();

            // Khởi template matcher
            _matcher = new TemplateMatcherService(templatesFolder, matchThreshold);

            // Folder gốc để lưu kết quả
            _outputFolder = outputFolder;
            if (!Directory.Exists(_outputFolder))
                Directory.CreateDirectory(_outputFolder);
        }

        /// <summary>
        /// Crop từng ô, lưu cell image, kiểm tra empty và match template.
        /// </summary>
        public Dictionary<string, List<string>> AnalyzeBattleScreen(Bitmap screenshot) {
            var results = new Dictionary<string, List<string>>();

            // Thư mục lưu raw cell images
            string rawDir = Path.Combine(_outputFolder, "RawCells");
            if (!Directory.Exists(rawDir))
                Directory.CreateDirectory(rawDir);

            // Xác định kích thước ô (dựa trên C1, C2)
            var c1 = _cells.First(r => r.Name == "C1").Rect;
            var c2 = _cells.First(r => r.Name == "C2").Rect;
            int cellW = c2.X - c1.X, cellH = c1.Height;
            int startX = c1.X, startY = c1.Y;

            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 5; col++) {
                    string key = $"cell_{row}_{col}";
                    var rect = new Rectangle(
                        startX + col * cellW,
                        startY + row * cellH,
                        cellW, cellH
                    );

                    // 1) Crop
                    using (var cellBmp = new Bitmap(rect.Width, rect.Height))
                    using (var g = Graphics.FromImage(cellBmp)) {
                        g.DrawImage(screenshot,
                                    new Rectangle(0, 0, rect.Width, rect.Height),
                                    rect,
                                    GraphicsUnit.Pixel);

                        // 2) Lưu raw image
                        string outImg = Path.Combine(rawDir, $"{key}.png");
                        cellBmp.Save(outImg);

                        // 3) Kiểm tra empty
                        if (IsEmptyCell(cellBmp)) {
                            results[key] = new List<string> { "Empty" };
                            continue;
                        }

                        // 4) Match template
                        var matches = _matcher.Match(cellBmp);
                        results[key] = matches.Count > 0
                            ? matches
                            : new List<string>();  // no matches
                    }
                }

            return results;
        }

        /// <summary>
        /// Kiểm tra empty: nếu >90% pixel giống nhau.
        /// </summary>
        private bool IsEmptyCell(Bitmap bmp) {
            const int step = 5;
            const int thresh = 15;  // ngưỡng brightness

            int minB = 255, maxB = 0;
            for (int y = 0; y < bmp.Height; y += step) {
                for (int x = 0; x < bmp.Width; x += step) {
                    Color c = bmp.GetPixel(x, y);
                    // luminance BT.601
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    if (gray < minB) minB = gray;
                    if (gray > maxB) maxB = gray;
                    // sớm thoát nếu đã quá ngưỡng
                    if (maxB - minB >= thresh)
                        return false;
                }
            }
            // nếu toàn màn gray gần nhau → empty
            return (maxB - minB) < thresh;
        }

        public void Dispose() {
            _matcher.Dispose();
        }
    }
}
