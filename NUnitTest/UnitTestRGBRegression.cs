using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ColorZXing;
using NUnit.Framework;

namespace NUnitTest
{
    public class UnitTestRGBRegression
    {
        [TestCase(RotateFlipType.Rotate180FlipNone)]
        [TestCase(RotateFlipType.Rotate270FlipNone)]
        [TestCase(RotateFlipType.RotateNoneFlipX)]
        public void SharedDecodeSupportsOrientation(RotateFlipType orientation)
        {
            using var bitmap = ColorZXingRGB.Encode(TestUtils.TextShort, 401, 397, 4);
            bitmap.RotateFlip(orientation);
            Assert.AreEqual(TestUtils.TextShort, ColorZXingRGB.TryDecodeShared(bitmap));
        }

        [Test]
        public void UnequalLegacyVersionsUseFallback()
        {
            var value = new string('1', 60) + new string('A', 60) + new string('a', 60);
            using var bitmap = ColorZXingRGB.EncodeLegacy(value, 500, 500, 4);
            Assert.AreEqual(value, ColorZXingRGB.DecodeLegacy(bitmap));
            Assert.IsNull(ColorZXingRGB.TryDecodeShared(bitmap));
            Assert.AreEqual(value, ColorZXingRGB.Decode(bitmap));
        }

        [Test]
        public void MixedEncodingModesShareGeometry()
        {
            var value = new string('1', 120) + new string('A', 120) + new string('a', 120);
            using var bitmap = ColorZXingRGB.Encode(value, 501, 499, 4);
            Assert.AreEqual(value, ColorZXingRGB.TryDecodeShared(bitmap));
            Assert.AreEqual(value, ColorZXingRGB.DecodeLegacy(bitmap));
        }

        [Test]
        public void UnicodeByteImbalanceRoundTrips()
        {
            var value = new string('a', 180) + string.Concat(Enumerable.Repeat("界😀", 45));
            using var bitmap = ColorZXingRGB.Encode(value, 500, 500, 4);
            Assert.AreEqual(value, ColorZXingRGB.TryDecodeShared(bitmap));
            Assert.AreEqual(value, ColorZXingRGB.DecodeLegacy(bitmap));
        }

        [Test]
        public void BlankImageReturnsNoPayload()
        {
            using var bitmap = new Bitmap(101, 99);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.White);
            Assert.IsNull(ColorZXingRGB.TryDecodeShared(bitmap));
            Assert.AreEqual(string.Empty, ColorZXingRGB.Decode(bitmap));
        }

        [Test]
        public void ConcurrentIndependentRoundTrips()
        {
            Parallel.For(0, 12, i =>
            {
                var value = $"Independent RGB QR payload {i}: 😀 中文";
                using var bitmap = ColorZXingRGB.Encode(value, 301, 299, 4);
                Assert.AreEqual(value, ColorZXingRGB.TryDecodeShared(bitmap));
            });
        }

        [TestCase("")]
        [TestCase("A")]
        [TestCase("AB")]
        public void TooShortPayloadIsRejected(string value)
        {
            Assert.Throws<ArgumentException>(() => ColorZXingRGB.Encode(value, 200, 200, 4));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ColorZXingRGB.Encode(null, 200, 200, 4));
            Assert.Throws<ArgumentNullException>(() => ColorZXingRGB.Decode((Bitmap)null));
        }
    }
}
