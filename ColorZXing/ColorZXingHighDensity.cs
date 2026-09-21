using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using QrDecoder = ZXing.QrCode.Internal.Decoder;
using QrEncoder = ZXing.QrCode.Internal.Encoder;

namespace ColorZXing
{
    /// <summary>
    /// A 64-color RGB QR format which multiplexes two binary QR layers into
    /// each color channel using the intensity levels 0, 85, 170, and 255.
    /// </summary>
    public static class ColorZXingHighDensity
    {
        private const int LayerCount = 6;
        private const byte FormatMarker = 0xE0;

        public static Bitmap Encode(string value, int width, int height, int margin)
        {
            return Encode(value, width, height, margin, out _);
        }

        public static Bitmap Encode(string value, int width, int height, int margin,
            out ColorZXingCompressionInfo compression)
        {
            return Encode(value, width, height, margin, compressed: true, out compression);
        }

        public static Bitmap Encode(string value, int width, int height, int margin, bool compressed)
        {
            return Encode(value, width, height, margin, compressed, out _);
        }

        public static Bitmap Encode(string value, int width, int height, int margin, bool compressed,
            out ColorZXingCompressionInfo compression)
        {
            var codes = EncodeLayers(CreateFrame(value, compressed, out compression));
            return RenderBitmap(codes, width, height, margin);
        }

        public static ColorZXingPixelData EncodeRgba(string value, int width, int height, int margin)
        {
            return EncodeRgba(value, width, height, margin, out _);
        }

        public static ColorZXingPixelData EncodeRgba(string value, int width, int height, int margin,
            out ColorZXingCompressionInfo compression)
        {
            return EncodeRgba(value, width, height, margin, compressed: true, out compression);
        }

        public static ColorZXingPixelData EncodeRgba(string value, int width, int height, int margin, bool compressed)
        {
            return EncodeRgba(value, width, height, margin, compressed, out _);
        }

        public static ColorZXingPixelData EncodeRgba(string value, int width, int height, int margin, bool compressed,
            out ColorZXingCompressionInfo compression)
        {
            var codes = EncodeLayers(CreateFrame(value, compressed, out compression));
            return RenderRgba(codes, width, height, margin);
        }

