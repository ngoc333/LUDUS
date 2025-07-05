using System;
using System.Drawing;
using System.IO;
using System.Linq;
using LUDUS.Utils;
using OpenCvSharp.Extensions;
using OpenCvSharp;

namespace LUDUS.Services {
    public class PvpNavigationService {
        private readonly AdbService _adb;
        private readonly ScreenCaptureService _capture;
        private readonly string _regionsXmlPath;
        private readonly string _templateBasePath;

        public PvpNavigationService(
            AdbService adb,
            ScreenCaptureService capture,
            string regionsXmlPath,
            string templateBasePath) {
            _adb = adb;
            _capture = capture;
            _regionsXmlPath = regionsXmlPath;
            _templateBasePath = templateBasePath;
        }

        /// <summary>
        /// Chỉ cố gắng click PVP nếu đang ở Main. Trả về true nếu thành công, false nếu không tìm thấy/click được PVP.
        /// </summary>
        public bool GoToPvp(string deviceId, Action<string> log) {
            using (var bmp = _capture.Capture(deviceId) as Bitmap) {
                if (bmp == null) {
                    log?.Invoke("Failed to capture screen.");
                    return false;
                }
                using (var mat0 = BitmapConverter.ToMat(bmp))
                using (var mat = new Mat()) {
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                    // Load template PVP
                    string tplPath = Path.Combine(_templateBasePath, "Main", "PVP.png");
                    if (!File.Exists(tplPath)) {
                        log?.Invoke("PVP template not found.");
                        return false;
                    }

                    using (var tplMat = Cv2.ImRead(tplPath, ImreadModes.Color))
                    using (var result = new Mat()) {
                        Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                        if (maxVal >= 0.95) // có thể hạ xuống 0.90 nếu nút bị mờ
                        {
                            int xTap = maxLoc.X + tplMat.Width / 2;
                            int yTap = maxLoc.Y + tplMat.Height / 2;
                            _adb.RunShellPersistent($"input tap {xTap} {yTap}");
                            log?.Invoke($"Click PVP button");
                            return true;
                        }
                    }
                }
            }
            log?.Invoke("PVP button not found");
            return false;
        }

        /// <summary>
        /// Chỉ cố gắng click PVP nếu đang ở Main. Trả về true nếu thành công, false nếu không tìm thấy/click được PVP.
        /// </summary>
        public bool GoToAstra(string deviceId, Action<string> log) {
            using (var bmp = _capture.Capture(deviceId) as Bitmap) {
                if (bmp == null) {
                    log?.Invoke("Failed to capture screen.");
                    return false;
                }
                using (var mat0 = BitmapConverter.ToMat(bmp))
                using (var mat = new Mat()) {
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                    // Load template PVP
                    string tplPath = Path.Combine(_templateBasePath, "Main", "Astra.png");
                    if (!File.Exists(tplPath)) {
                        log?.Invoke("Astra template not found.");
                        return false;
                    }

                    using (var tplMat = Cv2.ImRead(tplPath, ImreadModes.Color))
                    using (var result = new Mat()) {
                        Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                        if (maxVal >= 0.95) // có thể hạ xuống 0.90 nếu nút bị mờ
                        {
                            int xTap = maxLoc.X + tplMat.Width / 2;
                            int yTap = maxLoc.Y + tplMat.Height / 2;
                            _adb.RunShellPersistent($"input tap {xTap} {yTap}");
                            log?.Invoke($"Click Astra button");
                            return true;
                        }
                    }
                }
            }
            log?.Invoke("Astra button not found");
            return false;
        }
    }
}
