using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using static ZXing.RGBLuminanceSource;

namespace ColorZXing
{
    /// <summary>Automatically detects and decodes ColorZXing and conventional QR formats.</summary>
    public static class ColorZXingDecoder
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static ColorZXingDecodeResult Decode(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));
            return DecodePlanes(ColorZXingRGB.GetPlanes(bitmap), bitmap.Width, bitmap.Height,
                () => ColorZXingBasic.Decode(bitmap));
        }

        public static ColorZXingDecodeResult DecodeRgba(byte[] rgba, int width, int height)
        {
            var planes = ColorZXingRGB.GetPlanesFromRgba(rgba, width, height);
            return DecodePlanes(planes, width, height, () => DecodeAverageLuminance(planes, width, height));
        }

        public static ColorZXingDecodeResult Decode(byte[] encodedImage)
        {
            using var bitmap = Utils.CreateBitmap(encodedImage);
            return Decode(bitmap);
        }

        public static ColorZXingDecodeResult Decode(Uri url)
        {
            using var bitmap = Utils.DownloadBitmap(url);
            return Decode(bitmap);
        }

        public static bool TryDecode(Bitmap bitmap, out ColorZXingDecodeResult result)
        {
            try
            {
                result = Decode(bitmap);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is ArgumentException)
            {
                result = null;
                return false;
            }
        }

        public static bool TryDecodeRgba(byte[] rgba, int width, int height, out ColorZXingDecodeResult result)
        {
            try
            {
                result = DecodeRgba(rgba, width, height);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is ArgumentException)
            {
                result = null;
                return false;
            }
        }

        private static ColorZXingDecodeResult DecodePlanes(byte[][] planes, int width, int height,
            Func<string> decodeBlackAndWhite)
        {
            var layers = ColorZXingRGB.TryDecodeSharedLayers(planes, width, height)
                         ?? ColorZXingRGB.DecodePlanesIndependently(planes, width, height);

            if (HasLayerFamily(layers, 0xF0))
            {
                var bytes = CompressedRgbCodec.DecodeLayerTexts(layers, 0xF0);
                return new ColorZXingDecodeResult(StrictUtf8.GetString(bytes), ColorZXingFormat.CompressedRgb);
            }

            if (HasLayerFamily(layers, 0xE0))
            {
                return new ColorZXingDecodeResult(
                    ColorZXingHighDensity.DecodePlanes(planes, width, height),
                    ColorZXingFormat.HighDensity64Color);
            }

            if (layers.All(layer => !string.IsNullOrEmpty(layer)))
            {
                if (layers[0] == layers[1] && layers[1] == layers[2])
                    return new ColorZXingDecodeResult(layers[0], ColorZXingFormat.BlackAndWhite);
                return new ColorZXingDecodeResult(string.Concat(layers), ColorZXingFormat.Rgb);
            }

            try
            {
                return new ColorZXingDecodeResult(
                    ColorZXingHighDensity.DecodePlanes(planes, width, height),
                    ColorZXingFormat.HighDensity64Color);
            }
            catch (InvalidDataException)
            {
                var text = decodeBlackAndWhite();
                if (!string.IsNullOrEmpty(text))
                    return new ColorZXingDecodeResult(text, ColorZXingFormat.BlackAndWhite);
                throw new InvalidDataException("No supported QR format could be decoded from the image.");
            }
        }

        private static bool HasLayerFamily(string[] layers, byte family)
        {
            if (layers == null || layers.Length == 0)
                return false;
            foreach (var layer in layers)
            {
                if (string.IsNullOrEmpty(layer))
                    return false;
                var bytes = Encoding.Latin1.GetBytes(layer);
                if (bytes.Length < 2 || bytes[0] != 0xFF || (bytes[1] & 0xF0) != family)
                    return false;
            }
            return true;
        }

        private static string DecodeAverageLuminance(byte[][] planes, int width, int height)
        {
            var gray = new byte[checked(width * height)];
            for (var index = 0; index < gray.Length; index++)
                gray[index] = (byte)((planes[0][index] + planes[1][index] + planes[2][index]) / 3);
            return ColorZXingBasic.Decode(gray, width, height, BitmapFormat.Gray8);
        }
    }
}
