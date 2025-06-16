using System.Collections.Generic;
using System.Drawing;
using System.IO;
using LUDUS.Utils;

namespace LUDUS.Services {
    public class ScreenRecognitionService {
        private readonly List<RegionInfo> _regions;
        private readonly string _refFolder;

        public ScreenRecognitionService(string regionsXmlPath, string referenceFolder) {
            _regions = RegionLoader.LoadPresetRegions(regionsXmlPath);
            _refFolder = referenceFolder;
        }

        public List<string> Recognize(Bitmap screenshot, double threshold = 0.9) {
            var found = new List<string>();
            foreach (var reg in _regions) {
                var r = Rectangle.Intersect(reg.Rect,
                    new Rectangle(0, 0, screenshot.Width, screenshot.Height));
                if (r.Width <= 0 || r.Height <= 0) continue;

                using (var crop = screenshot.Clone(r, screenshot.PixelFormat)) {
                    string tplPath = Path.Combine(_refFolder, reg.Name + ".png");
                    if (!File.Exists(tplPath)) continue;
                    using (var tpl = new Bitmap(tplPath))
                        if (ImageCompare.AreSame(crop, tpl, threshold))
                            found.Add(reg.Name);
                }
            }
            return found;
        }
    }
}
