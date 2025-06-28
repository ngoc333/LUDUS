using System;
using System.Drawing;
using System.IO;
using System.Linq;
using LUDUS.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Services {
    public class ScreenDetectionService {
        private readonly ScreenCaptureService _capture;
        private readonly AdbService _adb;
        private readonly string _regionsXmlPath;
        private readonly string _templateBasePath;
        private const double MatchThreshold = 0.95;
        private const int MaxRetries = 10;

        public ScreenDetectionService(
            ScreenCaptureService captureService,
            AdbService adbService,
            string regionsXmlPath,
            string templateBasePath) {
            _capture = captureService;
            _adb = adbService;
            _regionsXmlPath = regionsXmlPath;
            _templateBasePath = templateBasePath;
        }

        public string DetectScreen(string deviceId, Action<string> log) {
            for (int attempt = 1; attempt <= MaxRetries; attempt++) {
                using (var screenshot = _capture.Capture(deviceId) as Bitmap) {
                    if (screenshot == null) {
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }

                    // 1. Thử template screen
                    var regions = RegionLoader.LoadPresetRegions(_regionsXmlPath)
                                              .Where(r => r.Group == "ScreenRegions");
                    foreach (var region in regions) {
                        string name = region.Name;
                        Rectangle rect = region.Rect;
                        using (var crop = screenshot.Clone(rect, screenshot.PixelFormat)) {
                            string tplPath = Path.Combine(_templateBasePath, "Screen", name + ".png");
                            if (!File.Exists(tplPath))
                                continue;
                            using (var tpl = new Bitmap(tplPath)) {
                                if (ImageCompare.AreSame(crop, tpl)) {
                                    return name;
                                }
                            }
                        }
                    }

                    // 2. Nếu chưa detect được, thử detect + tap button
                    string btn = DetectButtonWithOpenCvAndTap(deviceId, screenshot, log);
                    if (!string.IsNullOrEmpty(btn)) {
                        return btn;
                    }
                    
                    System.Threading.Thread.Sleep(1000);
                }
            }
            return "unknown";
        }

        // Detect and tap button via OpenCV
        private string DetectButtonWithOpenCvAndTap(string deviceId, Bitmap bmpScreenshot, Action<string> log) {
            using (var mat0 = BitmapConverter.ToMat(bmpScreenshot))
            using (var mat = new Mat()) {
                Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                string buttonFolder = Path.Combine(_templateBasePath, "Button");
                if (!Directory.Exists(buttonFolder)) {
                    return null;
                }

                foreach (var file in Directory.GetFiles(buttonFolder, "*.png")) {
                    string btnName = Path.GetFileNameWithoutExtension(file);
                    using (var tplMat = Cv2.ImRead(file, ImreadModes.Color))
                    using (var result = new Mat()) {
                        Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                        if (maxVal >= MatchThreshold) {
                            int xTap = maxLoc.X + tplMat.Width / 2;
                            int yTap = maxLoc.Y + tplMat.Height / 2;
                            _adb.RunShellPersistent($"input tap {xTap} {yTap}");
                            log?.Invoke($"Button: {btnName}");
                            System.Threading.Thread.Sleep(500);
                            return btnName;
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Kiểm tra vùng loading bằng OCR, trả về true nếu vẫn còn loading
        /// </summary>
        public bool IsScreenLoadingByOcr(string deviceId, Func<Bitmap, string> ocrFunc, Action<string> log) {
            var region = RegionLoader.LoadPresetRegions(_regionsXmlPath)
                .FirstOrDefault(r => r.Group == "ScreenRegions" && r.Name == "Loading");
            if (region == null) return false;

            using (var bmp = _capture.Capture(deviceId) as Bitmap) {
                if (bmp == null) return false;

                using (var crop = bmp.Clone(region.Rect, bmp.PixelFormat)) {
                    string ocrResult = ocrFunc(crop)?.Trim();

                    return !string.IsNullOrEmpty(ocrResult) &&
                           ocrResult.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }

        /// <summary>
        /// Kiểm tra kết quả trận đấu (thắng/thua) bằng cách so sánh với ảnh Victory.png
        /// </summary>
        /// <returns>true nếu thắng, false nếu thua</returns>
        public bool DetectVictoryResult(string deviceId, Action<string> log)
        {
            using (var screenshot = _capture.Capture(deviceId) as Bitmap)
            {
                if (screenshot == null)
                {
                    return false;
                }
                string tplPath = Path.Combine(_templateBasePath, "Battle", "Victory.png");
                if (!File.Exists(tplPath))
                {
                    return false;
                }
                using (var mat0 = BitmapConverter.ToMat(screenshot))
                using (var mat = new Mat())
                using (var tplMat = Cv2.ImRead(tplPath, ImreadModes.Color))
                using (var result = new Mat())
                {
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);
                    Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                    DetectButtonWithOpenCvAndTap(deviceId, screenshot, log);
                    if (maxVal >= MatchThreshold)
                    {
                        log?.Invoke("Victory");
                        return true;
                    }
                    else
                    {
                        log?.Invoke("Defeat");
                        return false;
                    }
                    
                }
            }
        }

        /// <summary>
        /// Kiểm tra nhanh xem có đang ở màn hình CombatBoosts hay không
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu đang ở màn hình CombatBoosts</returns>
        public bool IsCombatBoostsScreen(string deviceId, Action<string> log)
        {
            try
            {
                using (var screenshot = _capture.Capture(deviceId) as Bitmap)
                {
                    if (screenshot == null) return false;

                    var regions = RegionLoader.LoadPresetRegions(_regionsXmlPath)
                                              .Where(r => r.Group == "ScreenRegions" && r.Name == "CombatBoosts");
                    
                    foreach (var region in regions)
                    {
                        using (var crop = screenshot.Clone(region.Rect, screenshot.PixelFormat))
                        {
                            string tplPath = Path.Combine(_templateBasePath, "Screen", "CombatBoosts.png");
                            if (!File.Exists(tplPath)) continue;
                            
                            using (var tpl = new Bitmap(tplPath))
                            {
                                if (ImageCompare.AreSame(crop, tpl))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Lỗi khi kiểm tra CombatBoosts: {ex.Message}");
            }
            
            return false;
        }
    }
}
