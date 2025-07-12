using System;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace LUDUS.Utils
{
    public static class PopupDetector
    {
        /// <summary>
        /// Kiểm tra ảnh BGR (Mat) có popup phủ nền tối hay không.
        /// </summary>
        public static bool HasPopup(
            Mat src,
            double minAreaRatio = 0.10,  // popup ≥10 % diện tích ảnh
            int brightnessDelta = 20,   // chênh lệch độ sáng tối thiểu
            int kernelSize = 7,    // structuring element
            int morphIterations = 2)
        {
            if (src.Empty())
                throw new ArgumentException("Input Mat is empty.");

            // 1. Lấy kênh V (độ sáng) của HSV
            Mat hsv = new Mat();
            try
            {
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
                Mat[] channels = hsv.Split();
                Mat v = channels[2];

                // 2. Nhị phân bằng Otsu trên kênh V
                Mat bin = new Mat();
                try
                {
                    Cv2.Threshold(v, bin, 0, 255,
                        ThresholdTypes.Binary | ThresholdTypes.Otsu);

                    // 3. Morphology - close để nối mảnh, lấp lỗ
                    Mat kernel = Cv2.GetStructuringElement(
                        MorphShapes.Rect, new Size(kernelSize, kernelSize));
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Close,
                        kernel, iterations: morphIterations);

                    // 4. Lấy contour lớn nhất
                    Point[][] contours;
                    HierarchyIndex[] hier;
                    Cv2.FindContours(bin, out contours, out hier,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    if (contours.Length == 0) return false;

                    int bestIdx = 0;
                    double bestArea = 0;
                    for (int i = 0; i < contours.Length; i++)
                    {
                        double area = Cv2.ContourArea(contours[i]);
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bestIdx = i;
                        }
                    }

                    double imgArea = src.Rows * src.Cols;
                    if (bestArea < minAreaRatio * imgArea)
                        return false;                         // nhỏ quá, không xem là popup

                    // 5. So sánh độ sáng trong / ngoài contour
                    Mat mask = Mat.Zeros(bin.Size(), MatType.CV_8UC1);
                    try
                    {
                        Cv2.DrawContours(mask, contours, bestIdx, Scalar.White, thickness: -1);

                        double insideV = Cv2.Mean(v, mask).Val0;
                        Mat outside = new Mat();
                        try
                        {
                            Cv2.BitwiseNot(mask, outside);
                            double outsideV = Cv2.Mean(v, outside).Val0;

                            return (insideV - outsideV) >= brightnessDelta;
                        }
                        finally
                        {
                            outside.Dispose();
                        }
                    }
                    finally
                    {
                        mask.Dispose();
                    }
                }
                finally
                {
                    bin.Dispose();
                }
            }
            finally
            {
                hsv.Dispose();
            }
        }

        /// <summary>
        /// Cắt và trả về Bitmap popup (null nếu không có).
        /// </summary>
        public static Bitmap ExtractPopup(
            Mat src,
            double minAreaRatio = 0.10,
            int kernelSize = 7,
            int morphIterations = 2)
        {
            if (src.Empty()) return null;

            // Xử lý tương tự bước trên (không lặp giải thích)
            Mat hsv = new Mat();
            try
            {
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
                Mat[] channels = hsv.Split();
                Mat v = channels[2];

                Mat bin = new Mat();
                try
                {
                    Cv2.Threshold(v, bin, 0, 255,
                        ThresholdTypes.Binary | ThresholdTypes.Otsu);

                    Mat kernel = Cv2.GetStructuringElement(
                        MorphShapes.Rect, new Size(kernelSize, kernelSize));
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Close,
                        kernel, iterations: morphIterations);

                    Point[][] contours;
                    HierarchyIndex[] hier;
                    Cv2.FindContours(bin, out contours, out hier,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    if (contours.Length == 0) return null;

                    int bestIdx = 0;
                    double bestArea = 0;
                    for (int i = 0; i < contours.Length; i++)
                    {
                        double area = Cv2.ContourArea(contours[i]);
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bestIdx = i;
                        }
                    }

                    double imgArea = src.Rows * src.Cols;
                    if (bestArea < minAreaRatio * imgArea)
                        return null;

                    Rect rect = Cv2.BoundingRect(contours[bestIdx]);
                    Mat popupMat = new Mat(src, rect);
                    try
                    {
                        return popupMat.ToBitmap();               // Bitmap cho WinForms/WPF
                    }
                    finally
                    {
                        popupMat.Dispose();
                    }
                }
                finally
                {
                    bin.Dispose();
                }
            }
            finally
            {
                hsv.Dispose();
            }
        }
    }
} 