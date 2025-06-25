using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LUDUS.Utils;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace LUDUS.Services
{
    public class RoundDetectionService
    {
        private readonly ScreenCaptureService _capture;
        private readonly List<RegionInfo> _regions;
        private readonly string _templateBasePath;
        private const double MATCH_THRESHOLD = 0.8;

        public RoundDetectionService(
            ScreenCaptureService captureService,
            string regionsXmlPath,
            string templateBasePath)
        {
            _capture = captureService;
            _regions = RegionLoader.LoadPresetRegions(regionsXmlPath);
            _templateBasePath = templateBasePath;
        }

        /// <summary>
        /// Kiểm tra có phải round 1 hay không bằng cách đếm số lượng lifeEmpty.png trong Life1 và Life2
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu là round 1 (KHÔNG có lifeEmpty trong Life1 và Life2)</returns>
        public async Task<bool> IsRound1(string deviceId, Action<string> log)
        {
            try
            {
                log?.Invoke("Đang kiểm tra round 1...");

                // Lấy tọa độ Life1 và Life2 từ MySideCell
                var life1Region = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "Life1");
                var life2Region = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "Life2");

                if (life1Region == null || life2Region == null)
                {
                    log?.Invoke("Không tìm thấy vùng Life1 hoặc Life2 trong cấu hình");
                    return false;
                }

                // Chụp màn hình
                using (var screenshot = _capture.Capture(deviceId) as Bitmap)
                {
                    if (screenshot == null)
                    {
                        log?.Invoke("Không thể chụp màn hình");
                        return false;
                    }

                    // Đếm lifeEmpty trong Life1
                    int life1EmptyCount = await CountLifeEmptyInRegion(screenshot, life1Region.Rect, "Life1", log);
                    
                    // Đếm lifeEmpty trong Life2
                    int life2EmptyCount = await CountLifeEmptyInRegion(screenshot, life2Region.Rect, "Life2", log);

                    int totalEmptyCount = life1EmptyCount + life2EmptyCount;
                    log?.Invoke($"Tổng số lifeEmpty tìm thấy: {totalEmptyCount} (Life1: {life1EmptyCount}, Life2: {life2EmptyCount})");

                    // Round 1 là khi KHÔNG có lifeEmpty
                    bool isRound1 = totalEmptyCount == 0;
                    log?.Invoke($"Kết quả: {(isRound1 ? "ROUND 1" : $"ROUND {totalEmptyCount + 1}")}");
                    
                    return isRound1;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Lỗi khi kiểm tra round 1: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Đếm số lượng lifeEmpty.png trong một vùng cụ thể
        /// </summary>
        /// <param name="screenshot">Ảnh chụp màn hình</param>
        /// <param name="region">Vùng cần kiểm tra</param>
        /// <param name="regionName">Tên vùng để log</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>Số lượng lifeEmpty tìm thấy</returns>
        private async Task<int> CountLifeEmptyInRegion(Bitmap screenshot, Rectangle region, string regionName, Action<string> log)
        {
            try
            {
                // Cắt vùng cần kiểm tra
                using (var croppedImage = screenshot.Clone(region, screenshot.PixelFormat))
                using (var mat0 = BitmapConverter.ToMat(croppedImage))
                using (var mat = new Mat())
                {
                    // Chuyển đổi sang BGR (tương tự như hàm tham khảo)
                    Cv2.CvtColor(mat0, mat, ColorConversionCodes.BGRA2BGR);

                    // Đường dẫn đến template lifeEmpty.png
                    string templatePath = Path.Combine(_templateBasePath, "Battle", "lifeEmpty.png");
                    
                    if (!File.Exists(templatePath))
                    {
                        log?.Invoke($"Không tìm thấy file template: {templatePath}");
                        return 0;
                    }

                    // Đọc template
                    using (var tplMat = Cv2.ImRead(templatePath, ImreadModes.Color))
                    using (var result = new Mat())
                    {
                        if (tplMat.Empty())
                        {
                            log?.Invoke("Không thể đọc template lifeEmpty.png");
                            return 0;
                        }

                        // Thực hiện template matching
                        Cv2.MatchTemplate(mat, tplMat, result, TemplateMatchModes.CCoeffNormed);

                        // Tìm tất cả các match
                        var matches = new List<OpenCvSharp.Point>();
                        var resultClone = result.Clone();

                        while (true)
                        {
                            Cv2.MinMaxLoc(resultClone, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                            
                            if (maxVal >= MATCH_THRESHOLD)
                            {
                                matches.Add(maxLoc);
                                
                                // Xóa vùng đã tìm thấy để tìm tiếp
                                Cv2.Rectangle(resultClone, 
                                    new Rect(maxLoc.X - tplMat.Width/2, maxLoc.Y - tplMat.Height/2, 
                                            tplMat.Width, tplMat.Height), 
                                    Scalar.Black, -1);
                            }
                            else
                            {
                                break;
                            }
                        }

                        return matches.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Lỗi khi đếm lifeEmpty trong {regionName}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết về round hiện tại
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>Thông tin chi tiết về round</returns>
        public async Task<RoundInfo> GetRoundInfo(string deviceId, Action<string> log)
        {
            try
            {
                var life1Region = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "Life1");
                var life2Region = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "Life2");

                if (life1Region == null || life2Region == null)
                {
                    log?.Invoke("Không tìm thấy vùng Life1 hoặc Life2");
                    return new RoundInfo { IsRound1 = false, Life1EmptyCount = 0, Life2EmptyCount = 0, CalculatedRound = 1 };
                }

                using (var screenshot = _capture.Capture(deviceId) as Bitmap)
                {
                    if (screenshot == null)
                    {
                        log?.Invoke("Không thể chụp màn hình");
                        return new RoundInfo { IsRound1 = false, Life1EmptyCount = 0, Life2EmptyCount = 0, CalculatedRound = 1 };
                    }

                    int life1EmptyCount = await CountLifeEmptyInRegion(screenshot, life1Region.Rect, "Life1", log);
                    int life2EmptyCount = await CountLifeEmptyInRegion(screenshot, life2Region.Rect, "Life2", log);

                    // Tính round: số lifeEmpty + 1
                    int calculatedRound = life1EmptyCount + life2EmptyCount + 1;
                    bool isRound1 = calculatedRound == 1;

                    var roundInfo = new RoundInfo
                    {
                        IsRound1 = isRound1,
                        Life1EmptyCount = life1EmptyCount,
                        Life2EmptyCount = life2EmptyCount,
                        TotalEmptyCount = life1EmptyCount + life2EmptyCount,
                        CalculatedRound = calculatedRound
                    };

                    // Chỉ log thông tin tổng hợp
                    log?.Invoke($"Round {calculatedRound} (Life1: {life1EmptyCount}, Life2: {life2EmptyCount})");

                    return roundInfo;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Lỗi khi lấy thông tin round: {ex.Message}");
                return new RoundInfo { IsRound1 = false, Life1EmptyCount = 0, Life2EmptyCount = 0, CalculatedRound = 1 };
            }
        }

        /// <summary>
        /// Lưu file hình của Life1 và Life2 để kiểm tra
        /// </summary>
        /// <param name="deviceId">ID của thiết bị</param>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu lưu thành công</returns>
        public async Task<bool> SaveLifeRegions(string deviceId, Action<string> log)
        {
            try
            {
                log?.Invoke("Đang lưu file hình Life1 và Life2...");

                // Lấy tọa độ Life1 và Life2 từ MySideCell
                var life1Region = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "Life1");
                var life2Region = _regions.FirstOrDefault(r => r.Group == "MySideCell" && r.Name == "Life2");

                if (life1Region == null || life2Region == null)
                {
                    log?.Invoke("Không tìm thấy vùng Life1 hoặc Life2 trong cấu hình");
                    return false;
                }

                // Chụp màn hình
                using (var screenshot = _capture.Capture(deviceId) as Bitmap)
                {
                    if (screenshot == null)
                    {
                        log?.Invoke("Không thể chụp màn hình");
                        return false;
                    }

                    // Tạo thư mục để lưu
                    string saveDir = Path.Combine(Application.StartupPath, "LifeRegions");
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }

                    // Lưu Life1
                    using (var life1Image = screenshot.Clone(life1Region.Rect, screenshot.PixelFormat))
                    {
                        string life1Path = Path.Combine(saveDir, $"Life1_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                        life1Image.Save(life1Path);
                        log?.Invoke($"Đã lưu Life1: {life1Path}");
                    }

                    // Lưu Life2
                    using (var life2Image = screenshot.Clone(life2Region.Rect, screenshot.PixelFormat))
                    {
                        string life2Path = Path.Combine(saveDir, $"Life2_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                        life2Image.Save(life2Path);
                        log?.Invoke($"Đã lưu Life2: {life2Path}");
                    }

                    // Lưu toàn bộ màn hình với đánh dấu vùng Life1 và Life2
                    using (var markedScreenshot = new Bitmap(screenshot))
                    using (var graphics = Graphics.FromImage(markedScreenshot))
                    {
                        // Vẽ khung đỏ cho Life1
                        using (var redPen = new Pen(Color.Red, 3))
                        {
                            graphics.DrawRectangle(redPen, life1Region.Rect);
                            graphics.DrawString("Life1", new Font("Arial", 12, FontStyle.Bold), Brushes.Red, 
                                life1Region.Rect.X, life1Region.Rect.Y - 20);
                        }

                        // Vẽ khung xanh cho Life2
                        using (var bluePen = new Pen(Color.Blue, 3))
                        {
                            graphics.DrawRectangle(bluePen, life2Region.Rect);
                            graphics.DrawString("Life2", new Font("Arial", 12, FontStyle.Bold), Brushes.Blue, 
                                life2Region.Rect.X, life2Region.Rect.Y - 20);
                        }

                        string markedPath = Path.Combine(saveDir, $"MarkedScreen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                        markedScreenshot.Save(markedPath);
                        log?.Invoke($"Đã lưu màn hình có đánh dấu: {markedPath}");
                    }

                    log?.Invoke($"✅ Đã lưu thành công các file hình vào thư mục: {saveDir}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"❌ Lỗi khi lưu file hình: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lưu template lifeEmpty.png để debug
        /// </summary>
        /// <param name="log">Callback để log thông tin</param>
        /// <returns>True nếu lưu thành công</returns>
        public async Task<bool> SaveTemplateForDebug(Action<string> log)
        {
            try
            {
                string templatePath = Path.Combine(_templateBasePath, "Battle", "lifeEmpty.png");
                
                if (!File.Exists(templatePath))
                {
                    log?.Invoke($"Không tìm thấy file template: {templatePath}");
                    return false;
                }

                // Tạo thư mục để lưu
                string saveDir = Path.Combine(Application.StartupPath, "Debug");
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }

                // Copy template để debug
                string debugTemplatePath = Path.Combine(saveDir, "lifeEmpty_debug.png");
                File.Copy(templatePath, debugTemplatePath, true);

                // Đọc template và lưu thông tin
                using (var tplMat = Cv2.ImRead(templatePath, ImreadModes.Color))
                {
                    if (!tplMat.Empty())
                    {
                        log?.Invoke($"🔍 Debug Template: Size={tplMat.Width}x{tplMat.Height}, Channels={tplMat.Channels()}");
                        
                        // Lưu template với thông tin
                        string infoPath = Path.Combine(saveDir, "template_info.txt");
                        string info = $"Template: lifeEmpty.png\n" +
                                    $"Size: {tplMat.Width}x{tplMat.Height}\n" +
                                    $"Channels: {tplMat.Channels()}\n" +
                                    $"Type: {tplMat.Type()}\n" +
                                    $"Depth: {tplMat.Depth()}\n" +
                                    $"Saved at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                        File.WriteAllText(infoPath, info);
                        
                        log?.Invoke($"✅ Đã lưu template debug: {debugTemplatePath}");
                        log?.Invoke($"📄 Thông tin template: {infoPath}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"❌ Lỗi khi lưu template debug: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Thông tin về round hiện tại
    /// </summary>
    public class RoundInfo
    {
        public bool IsRound1 { get; set; }
        public int Life1EmptyCount { get; set; }
        public int Life2EmptyCount { get; set; }
        public int TotalEmptyCount { get; set; }
        public int CalculatedRound { get; set; }

        public override string ToString()
        {
            return $"Round {CalculatedRound}";
        }
    }
} 