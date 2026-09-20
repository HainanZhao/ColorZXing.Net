using System;
using System.Linq;
using ColorZXing;
using NUnit.Framework;

namespace NUnitTest
{
    public class UnitTestPixelData
    {
        [TestCase("Browser RGB payload with 中文 and 😀")]
        [TestCase("A deliberately longer browser payload that crosses QR module rows and validates all three RGBA channel layers together.")]
        public void RgbRgbaRoundTrips(string value)
        {
            var image = ColorZXingRGB.EncodeRgba(value, 401, 397, 4);

            Assert.AreEqual(401, image.Width);
            Assert.AreEqual(397, image.Height);
            Assert.AreEqual(value, ColorZXingRGB.DecodeRgba(image.Pixels, image.Width, image.Height));
            Assert.IsTrue(image.Pixels.Where((_, index) => index % 4 == 3).All(alpha => alpha == 255));
        }

        [TestCase("Browser black and white QR: café 🚀")]
        public void BasicRgbaRoundTrips(string value)
        {
            var image = ColorZXingBasic.EncodeRgba(value, 383, 379, 4);

            Assert.AreEqual(value, ColorZXingBasic.DecodeRgba(image.Pixels, image.Width, image.Height));
            for (var index = 0; index < image.Pixels.Length; index += 4)
            {
                Assert.AreEqual(image.Pixels[index], image.Pixels[index + 1]);
                Assert.AreEqual(image.Pixels[index], image.Pixels[index + 2]);
                Assert.AreEqual(255, image.Pixels[index + 3]);
            }
        }

        [Test]
        public void RgbaApisValidateBufferDimensions()
        {
            Assert.Throws<ArgumentException>(() => new ColorZXingPixelData(new byte[3], 1, 1));
            Assert.Throws<ArgumentException>(() => ColorZXingRGB.DecodeRgba(new byte[15], 2, 2));
            Assert.Throws<ArgumentException>(() => ColorZXingBasic.DecodeRgba(new byte[15], 2, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ColorZXingRGB.DecodeRgba(Array.Empty<byte>(), 0, 1));
        }
    }
}
