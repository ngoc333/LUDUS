// Services/HeroNameOcrService.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Tesseract;

namespace LUDUS.Services {
    public class HeroNameOcrService : IDisposable {
        private readonly TesseractEngine _engine;

        public HeroNameOcrService() {
            _engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
            _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
        }

        public string Recognize(Bitmap src) {
            // 1) Grayscale via ColorMatrix
            var gray = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(gray))
            using (var imgAttr = new ImageAttributes()) {
                var cm = new ColorMatrix(new[]
                {
            new float[] {0.299f, 0.299f, 0.299f, 0, 0},
            new float[] {0.587f, 0.587f, 0.587f, 0, 0},
            new float[] {0.114f, 0.114f, 0.114f, 0, 0},
            new float[] {     0,      0,      0, 1, 0},
            new float[] {     0,      0,      0, 0, 1},
        });
                imgAttr.SetColorMatrix(cm);
                g.DrawImage(src,
                    new Rectangle(0, 0, gray.Width, gray.Height),
                    0, 0, src.Width, src.Height,
                    GraphicsUnit.Pixel,
                    imgAttr
                );
            }

            // 2) Threshold → binary in 24bpp so SetPixel works
            var bin = new Bitmap(gray.Width, gray.Height, PixelFormat.Format24bppRgb);
            for (int y = 0; y < gray.Height; y++) {
                for (int x = 0; x < gray.Width; x++) {
                    int l = gray.GetPixel(x, y).R;  // already grayscale
                    bin.SetPixel(x, y, l > 200 ? Color.White : Color.Black);
                }
            }
            gray.Dispose();

            // 3) Invert (so text is black on white)
            for (int y = 0; y < bin.Height; y++)
                for (int x = 0; x < bin.Width; x++) {
                    var c = bin.GetPixel(x, y);
                    bin.SetPixel(x, y,
                        c.R == 255 ? Color.Black : Color.White);
                }

            // 4) Scale up 2× in 24bpp
            var scaled = new Bitmap(bin.Width * 2, bin.Height * 2, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(scaled)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bin, 0, 0, scaled.Width, scaled.Height);
            }
            bin.Dispose();

            // 5) OCR
            // Convert bitmap to byte array and then to Pix
            using (var ms = new MemoryStream()) {
                scaled.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();
                
                using (var pix = Pix.LoadFromMemory(imageBytes))
                using (var page = _engine.Process(pix, PageSegMode.SingleLine)) {
                    string txt = page.GetText().Trim();
                    scaled.Dispose();
                    return txt;
                }
            }
        }


        public void Dispose() {
            _engine?.Dispose();
        }
    }
}
