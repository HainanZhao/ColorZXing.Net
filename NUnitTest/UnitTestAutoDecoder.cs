using System;
using System.Drawing;
using ColorZXing;
using NUnit.Framework;

namespace NUnitTest
{
    public class UnitTestAutoDecoder
    {
        [Test]
        public void DetectsBlackAndWhite()
        {
            const string value = "Conventional black and white QR";
            var image = ColorZXingBasic.EncodeRgba(value, 400, 400, 4);

            var result = ColorZXingDecoder.DecodeRgba(image.Pixels, image.Width, image.Height);

            Assert.AreEqual(ColorZXingFormat.BlackAndWhite, result.Format);
            Assert.AreEqual(value, result.Text);
        }

        [Test]
        public void DetectsPlainRgb()
        {
            const string value = "Plain RGB payload with distinct channel chunks 0123456789";
            var image = ColorZXingRGB.EncodeRgba(value, 400, 400, 4);

            var result = ColorZXingDecoder.DecodeRgba(image.Pixels, image.Width, image.Height);

            Assert.AreEqual(ColorZXingFormat.Rgb, result.Format);
            Assert.AreEqual(value, result.Text);
        }

        [Test]
        public void DetectsCompressedRgb()
        {
            var value = string.Concat(System.Linq.Enumerable.Repeat("compressed RGB metadata;", 30));
            var image = ColorZXingRGB.EncodeRgba(value, 400, 400, 4, compressed: true);

            var result = ColorZXingDecoder.DecodeRgba(image.Pixels, image.Width, image.Height);

            Assert.AreEqual(ColorZXingFormat.CompressedRgb, result.Format);
            Assert.AreEqual(value, result.Text);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DetectsHighDensityWithOrWithoutCompression(bool compressed)
        {
            var value = string.Concat(System.Linq.Enumerable.Repeat("64-color spectrum payload;", 24));
            var image = ColorZXingHighDensity.EncodeRgba(value, 600, 600, 4, compressed);

            var result = ColorZXingDecoder.DecodeRgba(image.Pixels, image.Width, image.Height);

            Assert.AreEqual(ColorZXingFormat.HighDensity64Color, result.Format);
            Assert.AreEqual(value, result.Text);
        }

        [Test]
        public void DetectsColoredConventionalQrAsBlackAndWhite()
        {
            const string value = "Colored conventional QR";
            using var bitmap = ColorZXingMono.Encode(value, 400, 400, 4, Color.Blue, Color.Yellow);

            var result = ColorZXingDecoder.Decode(bitmap);

            Assert.AreEqual(ColorZXingFormat.BlackAndWhite, result.Format);
            Assert.AreEqual(value, result.Text);
        }

        [Test]
        public void InvalidImageReturnsFalse()
        {
            var rgba = new byte[100 * 100 * 4];
            Array.Fill(rgba, (byte)255);

            Assert.IsFalse(ColorZXingDecoder.TryDecodeRgba(rgba, 100, 100, out var result));
            Assert.IsNull(result);
        }
    }
}
