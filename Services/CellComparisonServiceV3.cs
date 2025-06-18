// Services/CellComparisonServiceV3.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Size = OpenCvSharp.Size;

namespace LUDUS.Services {
    public class CellComparisonServiceV3 : IDisposable {
        private readonly ORB _orb;
        private readonly BFMatcher _matcher;
        private readonly double _simThreshold;
        private readonly double _diffThreshold;
        private readonly int _distThreshold;

        public event Action<string> OnLog;

        public CellComparisonServiceV3(
            double simThreshold = 0.2,
            double diffThreshold = 0.7,
            int distThreshold = 30,
            int maxFeatures = 500) {
            _simThreshold = simThreshold;
            _diffThreshold = diffThreshold;
            _distThreshold = distThreshold;
            _orb = ORB.Create(maxFeatures);
            _matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        }

        public List<Tuple<string, string, double, double>> Compare(
            Dictionary<string, Bitmap> cells) {
            string outDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ProcessedCellsV3");
            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            OnLog?.Invoke("=== DYNAMIC CROP & PREPROCESS ===");

            var descMap = new Dictionary<string, Mat>();
            var grayMap = new Dictionary<string, Mat>();
            var kpCount = new Dictionary<string, int>();

            foreach (var kv in cells) {
                string key = kv.Key;
                Bitmap bmp = kv.Value;

                // 1) segment + crop ROI
                Rectangle roi;
                Bitmap roiBmp = DynamicCrop(bmp, out roi);

                // 2) mask overlay (vòng số) vùng đáy phải
                using (Graphics g = Graphics.FromImage(roiBmp)) {
                    int w = roiBmp.Width, h = roiBmp.Height;
                    var maskRect = new Rectangle(w - 60, h - 40, 60, 40);
                    g.FillRectangle(Brushes.Black, maskRect);
                }

                // 3) save ROI để kiểm tra
                string roiPath = Path.Combine(outDir, key + "_roi.png");
                roiBmp.Save(roiPath);
                OnLog?.Invoke("ROI SAVED & MASKED: " + key);

                // 4) convert to gray Mat
                Mat bgr = BitmapConverter.ToMat(roiBmp);
                Mat gray = new Mat();
                Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
                bgr.Dispose();

                // 5) ORB detect+compute
                KeyPoint[] kps;
                Mat desc = new Mat();
                _orb.DetectAndCompute(gray, null, out kps, desc);
                kpCount[key] = kps.Length;

                descMap[key] = desc;
                grayMap[key] = gray;
                roiBmp.Dispose();
            }

            OnLog?.Invoke("=== PAIRWISE COMPARE ===");

            var results = new List<Tuple<string, string, double, double>>();
            var keys = new List<string>(descMap.Keys);

            for (int i = 0; i < keys.Count; i++) {
                for (int j = i + 1; j < keys.Count; j++) {
                    string a = keys[i], b = keys[j];
                    Mat d1 = descMap[a], d2 = descMap[b];
                    Mat g1 = grayMap[a], g2 = grayMap[b];

                    // skip bad desc
                    if (d1.Empty() || d2.Empty() ||
                        d1.Type() != d2.Type() || d1.Cols != d2.Cols) {
                        OnLog?.Invoke($"SKIP [{a}] vs [{b}] (bad desc)");
                        continue;
                    }

                    // ORB
                    DMatch[] matches = _matcher.Match(d1, d2);
                    int good = 0;
                    foreach (var m in matches)
                        if (m.Distance < _distThreshold) good++;
                    int minK = Math.Min(kpCount[a], kpCount[b]);
                    double orbSim = (minK > 0) ? (double)good / minK : 0.0;

                    // pixel-diff
                    double diffSim = ComputeDiffSim(g1, g2);

                    bool ok = orbSim >= _simThreshold || diffSim >= _diffThreshold;
                    OnLog?.Invoke(
                        $"COMPARE [{a}] vs [{b}] → ORB={orbSim:F2}, DIFF={diffSim:F2} → "
                        + (ok ? "MATCH" : "NO MATCH"));

                    if (ok)
                        results.Add(Tuple.Create(a, b, orbSim, diffSim));
                }
            }

            // cleanup
            foreach (var m in descMap.Values) m.Dispose();
            foreach (var m in grayMap.Values) m.Dispose();

            OnLog?.Invoke("=== COMPLETE ===");
            return results;
        }

        private Bitmap DynamicCrop(Bitmap src, out Rectangle roi) {
            // 1) background color
            Color c = src.GetPixel(0, 0);
            Scalar bg = new Scalar(c.B, c.G, c.R);

            Mat mat = BitmapConverter.ToMat(src);
            Mat diff = new Mat();
            Cv2.Absdiff(mat, new Mat(mat.Size(), mat.Type(), bg), diff);

            Mat[] chs = Cv2.Split(diff);
            Mat maxCh = new Mat();
            Cv2.Max(chs[0], chs[1], maxCh);
            Cv2.Max(maxCh, chs[2], maxCh);

            Mat mask = new Mat();
            Cv2.Threshold(maxCh, mask, 30, 255, ThresholdTypes.Binary);

            // scan mask
            var pts = new List<OpenCvSharp.Point>();
            for (int y = 0; y < mask.Rows; y++)
                for (int x = 0; x < mask.Cols; x++)
                    if (mask.At<byte>(y, x) != 0)
                        pts.Add(new OpenCvSharp.Point(x, y));

            if (pts.Count == 0)
                roi = new Rectangle(0, 0, src.Width, src.Height);
            else {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
                foreach (var p in pts) {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
                minX = Math.Max(0, minX - 5);
                minY = Math.Max(0, minY - 5);
                int w = Math.Min(src.Width - minX, maxX - minX + 10);
                int h = Math.Min(src.Height - minY, maxY - minY + 10);
                roi = new Rectangle(minX, minY, w, h);
            }

            // crop
            Bitmap cropped = src.Clone(roi, src.PixelFormat);

            // cleanup
            mat.Dispose(); diff.Dispose(); maxCh.Dispose(); mask.Dispose();
            foreach (var m in chs) m.Dispose();

            return cropped;
        }

        private double ComputeDiffSim(Mat img1, Mat img2) {
            if (img1.Rows != img2.Rows || img1.Cols != img2.Cols)
                return 0;
            Mat diff = new Mat();
            Cv2.Absdiff(img1, img2, diff);
            Cv2.Threshold(diff, diff, 30, 255, ThresholdTypes.Binary);
            int nz = Cv2.CountNonZero(diff);
            double total = diff.Rows * diff.Cols;
            diff.Dispose();
            return 1.0 - (double)nz / total;
        }

        public void Dispose() {
            _matcher?.Dispose();
            _orb?.Dispose();
        }
    }
}
