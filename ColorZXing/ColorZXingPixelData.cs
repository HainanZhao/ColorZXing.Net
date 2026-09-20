using System;

namespace ColorZXing
{
    /// <summary>
    /// A platform-neutral RGBA image for browser, canvas, and non-System.Drawing callers.
    /// Pixels are stored row-major with four bytes per pixel in red, green, blue, alpha order.
    /// </summary>
    public sealed class ColorZXingPixelData
    {
        public ColorZXingPixelData(byte[] pixels, int width, int height)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels.Length != checked(width * height * 4))
                throw new ArgumentException("RGBA data must contain exactly four bytes per pixel.", nameof(pixels));

            Pixels = pixels;
            Width = width;
            Height = height;
        }

        public byte[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }
    }
}
