using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ColorZXing;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using QrDetector = ZXing.QrCode.Internal.Detector;
using QrDecoder = ZXing.QrCode.Internal.Decoder;

namespace Bench;

internal static class Legacy
{
    internal static void GetRGBByteArrayFromBitmap(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var width = bitmap.Width;
        var height = bitmap.Height;
        var stride = bmd.Stride;
        var scan0 = bmd.Scan0;

        for (int y = 0; y < height; y++)
        {
            var row = IntPtr.Add(scan0, (y * stride));
            for (int x = 0; x < width; x++)
            {
                var imgIndex = IntPtr.Add(row, x * Constants.PixelSize);
                var index = (y * width + x) * Constants.Gray8PixelSize;

                blue[index] = Marshal.ReadByte(IntPtr.Add(imgIndex, 0));
                green[index] = Marshal.ReadByte(IntPtr.Add(imgIndex, 1));
                red[index] = Marshal.ReadByte(IntPtr.Add(imgIndex, 2));
            }
        }
        bitmap.UnlockBits(bmd);
    }

    internal static void GetGray8ByteArrayFromBitmap(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var width = bitmap.Width;
        var height = bitmap.Height;
        var stride = bmd.Stride;
        var scan0 = bmd.Scan0;

        for (int y = 0; y < height; y++)
        {
            var row = IntPtr.Add(scan0, (y * stride));
            for (int x = 0; x < width; x++)
            {
                var imgIndex = IntPtr.Add(row, x * Constants.PixelSize);
                var index = (y * width + x) * Constants.Gray8PixelSize;
                int grayScale = Utils.GetGrayScale(Marshal.ReadByte(IntPtr.Add(imgIndex, 0)), Marshal.ReadByte(IntPtr.Add(imgIndex, 1)), Marshal.ReadByte(IntPtr.Add(imgIndex, 2)));
                byte blackOrWhite = grayScale < 128 ? (byte)0 : (byte)255;
                bytedata[index] = blackOrWhite;
            }
        }
        bitmap.UnlockBits(bmd);
    }
}


internal static class Program
{
    private const int Iterations = 30;
    private const int W = 400, H = 400;
    private static readonly byte[] bufA = new byte[W * H * 4];
    private static readonly byte[] bufB = new byte[W * H * 4];
    private static readonly byte[] bufC = new byte[W * H * 4];
    private static readonly byte[] bufD = new byte[W * H];

    private static void Main()
    {
        var payloadLength = int.TryParse(Environment.GetEnvironmentVariable("COLORZXING_BENCH_LENGTH"), out var configuredLength)
            ? configuredLength
            : 200;
        using var bitmap = ColorZXingRGB.EncodeLegacy(TestString(payloadLength), W, H, 0);

        time("RGB v0 legacy (Marshal.ReadByte x3)", () => Legacy.GetRGBByteArrayFromBitmap(bitmap, bufA, bufB, bufC));
        time("RGB v1 row-copy  (current PR-style) ", () => RowCopyRGB(bitmap, bufA, bufB, bufC));
        time("RGB v2 unsafe pointers              ", () => UnsafeRGB(bitmap, bufA, bufB, bufC));
        time("RGB v3 SIMD Ssse3 shuffle           ", () => SimdRGB(bitmap, bufA, bufB, bufC));

        time("Gray v0 legacy                      ", () => Legacy.GetGray8ByteArrayFromBitmap(bitmap, bufD));
        time("Gray v1 row-copy (current)          ", () => RowCopyGray(bitmap, bufD));
        time("Gray v2 unsafe pointers             ", () => UnsafeGray(bitmap, bufD));
        time("Gray v3 SIMD Ssse3 shuffle          ", () => SimdGray(bitmap, bufD));

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) ColorZXingRGB.DecodeLegacy(bitmap);
        sw.Stop();
        Console.WriteLine($"\nEnd-to-end ColorZXingRGB.Decode 400x400 (library as-is): {sw.ElapsedMilliseconds / (double)Iterations:F2} ms/op");

