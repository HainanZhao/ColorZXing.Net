using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ColorZXing;
using NUnit.Framework;
using ZXing;
using ZXing.Common;
using static ZXing.RGBLuminanceSource;

namespace NUnitTest
{
    public class UnitTestBasicRegression
    {
        [TestCase(RotateFlipType.Rotate90FlipNone)]
        [TestCase(RotateFlipType.Rotate180FlipNone)]
        [TestCase(RotateFlipType.RotateNoneFlipX)]
        public void QrFastPathSupportsOrientation(RotateFlipType orientation)
        {
            using var bitmap = ColorZXingBasic.Encode(TestUtils.TextShort, 401, 397, 4);
            bitmap.RotateFlip(orientation);

            Assert.AreEqual(TestUtils.TextShort, ColorZXingBasic.TryDecodeQr(bitmap));
        }

        [Test]
        public void QrFastPathSupportsJpeg()
        {
            using var encoded = ColorZXingBasic.Encode(TestUtils.TextLong, 500, 500, 4);
            using var stream = new MemoryStream();
            encoded.Save(stream, ImageFormat.Jpeg);
            using var decoded = Utils.CreateBitmap(stream.ToArray());

            Assert.AreEqual(TestUtils.TextLong, ColorZXingBasic.TryDecodeQr(decoded));
        }

        [TestCase("Basic QR: 中文 😀 café")]
        [TestCase("Mono QR: Ελληνικά 🚀")]
        public void UnicodeRoundTrips(string value)
        {
            using var basic = ColorZXingBasic.Encode(value, 400, 400, 4);
            using var mono = ColorZXingMono.Encode(value, 400, 400, 4, Color.DarkBlue, Color.LightYellow);

            Assert.AreEqual(value, ColorZXingBasic.Decode(basic));
            Assert.AreEqual(value, ColorZXingMono.Decode(mono));
        }

        [TestCaseSource(nameof(MonoColorPairs))]
        public void QrFastPathSupportsMonoColors(Color dark, Color light)
        {
            using var bitmap = ColorZXingMono.Encode(TestUtils.TextShort, 400, 400, 4, dark, light);
            Assert.AreEqual(TestUtils.TextShort, ColorZXingBasic.TryDecodeQr(bitmap));
        }

        [Test]
        public void RawBufferDecodeRetainsNonQrFallback()
        {
            const string value = "CODE128-FALLBACK-12345";
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions { Width = 500, Height = 160, Margin = 10 }
            };
            var pixels = writer.Write(value);

            Assert.AreEqual(
                value,
                ColorZXingBasic.Decode(pixels.Pixels, pixels.Width, pixels.Height, BitmapFormat.BGRA32));
        }

        [Test]
        public void AdaptiveLuminanceHandlesBrightnessGradient()
        {
            using var bitmap = ColorZXingBasic.Encode(TestUtils.TextShort, 500, 500, 4);
            ApplyBrightnessGradient(bitmap);

            Assert.AreEqual(TestUtils.TextShort, ColorZXingBasic.TryDecodeQr(bitmap));
        }

        [Test]
        public void BlankImageReturnsEmptyResult()
        {
            using var bitmap = new Bitmap(101, 99);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.White);

            Assert.IsNull(ColorZXingBasic.TryDecodeQr(bitmap));
            Assert.AreEqual(string.Empty, ColorZXingBasic.Decode(bitmap));
        }

        private static object[] MonoColorPairs => new object[]
        {
            new object[] { Color.Red, Color.White },
            new object[] { Color.Blue, Color.Yellow },
            new object[] { Color.Purple, Color.Orange }
        };

        private static void ApplyBrightnessGradient(Bitmap bitmap)
        {
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppRgb);
            try
            {
                var length = Math.Abs(data.Stride) * bitmap.Height;
                var pixels = new byte[length];
                Marshal.Copy(data.Scan0, pixels, 0, length);
                for (var y = 0; y < bitmap.Height; y++)
                {
                    var row = y * data.Stride;
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var offset = row + x * Constants.PixelSize;
                        var gradient = x * 120 / (bitmap.Width - 1);
                        var value = pixels[offset] < 128 ? 20 + gradient : 120 + gradient;
                        pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = (byte)value;
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
