using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using static ZXing.RGBLuminanceSource;
using QrDecoder = ZXing.QrCode.Internal.Decoder;
using QrDetector = ZXing.QrCode.Internal.Detector;
using QrEncoder = ZXing.QrCode.Internal.Encoder;

namespace ColorZXing
{    
    public class ColorZXingRGB
    {        
        ///Use the IntPtr method instead of bitmap.GetPixel, it's 6x faster.
        private static void SetBitmap(Bitmap bitmap, byte[] red, byte[] green, byte[] blue)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var stride = bmd.Stride;
                unsafe
                {
                    var dst = (byte*)bmd.Scan0;
                    fixed (byte* pr = red, pg = green, pb = blue)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var row = dst + y * stride;
                            for (int x = 0; x < width; x++)
                            {
                                var index = (y * width + x) * Constants.PixelSize;
                                var q = row + x * Constants.PixelSize;
                                q[0] = pr[index];
                                q[1] = pg[index + 1];
                                q[2] = pb[index + 2];
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmd);
            }
        }

        ///Use the IntPtr method instead of bitmap.SetPixel, it's 2x faster.
        private static void GetRGBByteArrayFromBitmap(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var stride = bmd.Stride;
                unsafe
                {
                    var src = (byte*)bmd.Scan0;
                    fixed (byte* pb = blue, pg = green, pr = red)
                    {
                        var db = pb;
                        var dg = pg;
                        var dr = pr;
                        if (Ssse3.IsSupported)
                        {
                            var maskB = Vector128.Create((byte)0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12);
                            var maskG = Vector128.Create((byte)1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13);
                            var maskR = Vector128.Create((byte)2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14);
                            var simdPixels = width & ~3;
                            for (int y = 0; y < height; y++)
                            {
                                var row = src + y * stride;
                                for (int x = 0; x < simdPixels; x += 4)
                                {
                                    var v = Sse2.LoadVector128(row + x * Constants.PixelSize);
                                    Write4(ref db, Ssse3.Shuffle(v, maskB));
                                    Write4(ref dg, Ssse3.Shuffle(v, maskG));
                                    Write4(ref dr, Ssse3.Shuffle(v, maskR));
                                }
                                for (int x = simdPixels; x < width; x++)
                                {
                                    var q = row + x * Constants.PixelSize;
                                    *db++ = q[0];
                                    *dg++ = q[1];
                                    *dr++ = q[2];
                                }
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                var row = src + y * stride;
                                for (int x = 0; x < width; x++)
                                {
                                    var q = row + x * Constants.PixelSize;
                                    *db++ = q[0];
                                    *dg++ = q[1];
                                    *dr++ = q[2];
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmd);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Write4(ref byte* d, Vector128<byte> v)
        {
            *(uint*)d = (uint)Vector128.AsUInt64(v).GetElement(0);
            d += 4;
        }

        public static Bitmap Encode(string value, int width, int height, int margin)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var parts = SplitUtf8(value);
            var codes = new QRCode[3];
            Parallel.For(0, codes.Length, i =>
            {
                codes[i] = QrEncoder.encode(parts[i], ErrorCorrectionLevel.L, Utf8Hints());
            });

            // All colors must use the same module geometry so finder, timing,
            // alignment, and data-module centers coincide.
            var version = codes.Max(code => code.Version.VersionNumber);
            Parallel.For(0, codes.Length, i =>
            {
                if (codes[i].Version.VersionNumber == version)
                    return;
                var hints = Utf8Hints();
                hints[EncodeHintType.QR_VERSION] = version;
                codes[i] = QrEncoder.encode(parts[i], ErrorCorrectionLevel.L, hints);
            });

            return RenderCombined(codes, width, height, margin);
        }

        internal static Bitmap EncodeLegacy(string value, int width, int height, int margin)
        {

            int subStringSize = value.Length / 3;
            var str1 = value.Substring(0, subStringSize);
            var str2 = value.Substring(subStringSize, subStringSize);
            var str3 = value.Substring(subStringSize * 2, value.Length - subStringSize * 2);

            var qrCodeWriter = new ZXing.BarcodeWriterPixelData
            {

                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = margin
                }
            };

            var red = qrCodeWriter.Write(str1);
            var green = qrCodeWriter.Write(str2);
            var blue = qrCodeWriter.Write(str3);

            var pixelWidth = blue.Width;
            var pixelHeight = blue.Height;

            var bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppRgb);

            SetBitmap(bitmap, red.Pixels, green.Pixels, blue.Pixels);

            return bitmap;
        }
        
        public static string Decode(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            return TryDecodeShared(bitmap) ?? DecodeLegacy(bitmap);
        }

        internal static string DecodeLegacy(Bitmap bitmap)
        {
            var byteSize = bitmap.Width * bitmap.Height * Constants.Gray8PixelSize;

            byte[] blue = new byte[byteSize];
            byte[] green = new byte[byteSize];
            byte[] red = new byte[byteSize];

            GetRGBByteArrayFromBitmap(bitmap, blue, green, red);
            var str1 = ColorZXingBasic.Decode(blue, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);
            var str2 = ColorZXingBasic.Decode(green, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);
            var str3 = ColorZXingBasic.Decode(red, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);

            return str1 + str2 + str3;
        }

        private static IDictionary<EncodeHintType, object> Utf8Hints()
        {
            return new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.CHARACTER_SET] = Encoding.UTF8.WebName
            };
        }

