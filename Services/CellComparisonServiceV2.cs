// Services/CellComparisonServiceV2.cs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
// Ép alias cho Size
using Size = OpenCvSharp.Size;

namespace LUDUS.Services {
    public class CellComparisonServiceV2 : IDisposable {
        private ORB _orb;
        private BFMatcher _matcher;
        private double _simThreshold;
        private double _ssimThreshold;
        private int _distThreshold;

        public event Action<string> OnLog;

        public CellComparisonServiceV2(
            double simThreshold = 0.2,
            double ssimThreshold = 0.7,
            int distThreshold = 30,
            int maxFeatures = 500) {
            _simThreshold = simThreshold;
            _ssimThreshold = ssimThreshold;
            _distThreshold = distThreshold;
            _orb = ORB.Create(maxFeatures);
            _matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        }

        public List<Tuple<string, string, double, double>> Compare(
            Dictionary<string, Bitmap> cells) {
            // Thư mục lưu ảnh đã crop
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string outDir = Path.Combine(baseDir, "ProcessedCellsV2");
            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            OnLog?.Invoke("=== PREPROCESSING CELLS ===");

            // 1) Precompute descriptors & gray mats
            var descMap = new Dictionary<string, Mat>();
            var grayMap = new Dictionary<string, Mat>();
            var kpCount = new Dictionary<string, int>();

            foreach (KeyValuePair<string, Bitmap> kv in cells) {
                string key = kv.Key;
                Bitmap bmp = kv.Value;

                // Center-crop chiều cao = 80px
                int cropH = Math.Min(70, bmp.Height);
                int y = 0;// (bmp.Height - cropH) / 2;
                Rectangle rect = new Rectangle(0, y, bmp.Width, cropH);
                Bitmap procBmp = bmp.Clone(rect, bmp.PixelFormat);

                // Lưu ảnh đã crop
                string savePath = Path.Combine(outDir, key + "_cropped.png");
                procBmp.Save(savePath);
                OnLog?.Invoke("CROPPED: " + key + " -> " + savePath);

                // Chuyển sang Mat BGR rồi gray
                Mat matBgr = BitmapConverter.ToMat(procBmp);
                Mat matGray = new Mat();
                Cv2.CvtColor(matBgr, matGray, ColorConversionCodes.BGR2GRAY);
                matBgr.Dispose();

                // ORB detect+compute
                KeyPoint[] kpArray;
                Mat desc = new Mat();
                _orb.DetectAndCompute(matGray, null, out kpArray, desc);
                kpCount[key] = kpArray.Length;

                // Lưu descriptor và gray mat
                descMap[key] = desc;
                grayMap[key] = matGray;

                procBmp.Dispose();
            }

            OnLog?.Invoke("=== PAIRWISE COMPARISON ===");

            // 2) So sánh mọi cặp
            var results = new List<Tuple<string, string, double, double>>();
            var keys = new List<string>(descMap.Keys);

            for (int i = 0; i < keys.Count; i++) {
                for (int j = i + 1; j < keys.Count; j++) {
                    string a = keys[i], b = keys[j];
                    Mat d1 = descMap[a], d2 = descMap[b];
                    Mat g1 = grayMap[a], g2 = grayMap[b];

                    // Kiểm tra descriptor tương thích
                    if (d1.Empty() || d2.Empty() ||
                        d1.Type() != d2.Type() || d1.Cols != d2.Cols) {
                        OnLog?.Invoke(
                            $"SKIP [{a}] vs [{b}] (incompatible descriptors)");
                        continue;
                    }

                    // ORB matching
                    DMatch[] matches = _matcher.Match(d1, d2);
                    int good = 0;
                    foreach (DMatch m in matches)
                        if (m.Distance < _distThreshold) good++;
                    int minKp = Math.Min(kpCount[a], kpCount[b]);
                    double sim = (minKp > 0) ? (double)good / minKp : 0.0;

                    // SSIM matching
                    double ssim = ComputeSSIM(g1, g2);

                    bool isMatch = sim >= _simThreshold || ssim >= _ssimThreshold;
                    OnLog?.Invoke(string.Format(
                        "COMPARE [{0}] vs [{1}] -> ORB={2:F2}, SSIM={3:F2} => {4}",
                        a, b, sim, ssim, isMatch ? "MATCH" : "NO MATCH"));

                    if (isMatch)
                        results.Add(Tuple.Create(a, b, sim, ssim));
                }
            }

            // 3) Cleanup
            foreach (Mat m in descMap.Values) m.Dispose();
            foreach (Mat m in grayMap.Values) m.Dispose();

            OnLog?.Invoke("=== COMPARISON COMPLETE ===");
            return results;
        }

