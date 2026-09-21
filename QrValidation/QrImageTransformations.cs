using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ColorZXing.QrValidation;

public static class QrImageTransformations
{
    public static Bitmap Clone(Bitmap source) => new(source);

    public static Bitmap Rotate(Bitmap source, RotateFlipType rotation)
    {
        var result = new Bitmap(source);
        result.RotateFlip(rotation);
        return result;
    }

    /// <summary>Small shear/scale is intentionally affine: it models camera skew without synthetic projective math.</summary>
    public static Bitmap AffinePerspectiveLike(Bitmap source, float shear = 0.035f, float scale = 0.96f)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var matrix = new Matrix(scale, 0, shear, scale, source.Width * (1 - scale) / 2f, source.Height * (1 - scale) / 2f);
        g.Transform = matrix;
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return result;
    }

    public static Bitmap Gradient(Bitmap source, int amplitude = 70)
    {
        var result = Clone(source);
        Mutate(result, (pixels, width, height) =>
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var i = (y * width + x) * 4;
                    var delta = (x * amplitude / Math.Max(1, width - 1)) - amplitude / 2;
                    for (var c = 0; c < 3; c++) pixels[i + c] = Clamp(pixels[i + c] + delta);
                }
        });
        return result;
    }

    public static Bitmap Blur(Bitmap source, int radius = 1)
    {
        if (radius < 1) return Clone(source);
        var result = Clone(source);
        MutatePair(source, result, (input, output, width, height) =>
        {
            var side = radius * 2 + 1;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var count = 0; var b = 0; var g = 0; var r = 0;
                for (var oy = -radius; oy <= radius; oy++) for (var ox = -radius; ox <= radius; ox++)
                {
                    var sx = Math.Clamp(x + ox, 0, width - 1); var sy = Math.Clamp(y + oy, 0, height - 1);
                    var i = (sy * width + sx) * 4; b += input[i]; g += input[i + 1]; r += input[i + 2]; count++;
                }
                // Retain most of the source contrast: this is a camera-like softening fixture,
                // not a destructive convolution that erases narrow QR timing modules.
                var d = (y * width + x) * 4;
                output[d] = (byte)((b + input[d] * 3 * count) / (count * 4));
                output[d + 1] = (byte)((g + input[d + 1] * 3 * count) / (count * 4));
                output[d + 2] = (byte)((r + input[d + 2] * 3 * count) / (count * 4));
                output[d + 3] = 255;
            }
        });
        return result;
    }

    public static Bitmap Noise(Bitmap source, int amplitude = 12, int seed = 73)
    {
        var random = new Random(seed); var result = Clone(source);
        Mutate(result, (pixels, width, height) =>
        {
            for (var i = 0; i < pixels.Length; i += 4)
                for (var c = 0; c < 3; c++) pixels[i + c] = Clamp(pixels[i + c] + random.Next(-amplitude, amplitude + 1));
        });
        return result;
    }

    public static Bitmap JpegRoundTrip(Bitmap source, long quality = 82)
    {
        using var stream = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using (var parameters = new EncoderParameters(1))
        {
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            source.Save(stream, codec, parameters);
        }
        using var decoded = new Bitmap(stream);
        return new Bitmap(decoded);
    }

    public static Bitmap CorrectableCorruption(QrBaselineImage source, int modules = 8)
    {
        var result = Clone(source.Bitmap);
        var random = new Random(91);
        using var g = Graphics.FromImage(result);
        for (var i = 0; i < modules; i++)
        {
            var x = source.QuietZone + 1 + random.Next(Math.Max(1, source.Dimension - 2));
            var y = source.QuietZone + 1 + random.Next(Math.Max(1, source.Dimension - 2));
            using var brush = new SolidBrush(Color.White);
            g.FillRectangle(brush, x * source.ModuleSize, y * source.ModuleSize, source.ModuleSize, source.ModuleSize);
        }
        return result;
    }

    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);

    private static void Mutate(Bitmap bitmap, Action<byte[], int, int> action)
    {
        using var normalized = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(normalized)) g.DrawImageUnscaled(bitmap, 0, 0);
        var data = normalized.LockBits(new Rectangle(0, 0, normalized.Width, normalized.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * normalized.Height]; Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var packed = new byte[normalized.Width * normalized.Height * 4];
            for (var y = 0; y < normalized.Height; y++) Buffer.BlockCopy(bytes, y * data.Stride, packed, y * normalized.Width * 4, normalized.Width * 4);
            action(packed, normalized.Width, normalized.Height);
            for (var y = 0; y < normalized.Height; y++) Buffer.BlockCopy(packed, y * normalized.Width * 4, bytes, y * data.Stride, normalized.Width * 4);
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally { normalized.UnlockBits(data); }
        using var output = Graphics.FromImage(bitmap); output.DrawImageUnscaled(normalized, 0, 0);
    }

    private static void MutatePair(Bitmap input, Bitmap output, Action<byte[], byte[], int, int> action)
    {
        using var normalized = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(normalized)) g.DrawImageUnscaled(input, 0, 0);
        var source = ReadPixels(normalized); var target = new byte[source.Length]; action(source, target, input.Width, input.Height); WritePixels(output, target);
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try { var b = new byte[bitmap.Width * bitmap.Height * 4]; for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), b, y * bitmap.Width * 4, bitmap.Width * 4); return b; }
        finally { bitmap.UnlockBits(data); }
    }

    private static void WritePixels(Bitmap bitmap, byte[] pixels)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(pixels, y * bitmap.Width * 4, IntPtr.Add(data.Scan0, y * data.Stride), bitmap.Width * 4); }
        finally { bitmap.UnlockBits(data); }
    }
}
