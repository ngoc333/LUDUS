using System.Drawing;

namespace LUDUS.Utils {
    public static class ImageCompare {
        public static bool AreSame(Bitmap a, Bitmap b, double threshold = 0.95) {
            if (a == null || b == null) return false;
            if (a.Width != b.Width || a.Height != b.Height) return false;

            long match = 0, total = (long)a.Width * a.Height;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y).ToArgb() == b.GetPixel(x, y).ToArgb())
                        match++;

            return (double)match / total >= threshold;
        }
    }
}