        private double ComputeSSIM(Mat img1, Mat img2) {
            const double C1 = 6.5025, C2 = 58.5225;

            // Convert to float
            Mat I1 = new Mat(), I2 = new Mat();
            img1.ConvertTo(I1, MatType.CV_32F);
            img2.ConvertTo(I2, MatType.CV_32F);

            // Compute μ
            Mat mu1 = new Mat(), mu2 = new Mat();
            Cv2.GaussianBlur(I1, mu1, new Size(11, 11), 1.5);
            Cv2.GaussianBlur(I2, mu2, new Size(11, 11), 1.5);

            // μ² and μ1·μ2
            Mat mu1_2 = mu1.Mul(mu1);
            Mat mu2_2 = mu2.Mul(mu2);
            Mat mu1_mu2 = mu1.Mul(mu2);

            // σ² and σ12
            Mat sigma1_2 = new Mat(), sigma2_2 = new Mat(), sigma12 = new Mat(), tmp = new Mat();
            Cv2.GaussianBlur(I1.Mul(I1), tmp, new Size(11, 11), 1.5);
            Cv2.Subtract(tmp, mu1_2, sigma1_2);
            Cv2.GaussianBlur(I2.Mul(I2), tmp, new Size(11, 11), 1.5);
            Cv2.Subtract(tmp, mu2_2, sigma2_2);
            Cv2.GaussianBlur(I1.Mul(I2), tmp, new Size(11, 11), 1.5);
            Cv2.Subtract(tmp, mu1_mu2, sigma12);

            // num = (2μ1μ2 + C1)*(2σ12 + C2)
            Mat t1 = new Mat(), t2 = new Mat(), num = new Mat();
            Cv2.AddWeighted(mu1_mu2, 2.0, mu1_mu2, 0.0, C1, t1);
            Cv2.AddWeighted(sigma12, 2.0, sigma12, 0.0, C2, t2);
            Cv2.Multiply(t1, t2, num);

            // den = (μ1²+μ2²+C1)*(σ1²+σ2²+C2)
            Mat t3 = new Mat(), t4 = new Mat(), den = new Mat();
            Cv2.Add(mu1_2, mu2_2, t3);
            Cv2.AddWeighted(t3, 1.0, t3, 0.0, C1, t3);
            Cv2.Add(sigma1_2, sigma2_2, t4);
            Cv2.AddWeighted(t4, 1.0, t4, 0.0, C2, t4);
            Cv2.Multiply(t3, t4, den);

            // SSIM map
            Mat ssimMap = new Mat();
            Cv2.Divide(num, den, ssimMap);
            Scalar mssim = Cv2.Mean(ssimMap);

            // Dispose
            I1.Dispose(); I2.Dispose();
            mu1.Dispose(); mu2.Dispose();
            mu1_2.Dispose(); mu2_2.Dispose(); mu1_mu2.Dispose();
            sigma1_2.Dispose(); sigma2_2.Dispose(); sigma12.Dispose();
            tmp.Dispose(); t1.Dispose(); t2.Dispose();
            t3.Dispose(); t4.Dispose(); num.Dispose(); den.Dispose();
            ssimMap.Dispose();

            return mssim.Val0;
        }

        public void Dispose() {
            if (_matcher != null) _matcher.Dispose();
            if (_orb != null) _orb.Dispose();
        }
    }
}
