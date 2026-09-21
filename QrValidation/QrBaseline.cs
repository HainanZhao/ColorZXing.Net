using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using QrDecoder = ZXing.QrCode.Internal.Decoder;
using QrDetector = ZXing.QrCode.Internal.Detector;
using QrEncoder = ZXing.QrCode.Internal.Encoder;

namespace ColorZXing.QrValidation;

/// <summary>Only ZXing.Net 0.16.11 is used for the correctness reference.</summary>
public static class QrBaseline
{
    public static QrBaselineImage Encode(QrCase qrCase, int moduleSize = 5, int quietZone = 4)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.QR_VERSION] = qrCase.Version,
            [EncodeHintType.CHARACTER_SET] = qrCase.CharacterSet,
            [EncodeHintType.ERROR_CORRECTION] = qrCase.ErrorCorrection,
        };
        if (qrCase.Mask is int mask)
            hints[EncodeHintType.QR_MASK_PATTERN] = mask;

        var code = QrEncoder.encode(qrCase.Payload, qrCase.ErrorCorrection, hints);
        var matrix = code.Matrix;
        var actualVersion = (matrix.Width - 17) / 4;
        if (actualVersion != qrCase.Version)
            throw new InvalidOperationException($"ZXing produced version {actualVersion} for {qrCase.Name}, expected {qrCase.Version}.");

        var size = checked((matrix.Width + quietZone * 2) * moduleSize);
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (var y = 0; y < matrix.Height; y++)
                for (var x = 0; x < matrix.Width; x++)
                    if (matrix[x, y] == 1)
                        graphics.FillRectangle(Brushes.Black,
                            (quietZone + x) * moduleSize, (quietZone + y) * moduleSize, moduleSize, moduleSize);
        }
        return new QrBaselineImage(bitmap, moduleSize, quietZone, matrix.Width, qrCase);
    }

    public static string Decode(Bitmap bitmap, bool tryRotations = false)
    {
        if (!tryRotations)
            return DecodeOne(bitmap);
        foreach (var rotation in new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.Rotate90FlipNone,
                     RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipNone })
        {
            using var candidate = new Bitmap(bitmap);
            candidate.RotateFlip(rotation);
            var result = DecodeOne(candidate);
            if (!string.IsNullOrEmpty(result))
                return result;
        }
        return string.Empty;
    }

    public static byte[] Preprocess(Bitmap bitmap)
    {
        using var normalized = Normalize(bitmap);
        var data = normalized.LockBits(new Rectangle(0, 0, normalized.Width, normalized.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var result = new byte[normalized.Width * normalized.Height];
            var row = new byte[Math.Abs(data.Stride)];
            for (var y = 0; y < normalized.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                for (var x = 0; x < normalized.Width; x++)
                {
                    var i = x * 4;
                    result[y * normalized.Width + x] = (byte)((row[i] * 299 + row[i + 1] * 587 + row[i + 2] * 114) / 1000);
                }
            }
            return result;
        }
        finally { normalized.UnlockBits(data); }
    }

    public static BitMatrix Binarize(byte[] luminance, int width, int height)
    {
        var source = new ByteArrayLuminanceSource(luminance, width, height);
        return new BinaryBitmap(new HybridBinarizer(source)).BlackMatrix;
    }

    public static BitMatrix DetectAndSample(BitMatrix binary)
        => new QrDetector(binary).detect()?.Bits
           ?? throw new InvalidOperationException("QR detector found no symbol.");

    public static string DecodeSampled(BitMatrix sampled)
        => new QrDecoder().decode(sampled, null)?.Text ?? string.Empty;

    private static string DecodeOne(Bitmap bitmap)
    {
        try
        {
            var luminance = Preprocess(bitmap);
            var source = new ByteArrayLuminanceSource(luminance, bitmap.Width, bitmap.Height);
            var binary = new BinaryBitmap(new HybridBinarizer(source));
            var hints = new Dictionary<DecodeHintType, object> { [DecodeHintType.TRY_HARDER] = true };
            return new QRCodeReader().decode(binary, hints)?.Text ?? string.Empty;
        }
        catch (ReaderException) { return string.Empty; }
        catch (ArgumentException) { return string.Empty; }
    }

    private static Bitmap Normalize(Bitmap source)
    {
        var normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppRgb);
        using var graphics = Graphics.FromImage(normalized);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(source, 0, 0);
        return normalized;
    }

    private sealed class ByteArrayLuminanceSource : LuminanceSource
    {
        private readonly byte[] _values;
        public ByteArrayLuminanceSource(byte[] values, int width, int height) : base(width, height) => _values = values;
        public override byte[] Matrix => _values;
        public override byte[] getRow(int y, byte[] row)
        {
            if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
            if (row is null || row.Length < Width) row = new byte[Width];
            Buffer.BlockCopy(_values, y * Width, row, 0, Width);
            return row;
        }
    }
}
