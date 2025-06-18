using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Services {
    public class ImageRecognitionService {
        private readonly string _templatePath;
        private readonly Dictionary<string, Bitmap> _templates;
        private const double SIMILARITY_THRESHOLD = 0.8; // Ngưỡng template matching

        public ImageRecognitionService(string templatePath) {
            _templatePath = templatePath;
            _templates = new Dictionary<string, Bitmap>();
            LoadTemplates();
        }

        private void LoadTemplates() {
            if (!Directory.Exists(_templatePath)) {
                Directory.CreateDirectory(_templatePath);
            }

            // Load tất cả template từ thư mục
            foreach (var file in Directory.GetFiles(_templatePath, "*.png")) {
                var name = Path.GetFileNameWithoutExtension(file);
                _templates[name] = new Bitmap(file);
            }
        }

        private double MatchTemplateContains(Mat large, Mat small) {
            // Kiểm tra null hoặc empty
            if (large == null || small == null || large.Empty() || small.Empty())
                return 0;

            // Kiểm tra kích thước
            if (small.Width > large.Width || small.Height > large.Height)
                return 0;

            // Chuyển về grayscale 8-bit
            Mat largeGray = new Mat();
            Mat smallGray = new Mat();

            if (large.Channels() == 3)
                Cv2.CvtColor(large, largeGray, ColorConversionCodes.BGR2GRAY);
            else if (large.Channels() == 4)
                Cv2.CvtColor(large, largeGray, ColorConversionCodes.BGRA2GRAY);
            else
                largeGray = large.Clone();

            if (small.Channels() == 3)
                Cv2.CvtColor(small, smallGray, ColorConversionCodes.BGR2GRAY);
            else if (small.Channels() == 4)
                Cv2.CvtColor(small, smallGray, ColorConversionCodes.BGRA2GRAY);
            else
                smallGray = small.Clone();

            // Đảm bảo depth là CV_8U
            if (largeGray.Depth() != MatType.CV_8U)
                largeGray = largeGray.ConvertTo(MatType.CV_8U);
            if (smallGray.Depth() != MatType.CV_8U)
                smallGray = smallGray.ConvertTo(MatType.CV_8U);

            // Kiểm tra lại kích thước sau khi chuyển đổi
            if (smallGray.Width > largeGray.Width || smallGray.Height > largeGray.Height)
                return 0;

            // Log thông tin ảnh
            Console.WriteLine($"Input: {largeGray.Width}x{largeGray.Height}x{largeGray.Channels()} type={largeGray.Type()}");
            Console.WriteLine($"Template: {smallGray.Width}x{smallGray.Height}x{smallGray.Channels()} type={smallGray.Type()}");

            // Template matching
            Mat result = new Mat();
            try
            {
                Cv2.MatchTemplate(largeGray, smallGray, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out _, out _);
                return maxVal;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MatchTemplate error: {ex.Message}");
                return 0;
            }
        }

        public string RecognizeHero(Bitmap cellImage) {
            if (cellImage == null)
                return "unknown";
            Mat inputMat = BitmapConverter.ToMat(cellImage);
            if (inputMat.Empty())
                return "unknown";
            string bestMatch = "unknown";
            double bestScore = 0;

            foreach (var kv in _templates) {
                if (kv.Value == null) continue;
                Mat templateMat = BitmapConverter.ToMat(kv.Value);
                if (templateMat.Empty()) continue;
                double score = MatchTemplateContains(inputMat, templateMat);
                Console.WriteLine($"So sánh template nhỏ với {kv.Key}: {score}");
                if (score > bestScore) {
                    bestScore = score;
                    bestMatch = kv.Key;
                }
            }

            if (bestScore >= SIMILARITY_THRESHOLD)
                return bestMatch;
            return "unknown";
        }

        public void SaveTemplate(Bitmap image, string name) {
            // Lưu nguyên ảnh template nhỏ
            string path = Path.Combine(_templatePath, $"{name}.png");
            image.Save(path, ImageFormat.Png);
            _templates[name] = new Bitmap(image);
        }

        public bool IsEmptyCell(Bitmap cellImage) {
            // Kiểm tra ô trống dựa trên độ tương phản toàn ảnh
            Mat mat = BitmapConverter.ToMat(cellImage);
            double contrast = CalculateContrast(mat);
            return contrast < 0.1;
        }

        private double CalculateContrast(Mat mat) {
            // Tính độ lệch chuẩn của pixel grayscale
            Mat gray = new Mat();
            if (mat.Channels() == 3)
                Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            else
                gray = mat.Clone();
            var mean = Cv2.Mean(gray)[0];
            var stddev = new Scalar();
            Cv2.MeanStdDev(gray, out _, out stddev);
            return stddev.Val0 / 255.0;
        }
    }
} 
