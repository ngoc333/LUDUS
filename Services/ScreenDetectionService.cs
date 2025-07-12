using System;
using System.Drawing;
using System.IO;
using System.Linq;
using LUDUS.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Threading.Tasks;

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

                    // Kiểm tra popup trước khi detect screen
                    CheckPopupAndLog(screenshot, log);

                    // 1. Thử template screen với OpenCV
                    string screenResult = DetectScreenWithOpenCv(screenshot, log);
                    if (!string.IsNullOrEmpty(screenResult)) {
                        return screenResult;
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

        // Phiên bản async thực sự: sử dụng await/async để tránh block UI
        public async Task<string> DetectScreenAsync(string deviceId, Action<string> log, System.Threading.CancellationToken ct = default) {
            for (int attempt = 1; attempt <= MaxRetries && !ct.IsCancellationRequested; attempt++) {
                // Chụp màn hình trên thread pool để không chặn UI
                using (var screenshot = await Task.Run(() => _capture.Capture(deviceId), ct) as Bitmap) {
                    if (screenshot == null) {
                        await Task.Delay(1000, ct);
                        continue;
                    }

                    // Kiểm tra popup trước khi detect screen
                    CheckPopupAndLog(screenshot, log);

                    // 1. Thử template screen với OpenCV
                    string screenResult = await Task.Run(() => DetectScreenWithOpenCv(screenshot, log), ct);
                    if (!string.IsNullOrEmpty(screenResult)) {
                        return screenResult;
                    }

                    // 2. Nếu chưa detect được, thử detect + tap button
                    string btn = await Task.Run(() => DetectButtonWithOpenCvAndTap(deviceId, screenshot, log), ct);
                    if (!string.IsNullOrEmpty(btn)) {
                        return btn;
                    }

                    await Task.Delay(1000, ct);
                }
            }
            return "unknown";
        }

        // Overload không có CancellationToken để đảm bảo tương thích ngược
        public Task<string> DetectScreenAsync(string deviceId, Action<string> log)
            => DetectScreenAsync(deviceId, log, System.Threading.CancellationToken.None);

        // Detect screen via OpenCV template matching
        private string DetectScreenWithOpenCv(Bitmap bmpScreenshot, Action<string> log) {
            try {
                if (bmpScreenshot == null) {
                    log?.Invoke("[DetectScreenWithOpenCv] Screenshot là null");
                    return null;
                }

                // Kiểm tra kích thước bitmap
                if (bmpScreenshot.Width <= 0 || bmpScreenshot.Height <= 0) {
                    log?.Invoke($"[DetectScreenWithOpenCv] Kích thước screenshot không hợp lệ: {bmpScreenshot.Width}x{bmpScreenshot.Height}");
                    return null;
                }

                using (var mat0 = BitmapConverter.ToMat(bmpScreenshot))
                using (var mat = new Mat()) {
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                    string screenFolder = Path.Combine(_templateBasePath, "Screen");
                    if (!Directory.Exists(screenFolder)) {
                        log?.Invoke($"[DetectScreenWithOpenCv] Không tìm thấy thư mục template Screen: {screenFolder}");
                        return null;
                    }

                    var tplFiles = Directory.GetFiles(screenFolder, "*.png")
                                          .OrderBy(f => Path.GetFileNameWithoutExtension(f));

                    foreach (var tplPath in tplFiles) {
                        string name = Path.GetFileNameWithoutExtension(tplPath);

                        using (var tplMat = Cv2.ImRead(tplPath, ImreadModes.Color)) {
                            if (tplMat.Empty()) {
                                log?.Invoke($"[DetectScreenWithOpenCv] Không thể đọc template: {tplPath}");
                                continue;
                            }

                            using (var result = new Mat()) {
                                Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                                if (maxVal >= MatchThreshold) {
                                    log?.Invoke($"Screen detected: {name}");
                                    return name;
                                }
                            }
                        }
                    }
                    return null;
                }
            } catch (Exception ex) {
                log?.Invoke($"[DetectScreenWithOpenCv] Lỗi: {ex.Message}");
                return null;
            }
        }

        // Detect and tap button via OpenCV
        private string DetectButtonWithOpenCvAndTap(string deviceId, Bitmap bmpScreenshot, Action<string> log) {
            try {
                if (bmpScreenshot == null) {
                    log?.Invoke("[DetectButtonWithOpenCvAndTap] Screenshot là null");
                    return null;
                }

                // Kiểm tra kích thước bitmap
                if (bmpScreenshot.Width <= 0 || bmpScreenshot.Height <= 0) {
                    log?.Invoke($"[DetectButtonWithOpenCvAndTap] Kích thước screenshot không hợp lệ: {bmpScreenshot.Width}x{bmpScreenshot.Height}");
                    return null;
                }

                using (var mat0 = BitmapConverter.ToMat(bmpScreenshot))
                using (var mat = new Mat()) {
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                    string buttonFolder = Path.Combine(_templateBasePath, "Button");
                    if (!Directory.Exists(buttonFolder)) {
                        return null;
                    }

                    var buttonTpls = Directory.GetFiles(buttonFolder, "*.png")
                                             .OrderBy(f => Path.GetFileNameWithoutExtension(f));

                    foreach (var file in buttonTpls) {
                        string btnName = Path.GetFileNameWithoutExtension(file);
                        using (var tplMat = Cv2.ImRead(file, ImreadModes.Color)) {
                            if (tplMat.Empty()) {
                                log?.Invoke($"[DetectButtonWithOpenCvAndTap] Không thể đọc template: {file}");
                                continue;
                            }

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
                    }
                }
                return null;
            } catch (Exception ex) {
                log?.Invoke($"[DetectButtonWithOpenCvAndTap] Lỗi: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Kiểm tra kết quả trận đấu (thắng/thua) bằng cách so sánh với ảnh Victory.png
        /// </summary>
        /// <returns>true nếu thắng, false nếu thua</returns>
        public bool DetectVictoryResult(string deviceId, Action<string> log) {
            try {
                using (var screenshot = _capture.Capture(deviceId) as Bitmap) {
                    if (screenshot == null) {
                        log?.Invoke("[DetectVictoryResult] Không thể chụp màn hình");
                        return false;
                    }

                    // Kiểm tra kích thước bitmap
                    if (screenshot.Width <= 0 || screenshot.Height <= 0) {
                        log?.Invoke($"[DetectVictoryResult] Kích thước screenshot không hợp lệ: {screenshot.Width}x{screenshot.Height}");
                        return false;
                    }

                    string tplPath = Path.Combine(_templateBasePath, "Battle", "Victory.png");
                    if (!File.Exists(tplPath)) {
                        log?.Invoke("[DetectVictoryResult] Không tìm thấy template Victory.png");
                        return false;
                    }
                    using (var mat0 = BitmapConverter.ToMat(screenshot))
                    using (var mat = new Mat())
                    using (var tplMat = Cv2.ImRead(tplPath, ImreadModes.Color)) {
                        if (tplMat.Empty()) {
                            log?.Invoke("[DetectVictoryResult] Không thể đọc template Victory.png");
                            return false;
                        }

                        using (var result = new Mat()) {
                            Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);
                            Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);
                            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                            DetectButtonWithOpenCvAndTap(deviceId, screenshot, log);
                            if (maxVal >= MatchThreshold) {
                                log?.Invoke("Victory");
                                return true;
                            }
                            else {
                                log?.Invoke("Defeat");
                                return false;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                log?.Invoke($"[DetectVictoryResult] Lỗi: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra nhanh xem có đang ở màn hình CombatBoosts hay không
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu đang ở màn hình CombatBoosts</returns>
        public bool IsCombatBoostsScreen(string deviceId, Action<string> log) {
            try {
                using (var screenshot = _capture.Capture(deviceId) as Bitmap) {
                    if (screenshot == null) {
                        log?.Invoke("[IsCombatBoostsScreen] Không thể chụp màn hình");
                        return false;
                    }

                    // Kiểm tra kích thước bitmap
                    if (screenshot.Width <= 0 || screenshot.Height <= 0) {
                        log?.Invoke($"[IsCombatBoostsScreen] Kích thước screenshot không hợp lệ: {screenshot.Width}x{screenshot.Height}");
                        return false;
                    }

                    var regions = RegionLoader.LoadPresetRegions(_regionsXmlPath)
                                              .Where(r => r.Group == "ScreenRegions" && r.Name == "CombatBoosts");

                    foreach (var region in regions) {
                        // Kiểm tra rect có hợp lệ không
                        if (region.Rect.X < 0 || region.Rect.Y < 0 ||
                            region.Rect.Right > screenshot.Width ||
                            region.Rect.Bottom > screenshot.Height) {
                            log?.Invoke($"[IsCombatBoostsScreen] Rect không hợp lệ: {region.Rect}, Screenshot size: {screenshot.Width}x{screenshot.Height}");
                            continue;
                        }

                        using (var crop = screenshot.Clone(region.Rect, screenshot.PixelFormat)) {
                            string tplPath = Path.Combine(_templateBasePath, "Screen", "CombatBoosts.png");
                            if (!File.Exists(tplPath)) {
                                log?.Invoke("[IsCombatBoostsScreen] Không tìm thấy template CombatBoosts.png");
                                continue;
                            }

                            using (var tpl = new Bitmap(tplPath)) {
                                if (ImageCompare.AreSame(crop, tpl)) {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                log?.Invoke($"Lỗi khi kiểm tra CombatBoosts: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Kiểm tra popup và ghi log nếu phát hiện
        /// </summary>
        private void CheckPopupAndLog(Bitmap screenshot, Action<string> log) {
            try {
                // Chuyển đổi Bitmap sang Mat
                using (var mat = BitmapConverter.ToMat(screenshot)) {
                    // Kiểm tra popup
                    bool hasPopup = PopupDetector.HasPopup(mat);
                    
                    if (hasPopup) {
                        log?.Invoke("🔍 Phát hiện popup phủ nền tối!");
                        
                        // Lưu ảnh popup nếu cần
                        //Bitmap popupBitmap = PopupDetector.ExtractPopup(mat);
                        //if (popupBitmap != null) {
                        //    try {
                        //        string popupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Popups");
                        //        if (!Directory.Exists(popupDir)) Directory.CreateDirectory(popupDir);

                        //        string fileName = $"Popup_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        //        string filePath = Path.Combine(popupDir, fileName);

                        //        popupBitmap.Save(filePath);
                        //        log?.Invoke($"📸 Đã lưu ảnh popup: {filePath}");
                        //    }
                        //    catch (Exception ex) {
                        //        log?.Invoke($"❌ Lỗi khi lưu ảnh popup: {ex.Message}");
                        //    }
                        //    finally {
                        //        popupBitmap.Dispose();
                        //    }
                        //}
                    }
                }
            }
            catch (Exception ex) {
                log?.Invoke($"❌ Lỗi khi kiểm tra popup: {ex.Message}");
            }
        }
    }
}