        private static string[] SplitUtf8(string value)
        {
            var runes = value.EnumerateRunes().ToArray();
            if (runes.Length < 3)
                throw new ArgumentException("RGB encoding requires at least three Unicode scalar values.", nameof(value));

            var parts = new string[3];
            var runeIndex = 0;
            var bytesRemaining = runes.Sum(rune => rune.Utf8SequenceLength);
            for (int channel = 0; channel < 2; channel++)
            {
                var channelsRemaining = 3 - channel;
                var targetBytes = (bytesRemaining + channelsRemaining - 1) / channelsRemaining;
                var builder = new StringBuilder();
                var bytes = 0;
                var runesThatMustRemain = 2 - channel;
                while (runeIndex < runes.Length - runesThatMustRemain)
                {
                    var rune = runes[runeIndex];
                    if (builder.Length > 0 && bytes >= targetBytes)
                        break;
                    builder.Append(rune.ToString());
                    bytes += rune.Utf8SequenceLength;
                    runeIndex++;
                }
                parts[channel] = builder.ToString();
                bytesRemaining -= bytes;
            }

            var final = new StringBuilder();
            while (runeIndex < runes.Length)
                final.Append(runes[runeIndex++].ToString());
            parts[2] = final.ToString();
            return parts;
        }

        private static Bitmap RenderCombined(QRCode[] codes, int width, int height, int margin)
        {
            if (width < 0 || height < 0)
                throw new ArgumentException($"Requested dimensions are too small: {width}x{height}");
            if (margin < 0)
                throw new ArgumentOutOfRangeException(nameof(margin));

            var dimension = codes[0].Matrix.Width;
            var qrDimension = checked(dimension + margin * 2);
            var outputWidth = Math.Max(width, qrDimension);
            var outputHeight = Math.Max(height, qrDimension);
            var multiple = Math.Min(outputWidth / qrDimension, outputHeight / qrDimension);
            var left = (outputWidth - dimension * multiple) / 2;
            var top = (outputHeight - dimension * multiple) / 2;
            var bitmap = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppRgb);
            try
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, outputWidth, outputHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
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
                                var pixel = row + x * Constants.PixelSize;
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
                    bitmap.UnlockBits(data);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        internal static string TryDecodeShared(Bitmap bitmap)
        {
            try
            {
                var length = checked(bitmap.Width * bitmap.Height);
                var planes = new[] { new byte[length], new byte[length], new byte[length] };
                GetRGBByteArrayFromBitmap(bitmap, planes[0], planes[1], planes[2]);

                var source = new PlaneLuminanceSource(planes[0], bitmap.Width, bitmap.Height);
                var detectorMatrix = new BinaryBitmap(new HybridBinarizer(source)).BlackMatrix;
                var detected = new QrDetector(detectorMatrix).detect();
                if (detected == null)
                    return null;

                var dimension = detected.Bits.Width;
                var transform = RecreateTransform(detected.Points, dimension);
                var samples = SampleAllChannels(planes, bitmap.Width, bitmap.Height, transform, dimension);
                if (samples == null)
                    return null;

                var matrices = new BitMatrix[3];
                for (int channel = 0; channel < matrices.Length; channel++)
                    matrices[channel] = ThresholdModules(samples[channel], dimension);

                var results = new string[3];
                Parallel.Invoke(
                    () => results[0] = new QrDecoder().decode(matrices[0], null)?.Text,
                    () => results[1] = new QrDecoder().decode(matrices[1], null)?.Text,
                    () => results[2] = new QrDecoder().decode(matrices[2], null)?.Text);
                return results.All(result => result != null) ? string.Concat(results) : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static byte[][] SampleAllChannels(byte[][] planes, int width, int height, PerspectiveTransform transform, int dimension)
        {
            var samples = new[]
            {
                new byte[dimension * dimension],
                new byte[dimension * dimension],
                new byte[dimension * dimension]
            };
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
                    var imageX = (int)points[x * 2];
                    var imageY = (int)points[x * 2 + 1];
                    if ((uint)imageX >= (uint)width || (uint)imageY >= (uint)height)
                        return null;
                    var sourceIndex = imageY * width + imageX;
                    var targetIndex = y * dimension + x;
                    samples[0][targetIndex] = planes[0][sourceIndex];
                    samples[1][targetIndex] = planes[1][sourceIndex];
                    samples[2][targetIndex] = planes[2][sourceIndex];
                }
            }
            return samples;
        }

        private static BitMatrix ThresholdModules(byte[] samples, int dimension)
        {
            var threshold = OtsuThreshold(samples);
            var matrix = new BitMatrix(dimension);
            for (int y = 0; y < dimension; y++)
                for (int x = 0; x < dimension; x++)
                    if (samples[y * dimension + x] <= threshold)
                        matrix[x, y] = true;
            return matrix;
        }

        private static int OtsuThreshold(byte[] values)
        {
            Span<int> histogram = stackalloc int[256];
            histogram.Clear();
            long totalSum = 0;
            foreach (var value in values)
            {
                histogram[value]++;
                totalSum += value;
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
                var foregroundMean = (double)(totalSum - backgroundSum) / foregroundCount;
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

        private sealed class PlaneLuminanceSource : LuminanceSource
        {
            private readonly byte[] values;

            public PlaneLuminanceSource(byte[] values, int width, int height)
                : base(width, height)
            {
                this.values = values;
            }

            public override byte[] Matrix => values;

            public override byte[] getRow(int y, byte[] row)
            {
                if ((uint)y >= (uint)Height)
                    throw new ArgumentOutOfRangeException(nameof(y));
                if (row == null || row.Length < Width)
                    row = new byte[Width];
                Buffer.BlockCopy(values, y * Width, row, 0, Width);
                return row;
            }
        }

        public static string Decode(byte[] bytes)
        {
            using var bitmap = Utils.CreateBitmap(bytes);
            return Decode(bitmap);
        }

        public static string Decode(Uri url)
        {
            using var bitmap = Utils.DownloadBitmap(url);
            return Decode(bitmap);
        }
    }
}
