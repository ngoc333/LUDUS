using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Services {
    public class CellComparisonService : IDisposable {
        private ORB _orb;
        private BFMatcher _matcher;
        private double _similarityThreshold;
        private int _distanceThreshold;

        public CellComparisonService(double similarityThreshold = 0.25,
                                     int distanceThreshold = 30,
                                     int maxFeatures = 500) {
            _similarityThreshold = similarityThreshold;
            _distanceThreshold = distanceThreshold;
            _orb = ORB.Create(maxFeatures);
            _matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        }

        /// <summary>
        /// Nhận vào dictionary: key→Bitmap của mỗi cell,
        /// so sánh mọi cặp và trả về những cặp sim ≥ threshold.
        /// </summary>
        public List<Tuple<string, string, double>> Compare(
            Dictionary<string, Bitmap> cellBitmaps) {
            // 1) Tính descriptor và số keypoint cho mỗi cell
            var descMap = new Dictionary<string, Mat>();
            var kpCount = new Dictionary<string, int>();

            foreach (var kv in cellBitmaps) {
                string key = kv.Key;
                Bitmap bmp = kv.Value;

                // Bitmap -> Mat -> gray
                Mat matColor = BitmapConverter.ToMat(bmp);
                Mat matGray = new Mat();
                Cv2.CvtColor(matColor, matGray, ColorConversionCodes.BGR2GRAY);
                matColor.Dispose();

                KeyPoint[] kp;
                Mat desc = new Mat();
                _orb.DetectAndCompute(matGray, null, out kp, desc);
                kpCount[key] = kp.Length;
                matGray.Dispose();

                descMap[key] = desc;  // giữ lại để match
            }

            // 2) So sánh pairwise
            var results = new List<Tuple<string, string, double>>();
            var keys = descMap.Keys.ToList();
            for (int i = 0; i < keys.Count; i++) {
                for (int j = i + 1; j < keys.Count; j++) {
                    string k1 = keys[i];
                    string k2 = keys[j];
                    Mat d1 = descMap[k1];
                    Mat d2 = descMap[k2];

                    if (d1.Empty() || d2.Empty())
                        continue;

                    DMatch[] matches = _matcher.Match(d1, d2);
                    int good = matches.Count(m => m.Distance < _distanceThreshold);
                    int minKp = Math.Min(kpCount[k1], kpCount[k2]);
                    if (minKp == 0)
                        continue;

                    double sim = (double)good / minKp;
                    if (sim >= _similarityThreshold)
                        results.Add(Tuple.Create(k1, k2, sim));
                }
            }

            // 3) Giải phóng descriptor mats
            foreach (var desc in descMap.Values)
                desc.Dispose();

            return results;
        }

        public void Dispose() {
            if (_matcher != null) _matcher.Dispose();
            if (_orb != null) _orb.Dispose();
        }
    }
}
