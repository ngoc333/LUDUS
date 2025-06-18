using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Services {
    public class CellComparisonService : IDisposable {
        private ORB _orb;
        private BFMatcher _matcher;
        private double _similarityThreshold;
        private int _distanceThreshold;

        public event Action<string> OnLog;

        public CellComparisonService(
            double similarityThreshold = 0.25,
            int distanceThreshold = 30,
            int maxFeatures = 500) {
            _similarityThreshold = similarityThreshold;
            _distanceThreshold = distanceThreshold;
            _orb = ORB.Create(maxFeatures);
            _matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        }

        public List<Tuple<string, string, double>> Compare(
            Dictionary<string, Bitmap> cellBitmaps) {
            // 1) Folder lưu ảnh đã crop
            string processedDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ProcessedCells");
            if (!Directory.Exists(processedDir))
                Directory.CreateDirectory(processedDir);

            OnLog?.Invoke("=== START CROPPING CELLS ===");

            // 2) Crop + save + descriptor
            var descMap = new Dictionary<string, Mat>();
            var kpCount = new Dictionary<string, int>();
            foreach (var kv in cellBitmaps) {
                string key = kv.Key;
                Bitmap bmp = kv.Value;

                // center-crop height=80
                int cropH = Math.Min(80, bmp.Height);
                int y = (bmp.Height - cropH) / 2;
                var rect = new Rectangle(0, y, bmp.Width, cropH);
                Bitmap procBmp = bmp.Clone(rect, bmp.PixelFormat);

                string outPath = Path.Combine(processedDir, key + "_cropped.png");
                procBmp.Save(outPath);
                OnLog?.Invoke($"CROPPED [{key}] → {outPath}");

                // to gray mat + detect
                Mat matColor = BitmapConverter.ToMat(procBmp);
                Mat matGray = new Mat();
                Cv2.CvtColor(matColor, matGray, ColorConversionCodes.BGR2GRAY);
                matColor.Dispose();

                KeyPoint[] kp;
                Mat desc = new Mat();
                _orb.DetectAndCompute(matGray, null, out kp, desc);
                kpCount[key] = kp.Length;
                matGray.Dispose();

                descMap[key] = desc;
                procBmp.Dispose();
            }

            OnLog?.Invoke("=== START PAIRWISE MATCHING ===");

            // 3) Pairwise compare & log all
            var results = new List<Tuple<string, string, double>>();
            var keys = descMap.Keys.ToList();
            for (int i = 0; i < keys.Count; i++) {
                for (int j = i + 1; j < keys.Count; j++) {
                    string k1 = keys[i], k2 = keys[j];
                    Mat d1 = descMap[k1], d2 = descMap[k2];

                    if (d1.Empty() || d2.Empty()) {
                        OnLog?.Invoke($"COMPARE [{k1}] vs [{k2}] → no descriptors");
                        continue;
                    }

                    var matches = _matcher.Match(d1, d2);
                    int good = matches.Count(m => m.Distance < _distanceThreshold);
                    int minKp = Math.Min(kpCount[k1], kpCount[k2]);
                    double sim = minKp > 0 ? (double)good / minKp : 0.0;

                    OnLog?.Invoke(
                        $"COMPARE [{k1}] vs [{k2}] → sim={sim:F2} "
                        + (sim >= _similarityThreshold ? "→ MATCH" : "→ NO MATCH")
                    );

                    if (sim >= _similarityThreshold)
                        results.Add(Tuple.Create(k1, k2, sim));
                }
            }

            // 4) Cleanup
            foreach (var desc in descMap.Values)
                desc.Dispose();

            OnLog?.Invoke("=== COMPARISON COMPLETE ===");
            return results;
        }

        public void Dispose() {
            _matcher?.Dispose();
            _orb?.Dispose();
        }
    }
}