        var expected = TestString(payloadLength);
        benchmarkDecode("Decode v0 current (3x MultiFormat + detect)", bitmap, expected, DecodeCurrent);
        benchmarkDecode("Decode v1 QR-only serial               ", bitmap, expected, DecodeQrSerial);
        benchmarkDecode("Decode v2 QR-only parallel             ", bitmap, expected, DecodeQrParallel);
        benchmarkDecode("Decode v3 shared detection/geometry    ", bitmap, expected, DecodeSharedGeometry);
        benchmarkDecode("Decode v4 pure QR serial (clean images)", bitmap, expected, DecodePureSerial);
        benchmarkDecode("Decode v5 one-pass module sampler      ", bitmap, expected, DecodeOnePassModules);
        benchmarkDecode("Decode v6 optimized library + fallback", bitmap, expected, ColorZXingRGB.Decode);

        benchmarkEncode("Encode v0 current (3 full raster buffers)", expected, value => ColorZXingRGB.EncodeLegacy(value, W, H, 0));
        benchmarkEncode("Encode v1 prototype module renderer    ", expected, value => EncodeOnePass(value, W, H, 0));
        benchmarkEncode("Encode v2 optimized library            ", expected, value => ColorZXingRGB.Encode(value, W, H, 0));
    }

    private static void benchmarkEncode(string name, string value, Func<string, Bitmap> encode)
    {
        using (var check = encode(value))
        {
            if (ColorZXingRGB.Decode(check) != value)
                throw new InvalidOperationException($"{name} did not round-trip");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            using var encoded = encode(value);
        }
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        Console.WriteLine($"{name}: {sw.Elapsed.TotalMilliseconds / Iterations,8:F3} ms/op, {allocated / Iterations / 1024.0,8:F1} KiB/op");
    }

    private static Bitmap EncodeOnePass(string value, int width, int height, int margin)
    {
        var partLength = value.Length / 3;
        var parts = new[]
        {
            value.Substring(0, partLength),
            value.Substring(partLength, partLength),
            value.Substring(partLength * 2)
        };

        var codes = new ZXing.QrCode.Internal.QRCode[3];
        var version = 1;
        Parallel.For(0, codes.Length, i =>
        {
            codes[i] = ZXing.QrCode.Internal.Encoder.encode(parts[i], ErrorCorrectionLevel.L);
        });
        version = codes.Max(code => code.Version.VersionNumber);
        Parallel.For(0, codes.Length, i =>
        {
            if (codes[i].Version.VersionNumber == version)
                return;
            var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.QR_VERSION] = version };
            codes[i] = ZXing.QrCode.Internal.Encoder.encode(parts[i], ErrorCorrectionLevel.L, hints);
        });

        var dimension = codes[0].Matrix.Width;
        var qrDimension = dimension + margin * 2;
        var outputWidth = Math.Max(width, qrDimension);
        var outputHeight = Math.Max(height, qrDimension);
        var multiple = Math.Min(outputWidth / qrDimension, outputHeight / qrDimension);
        var left = (outputWidth - dimension * multiple) / 2;
        var top = (outputHeight - dimension * multiple) / 2;
        var result = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppRgb);
        var data = result.LockBits(new Rectangle(0, 0, outputWidth, outputHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            unsafe
            {
                var scan0 = (byte*)data.Scan0;
                for (int y = 0; y < outputHeight; y++)
                {
                    var row = scan0 + y * data.Stride;
                    var moduleY = (y - top) / multiple;
                    var insideY = y >= top && moduleY < dimension;
                    for (int x = 0; x < outputWidth; x++)
                    {
                        var pixel = row + x * 4;
                        var moduleX = (x - left) / multiple;
                        var inside = insideY && x >= left && moduleX < dimension;
                        pixel[0] = inside && codes[0].Matrix[moduleX, moduleY] == 1 ? (byte)0 : (byte)255;
                        pixel[1] = inside && codes[1].Matrix[moduleX, moduleY] == 1 ? (byte)0 : (byte)255;
                        pixel[2] = inside && codes[2].Matrix[moduleX, moduleY] == 1 ? (byte)0 : (byte)255;
                    }
                }
            }
        }
        finally
        {
            result.UnlockBits(data);
        }
        return result;
    }

    private static void benchmarkDecode(string name, Bitmap bitmap, string expected, Func<Bitmap, string> decode)
    {
        var actual = decode(bitmap);
        if (actual != expected)
            throw new InvalidOperationException($"{name} returned an incorrect result");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            actual = decode(bitmap);
        }
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        Console.WriteLine($"{name}: {sw.Elapsed.TotalMilliseconds / Iterations,8:F3} ms/op, {allocated / Iterations / 1024.0,8:F1} KiB/op");
    }

    private static string DecodeCurrent(Bitmap bitmap) => ColorZXingRGB.DecodeLegacy(bitmap);

    private static string DecodeQrSerial(Bitmap bitmap)
    {
        var planes = ExtractPlanes(bitmap);
        return DecodePlane(planes[0], bitmap.Width, bitmap.Height, false)
             + DecodePlane(planes[1], bitmap.Width, bitmap.Height, false)
             + DecodePlane(planes[2], bitmap.Width, bitmap.Height, false);
    }

    private static string DecodeQrParallel(Bitmap bitmap)
    {
        var planes = ExtractPlanes(bitmap);
        var results = new string[3];
        Parallel.Invoke(
            () => results[0] = DecodePlane(planes[0], bitmap.Width, bitmap.Height, false),
            () => results[1] = DecodePlane(planes[1], bitmap.Width, bitmap.Height, false),
            () => results[2] = DecodePlane(planes[2], bitmap.Width, bitmap.Height, false));
        return string.Concat(results);
    }

    private static string DecodePureSerial(Bitmap bitmap)
    {
        var planes = ExtractPlanes(bitmap);
        return DecodePlane(planes[0], bitmap.Width, bitmap.Height, true)
             + DecodePlane(planes[1], bitmap.Width, bitmap.Height, true)
             + DecodePlane(planes[2], bitmap.Width, bitmap.Height, true);
    }

    private static string DecodePlane(byte[] plane, int width, int height, bool pure)
    {
        var source = new RGBLuminanceSource(plane, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        var binary = new BinaryBitmap(new HybridBinarizer(source));
        IDictionary<DecodeHintType, object> hints = pure
            ? new Dictionary<DecodeHintType, object> { [DecodeHintType.PURE_BARCODE] = true }
            : null;
        return new QRCodeReader().decode(binary, hints)?.Text ?? string.Empty;
    }

    private static string DecodeSharedGeometry(Bitmap bitmap)
    {
        var planes = ExtractPlanes(bitmap);
        var matrices = new BitMatrix[3];
        for (int i = 0; i < matrices.Length; i++)
        {
            var source = new RGBLuminanceSource(planes[i], bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8);
            matrices[i] = new BinaryBitmap(new HybridBinarizer(source)).BlackMatrix;
        }

        var detected = new QrDetector(matrices[0]).detect();
        if (detected == null)
            return string.Empty;

        var transform = RecreateTransform(detected.Points, detected.Bits.Width);
        var sampled = new[]
        {
            detected.Bits,
            GridSampler.Instance.sampleGrid(matrices[1], detected.Bits.Width, detected.Bits.Height, transform),
            GridSampler.Instance.sampleGrid(matrices[2], detected.Bits.Width, detected.Bits.Height, transform)
        };

        var results = new string[3];
        for (int i = 0; i < sampled.Length; i++)
            results[i] = new QrDecoder().decode(sampled[i], null)?.Text ?? string.Empty;
        return string.Concat(results);
    }

    private static string DecodeOnePassModules(Bitmap bitmap)
    {
        var planes = ExtractPlanes(bitmap);
        var detectorSource = new RGBLuminanceSource(planes[0], bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8);
        var detectorMatrix = new BinaryBitmap(new HybridBinarizer(detectorSource)).BlackMatrix;
        var detected = new QrDetector(detectorMatrix).detect();
        if (detected == null)
            return string.Empty;

        var dimension = detected.Bits.Width;
        var transform = RecreateTransform(detected.Points, dimension);
        var samples = new[] { new byte[dimension * dimension], new byte[dimension * dimension], new byte[dimension * dimension] };
        var points = new float[dimension * 2];

        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                points[x * 2] = x + 0.5f;
                points[x * 2 + 1] = y + 0.5f;
            }
            transform.transformPoints(points);
            for (int x = 0; x < dimension; x++)
            {
                var imageX = Math.Clamp((int)points[x * 2], 0, bitmap.Width - 1);
                var imageY = Math.Clamp((int)points[x * 2 + 1], 0, bitmap.Height - 1);
                var sourceIndex = imageY * bitmap.Width + imageX;
                var targetIndex = y * dimension + x;
                samples[0][targetIndex] = planes[0][sourceIndex];
                samples[1][targetIndex] = planes[1][sourceIndex];
                samples[2][targetIndex] = planes[2][sourceIndex];
            }
        }

        var matrices = new BitMatrix[3];
        for (int channel = 0; channel < matrices.Length; channel++)
        {
            var threshold = OtsuThreshold(samples[channel]);
            var matrix = new BitMatrix(dimension);
            for (int y = 0; y < dimension; y++)
                for (int x = 0; x < dimension; x++)
                    if (samples[channel][y * dimension + x] <= threshold)
                        matrix[x, y] = true;
            matrices[channel] = matrix;
        }

        var results = new string[3];
        Parallel.Invoke(
            () => results[0] = new QrDecoder().decode(matrices[0], null)?.Text ?? string.Empty,
            () => results[1] = new QrDecoder().decode(matrices[1], null)?.Text ?? string.Empty,
            () => results[2] = new QrDecoder().decode(matrices[2], null)?.Text ?? string.Empty);
        return string.Concat(results);
    }

    private static int OtsuThreshold(byte[] values)
    {
        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        long sum = 0;
        foreach (var value in values)
        {
            histogram[value]++;
            sum += value;
        }

        long backgroundSum = 0;
        int backgroundCount = 0;
        double bestVariance = -1;
        int bestThreshold = 127;
        for (int threshold = 0; threshold < histogram.Length; threshold++)
        {
            backgroundCount += histogram[threshold];
            if (backgroundCount == 0)
                continue;
            var foregroundCount = values.Length - backgroundCount;
            if (foregroundCount == 0)
                break;
            backgroundSum += (long)threshold * histogram[threshold];
            var backgroundMean = (double)backgroundSum / backgroundCount;
            var foregroundMean = (double)(sum - backgroundSum) / foregroundCount;
            var difference = backgroundMean - foregroundMean;
            var variance = (double)backgroundCount * foregroundCount * difference * difference;
            if (variance > bestVariance)
            {
                bestVariance = variance;
                bestThreshold = threshold;
            }
        }
        return bestThreshold;
    }

    private static PerspectiveTransform RecreateTransform(ResultPoint[] points, int dimension)
    {
        var bottomLeft = points[0];
        var topLeft = points[1];
        var topRight = points[2];
        var dimMinusThree = dimension - 3.5f;
        float bottomRightX;
        float bottomRightY;
        float sourceBottomRight;

        if (points.Length > 3)
        {
            bottomRightX = points[3].X;
            bottomRightY = points[3].Y;
            sourceBottomRight = dimMinusThree - 3.0f;
        }
        else
        {
            bottomRightX = topRight.X - topLeft.X + bottomLeft.X;
            bottomRightY = topRight.Y - topLeft.Y + bottomLeft.Y;
            sourceBottomRight = dimMinusThree;
        }

        return PerspectiveTransform.quadrilateralToQuadrilateral(
            3.5f, 3.5f,
            dimMinusThree, 3.5f,
            sourceBottomRight, sourceBottomRight,
            3.5f, dimMinusThree,
            topLeft.X, topLeft.Y,
            topRight.X, topRight.Y,
            bottomRightX, bottomRightY,
            bottomLeft.X, bottomLeft.Y);
    }

    private static byte[][] ExtractPlanes(Bitmap bitmap)
    {
        var length = checked(bitmap.Width * bitmap.Height);
        var planes = new[] { new byte[length], new byte[length], new byte[length] };
        UnsafeRGB(bitmap, planes[0], planes[1], planes[2]);
        return planes;
    }

    private static void time(string name, Action act)
    {
        act();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) act();
        Console.WriteLine($"{name} : {sw.Elapsed.TotalMilliseconds / Iterations:F3} ms/op");
    }

    private static string TestString(int len)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < len; i++) sb.Append((char)('A' + i % 26));
        return sb.ToString();
    }

    private static void RowCopyRGB(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride; var scan0 = bmd.Scan0;
            var rowSize = width * 4; var rowBuffer = new byte[rowSize];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(scan0, y * stride), rowBuffer, 0, rowSize);
                var outputOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    var source = x * 4;
                    var target = outputOffset + x;
                    blue[target] = rowBuffer[source];
                    green[target] = rowBuffer[source + 1];
                    red[target] = rowBuffer[source + 2];
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static void UnsafeRGB(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride;
            unsafe
            {
                var src = (byte*)bmd.Scan0;
                fixed (byte* pb = blue, pg = green, pr = red)
                {
                    var db = pb; var dg = pg; var dr = pr;
                    for (int y = 0; y < height; y++)
                    {
                        var row = src + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            var q = row + x * 4;
                            *db++ = q[0];
                            *dg++ = q[1];
                            *dr++ = q[2];
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static unsafe void SimdRGB(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride;
            unsafe
            {
                var src = (byte*)bmd.Scan0;
                fixed (byte* pb = blue, pg = green, pr = red)
                {
                    if (System.Runtime.Intrinsics.X86.Ssse3.IsSupported)
                    {
                        var maskB = Vector128.Create((byte)0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12);
                        var maskG = Vector128.Create((byte)1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13);
                        var maskR = Vector128.Create((byte)2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14);
                        var db = pb; var dg = pg; var dr = pr;
                        var simdPixels = width & ~3;
                        for (int y = 0; y < height; y++)
                        {
                            var row = src + y * stride;
                            for (int x = 0; x < simdPixels; x += 4)
                            {
                                var v = Sse2.LoadVector128(row + x * 4);
                                var lo = Ssse3.Shuffle(v, maskB);
                                var hi = Ssse3.Shuffle(v, maskG);
                                var rr = Ssse3.Shuffle(v, maskR);
                                Write4(db, lo); Write4(dg, hi); Write4(dr, rr);
                                db += 4; dg += 4; dr += 4;
                            }
                            for (int x = simdPixels; x < width; x++)
                            {
                                var q = row + x * 4;
                                *db++ = q[0]; *dg++ = q[1]; *dr++ = q[2];
                            }
                        }
                    }
                    else
                    {
                        var db = pb; var dg = pg; var dr = pr;
                        for (int y = 0; y < height; y++)
                        {
                            var row = src + y * stride;
                            for (int x = 0; x < width; x++)
                            {
                                var q = row + x * 4;
                                *db++ = q[0]; *dg++ = q[1]; *dr++ = q[2];
                            }
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Write4(byte* d, System.Runtime.Intrinsics.Vector128<byte> v)
    {
        ulong lo = System.Runtime.Intrinsics.Vector128.AsUInt64(v).ToScalar();
        *(uint*)d = (uint)lo;
    }

    private static void RowCopyGray(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride; var scan0 = bmd.Scan0;
            var rowSize = width * 4; var rowBuffer = new byte[rowSize];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(scan0, y * stride), rowBuffer, 0, rowSize);
                var outputOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    var s = x * 4;
                    var grayScale = (rowBuffer[s] + rowBuffer[s + 1] + rowBuffer[s + 2]) / 3;
                    bytedata[outputOffset + x] = grayScale < 128 ? (byte)0 : (byte)255;
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static void UnsafeGray(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride;
            unsafe
            {
                var src = (byte*)bmd.Scan0;
                fixed (byte* pd = bytedata)
                {
                    var d = pd;
                    for (int y = 0; y < height; y++)
                    {
                        var row = src + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            var q = row + x * 4;
                            var grayScale = (*q + q[1] + q[2]) / 3;
                            *d++ = grayScale < 128 ? (byte)0 : (byte)255;
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static unsafe void SimdGray(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride;
            unsafe
            {
                var src = (byte*)bmd.Scan0;
                fixed (byte* pd = bytedata)
                {
                    var d = pd;
                    for (int y = 0; y < height; y++)
                    {
                        var row = src + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            var q = row + x * 4;
                            var grayScale = (*q + q[1] + q[2]) / 3;
                            *d++ = grayScale < 128 ? (byte)0 : (byte)255;
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }
}
