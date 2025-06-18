using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Services {
    public class TemplateMatcherService : IDisposable {
        private readonly List<(string Name, Mat Template)> _templates;
        private readonly double _threshold;

        public TemplateMatcherService(string templatesFolder, double threshold = 0.8) {
            _threshold = threshold;
            _templates = new List<(string, Mat)>();

            if (!Directory.Exists(templatesFolder))
                throw new DirectoryNotFoundException($"Templates folder not found: {templatesFolder}");

            foreach (var file in Directory.GetFiles(templatesFolder, "*.png")) {
                string name = Path.GetFileNameWithoutExtension(file);
                var tpl = Cv2.ImRead(file, ImreadModes.Color);
                _templates.Add((name, tpl));
            }
        }

        public List<string> Match(Bitmap bmp) {
            var found = new List<string>();

            // Chuyển screenshot → Mat BGRA rồi → Gray
            using (var srcBGRA = BitmapConverter.ToMat(bmp))
            using (var src = new Mat()) {
                Cv2.CvtColor(srcBGRA, src, ColorConversionCodes.BGRA2GRAY);

                foreach (var (name, tpl) in _templates) {
                    // Chuẩn bị template gray (nếu chưa có sẵn, có thể cache)
                    using (var tplGray = new Mat()) {
                        Cv2.CvtColor(tpl, tplGray, ColorConversionCodes.BGR2GRAY);

                        if (src.Width < tplGray.Width || src.Height < tplGray.Height)
                            continue;

                        using (var result = new Mat()) {
                            Cv2.MatchTemplate(src, tplGray, result, TemplateMatchModes.CCoeffNormed);
                            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
                            if (maxVal >= _threshold)
                                found.Add(name);
                        }
                    }
                }
            }

            return found;
        }


        public void Dispose() {
            foreach (var (_, tpl) in _templates)
                tpl.Dispose();
        }
    }
}