        public static string Decode(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));
            return DecodePlanes(ColorZXingRGB.GetPlanes(bitmap), bitmap.Width, bitmap.Height);
        }

        public static string DecodeRgba(byte[] rgba, int width, int height)
        {
            return DecodePlanes(ColorZXingRGB.GetPlanesFromRgba(rgba, width, height), width, height);
        }

        public static string Decode(byte[] encodedImage)
        {
            using var bitmap = Utils.CreateBitmap(encodedImage);
            return Decode(bitmap);
        }

        public static string Decode(Uri url)
        {
            using var bitmap = Utils.DownloadBitmap(url);
            return Decode(bitmap);
        }

        public static bool TryDecode(Bitmap bitmap, out string value)
        {
            try
            {
                value = Decode(bitmap);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException ||
                                              exception is ArgumentException ||
                                              exception is DecoderFallbackException)
            {
                value = string.Empty;
                return false;
            }
        }

        public static bool TryDecodeRgba(byte[] rgba, int width, int height, out string value)
        {
            try
            {
                value = DecodeRgba(rgba, width, height);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException ||
                                              exception is ArgumentException ||
                                              exception is DecoderFallbackException)
            {
                value = string.Empty;
                return false;
            }
        }

        private static byte[] CreateFrame(string value, bool compressed, out ColorZXingCompressionInfo compression)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return CompressedRgbCodec.CreateFrame(Encoding.UTF8.GetBytes(value), compressed, out compression);
        }

        private static QRCode[] EncodeLayers(byte[] frame)
        {
            var chunks = CompressedRgbCodec.SplitFrame(frame, LayerCount, FormatMarker);
            var codes = new QRCode[LayerCount];
            Parallel.For(0, codes.Length, layer =>
            {
                codes[layer] = QrEncoder.encode(
                    Encoding.Latin1.GetString(chunks[layer]),
                    ErrorCorrectionLevel.L,
                    CompressedRgbCodec.BinaryHints());
            });

            var version = codes.Max(code => code.Version.VersionNumber);
            Parallel.For(0, codes.Length, layer =>
            {
                if (codes[layer].Version.VersionNumber == version)
                    return;
                var hints = CompressedRgbCodec.BinaryHints();
                hints[EncodeHintType.QR_VERSION] = version;
                codes[layer] = QrEncoder.encode(
                    Encoding.Latin1.GetString(chunks[layer]),
                    ErrorCorrectionLevel.L,
                    hints);
            });
            return codes;
        }

        private static Bitmap RenderBitmap(QRCode[] codes, int width, int height, int margin)
        {
            GetGeometry(codes, width, height, margin, out var dimension, out var outputWidth,
                out var outputHeight, out var multiple, out var left, out var top);
            var bitmap = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppRgb);
            try
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, outputWidth, outputHeight),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                try
                {
                    unsafe
                    {
                        var scan0 = (byte*)data.Scan0;
                        for (var y = 0; y < outputHeight; y++)
                        {
                            var row = scan0 + y * data.Stride;
                            for (var x = 0; x < outputWidth; x++)
                            {
                                GetModule(codes, dimension, multiple, left, top, x, y,
                                    out var red, out var green, out var blue);
                                var pixel = row + x * Constants.PixelSize;
                                pixel[0] = blue;
                                pixel[1] = green;
                                pixel[2] = red;
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

        private static ColorZXingPixelData RenderRgba(QRCode[] codes, int width, int height, int margin)
        {
            GetGeometry(codes, width, height, margin, out var dimension, out var outputWidth,
                out var outputHeight, out var multiple, out var left, out var top);
            var pixels = new byte[checked(outputWidth * outputHeight * 4)];
            for (var y = 0; y < outputHeight; y++)
            {
                for (var x = 0; x < outputWidth; x++)
                {
                    GetModule(codes, dimension, multiple, left, top, x, y,
                        out var red, out var green, out var blue);
                    var offset = (y * outputWidth + x) * 4;
                    pixels[offset] = red;
                    pixels[offset + 1] = green;
                    pixels[offset + 2] = blue;
                    pixels[offset + 3] = 255;
                }
            }
            return new ColorZXingPixelData(pixels, outputWidth, outputHeight);
        }

        private static void GetModule(QRCode[] codes, int dimension, int multiple, int left, int top,
            int x, int y, out byte red, out byte green, out byte blue)
        {
            var moduleX = (x - left) / multiple;
            var moduleY = (y - top) / multiple;
            if (x < left || y < top || moduleX >= dimension || moduleY >= dimension)
            {
                red = green = blue = 255;
                return;
            }

            red = Mix(codes[0].Matrix[moduleX, moduleY], codes[1].Matrix[moduleX, moduleY]);
            green = Mix(codes[2].Matrix[moduleX, moduleY], codes[3].Matrix[moduleX, moduleY]);
            blue = Mix(codes[4].Matrix[moduleX, moduleY], codes[5].Matrix[moduleX, moduleY]);
        }

        private static byte Mix(int lowLayer, int highLayer)
        {
            var low = lowLayer == 1 ? 0 : 1;
            var high = highLayer == 1 ? 0 : 1;
            return (byte)(low * 85 + high * 170);
        }

        private static void GetGeometry(QRCode[] codes, int width, int height, int margin,
            out int dimension, out int outputWidth, out int outputHeight, out int multiple,
            out int left, out int top)
        {
            if (width < 0 || height < 0)
                throw new ArgumentException($"Requested dimensions are too small: {width}x{height}");
            if (margin < 0)
                throw new ArgumentOutOfRangeException(nameof(margin));
            dimension = codes[0].Matrix.Width;
            var qrDimension = checked(dimension + margin * 2);
            outputWidth = Math.Max(width, qrDimension);
            outputHeight = Math.Max(height, qrDimension);
            multiple = Math.Min(outputWidth / qrDimension, outputHeight / qrDimension);
            left = (outputWidth - dimension * multiple) / 2;
            top = (outputHeight - dimension * multiple) / 2;
        }

        internal static string DecodePlanes(byte[][] planes, int width, int height)
        {
            InvalidDataException lastError = null;
            for (var detectorPlane = 0; detectorPlane < 3; detectorPlane++)
            {
                var samples = ColorZXingRGB.TrySampleChannels(
                    planes, width, height, out var dimension, detectorPlane);
                if (samples == null)
                    continue;
                try
                {
                    return DecodeSamples(samples, dimension);
                }
                catch (InvalidDataException exception)
                {
                    lastError = exception;
                }
            }
            throw lastError ?? new InvalidDataException("High-density RGB geometry could not be detected.");
        }

        private static string DecodeSamples(byte[][] samples, int dimension)
        {
            var matrices = new BitMatrix[LayerCount];
            for (var layer = 0; layer < matrices.Length; layer++)
                matrices[layer] = new BitMatrix(dimension);

            var redCalibration = DecodeChannel(samples[2], dimension, matrices[0], matrices[1]);
            var greenCalibration = DecodeChannel(samples[1], dimension, matrices[2], matrices[3]);
            var blueCalibration = DecodeChannel(samples[0], dimension, matrices[4], matrices[5]);

            var layerTexts = new string[LayerCount];
            Parallel.For(0, matrices.Length, layer =>
            {
                layerTexts[layer] = new QrDecoder().decode(matrices[layer], null)?.Text;
            });
            if (layerTexts.Any(string.IsNullOrEmpty))
                throw new InvalidDataException($"High-density RGB layers ({dimension} modules; " +
                    $"R={redCalibration.Black}/{redCalibration.White}, " +
                    $"G={greenCalibration.Black}/{greenCalibration.White}, " +
                    $"B={blueCalibration.Black}/{blueCalibration.White}) could not be decoded: " +
                    string.Join(", ", layerTexts.Select((text, index) => string.IsNullOrEmpty(text) ? index.ToString() : null)
                        .Where(index => index != null)));

            var bytes = CompressedRgbCodec.DecodeLayerTexts(layerTexts, FormatMarker);
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static (int Black, int White) DecodeChannel(byte[] samples, int dimension, BitMatrix lowLayer, BitMatrix highLayer)
        {
            EstimateFinderReferences(samples, dimension, out var black, out var white);
            if (white - black < 90)
                throw new InvalidDataException("A color channel has insufficient calibrated contrast.");

            for (var y = 0; y < dimension; y++)
            {
                for (var x = 0; x < dimension; x++)
                {
                    var value = samples[y * dimension + x];
                    var normalized = Math.Clamp((value - black) * 255 / (white - black), 0, 255);
                    var level = Math.Clamp((normalized + 42) / 85, 0, 3);
                    if ((level & 1) == 0)
                        lowLayer[x, y] = true;
                    if ((level & 2) == 0)
                        highLayer[x, y] = true;
                }
            }
            return (black, white);
        }

        private static void EstimateFinderReferences(byte[] samples, int dimension, out int black, out int white)
        {
            var blackSamples = new List<byte>(99);
            var whiteSamples = new List<byte>(48);
            AddFinder(0, 0);
            AddFinder(dimension - 7, 0);
            AddFinder(0, dimension - 7);

            blackSamples.Sort();
            whiteSamples.Sort();
            black = blackSamples[blackSamples.Count / 2];
            white = whiteSamples[whiteSamples.Count / 2];
            return;

            void AddFinder(int originX, int originY)
            {
                for (var y = 0; y < 7; y++)
                {
                    for (var x = 0; x < 7; x++)
                    {
                        var isBlack = x == 0 || x == 6 || y == 0 || y == 6 ||
                                      x >= 2 && x <= 4 && y >= 2 && y <= 4;
                        var value = samples[(originY + y) * dimension + originX + x];
                        (isBlack ? blackSamples : whiteSamples).Add(value);
                    }
                }
            }
        }
    }
}
