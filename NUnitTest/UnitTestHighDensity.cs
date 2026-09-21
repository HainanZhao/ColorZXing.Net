using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorZXing;
using NUnit.Framework;

namespace NUnitTest
{
    public class UnitTestHighDensity
    {
        [Test]
        public void CompressionCanBeDisabledForPureSpectrumMuxing()
        {
            var value = string.Concat(Enumerable.Repeat("structured high-density payload ", 20));
            var image = ColorZXingHighDensity.EncodeRgba(
                value, 600, 600, 4, compressed: false, out var compression);

            Assert.IsFalse(compression.IsCompressed);
            Assert.AreEqual(compression.OriginalBytes, compression.StoredBytes);
            Assert.AreEqual(value, ColorZXingHighDensity.DecodeRgba(
                image.Pixels, image.Width, image.Height));
        }

        [Test]
        public void CompressionFurtherReducesCompressibleSpectrumSymbol()
        {
            var value = string.Concat(Enumerable.Repeat("repeatable telemetry record;", 120));
            var uncompressed = ColorZXingHighDensity.EncodeRgba(
                value, 0, 0, 4, compressed: false);
            var compressed = ColorZXingHighDensity.EncodeRgba(
                value, 0, 0, 4, compressed: true);

            Assert.Less(compressed.Width, uncompressed.Width);
        }

        [Test]
        public void SixLayerRgbaRoundTripsUnicode()
        {
            var value = string.Concat(Enumerable.Repeat(
                "Six QR layers across 64 colors · 上海 · 😀 · ", 30));

            var image = ColorZXingHighDensity.EncodeRgba(value, 500, 500, 4, out var compression);

            Assert.AreEqual(value, ColorZXingHighDensity.DecodeRgba(
                image.Pixels, image.Width, image.Height));
            Assert.Greater(compression.OriginalBytes, 0);
        }

        [Test]
        public void SymbolUsesMoreThanEightColors()
        {
            var value = RandomLowercase(3000, 17);
            var image = ColorZXingHighDensity.EncodeRgba(value, 0, 0, 4);
            var colors = new HashSet<int>();
            for (var offset = 0; offset < image.Pixels.Length; offset += 4)
                colors.Add(image.Pixels[offset] << 16 | image.Pixels[offset + 1] << 8 | image.Pixels[offset + 2]);

            Assert.Greater(colors.Count, 8);
            Assert.LessOrEqual(colors.Count, 64);
        }

        [Test]
        public void SixLayersUseFewerModulesThanThreeForRandomText()
        {
            var value = RandomLowercase(3000, 23);

            var threeLayer = ColorZXingRGB.EncodeRgba(value, 0, 0, 4);
            var sixLayer = ColorZXingHighDensity.EncodeRgba(value, 0, 0, 4);

            Assert.Less(sixLayer.Width, threeLayer.Width);
        }

        [Test]
        public void SixLayersCanExceedThreeLayerCapacity()
        {
            var value = RandomLowercase(12000, 31);

            Assert.Throws<AggregateException>(() => ColorZXingRGB.EncodeRgba(value, 0, 0, 4));
            var image = ColorZXingHighDensity.EncodeRgba(value, 600, 600, 4);

            Assert.AreEqual(value, ColorZXingHighDensity.DecodeRgba(
                image.Pixels, image.Width, image.Height));
        }

        [Test]
        public void WhiteBalanceOffsetAndNoiseAreCalibrated()
        {
            var value = RandomLowercase(1800, 47);
            var image = ColorZXingHighDensity.EncodeRgba(value, 600, 600, 4);
            var transformed = ApplyChannelCalibration(image.Pixels);

            Assert.AreEqual(value, ColorZXingHighDensity.DecodeRgba(
                transformed, image.Width, image.Height));
        }

        [Test]
        public void RotationRoundTrips()
        {
            var value = RandomLowercase(1000, 59);
            using var bitmap = ColorZXingHighDensity.Encode(value, 600, 600, 4);
            bitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);

            Assert.AreEqual(value, ColorZXingHighDensity.Decode(bitmap));
        }

        [Test]
        public void JpegRoundTripsAtLargeModuleScale()
        {
            var value = RandomLowercase(500, 71);
            using var bitmap = ColorZXingHighDensity.Encode(value, 800, 800, 4);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);

            Assert.AreEqual(value, ColorZXingHighDensity.Decode(stream.ToArray()));
        }

        [Test]
        public void ThreeLayerFormatsAreRejected()
        {
            var plain = ColorZXingRGB.EncodeRgba("ordinary RGB payload", 320, 320, 4);
            var compressed = ColorZXingRGB.EncodeRgba(
                "compressed RGB payload compressed RGB payload", 320, 320, 4, compressed: true);

            Assert.IsFalse(ColorZXingHighDensity.TryDecodeRgba(
                plain.Pixels, plain.Width, plain.Height, out _));
            Assert.IsFalse(ColorZXingHighDensity.TryDecodeRgba(
                compressed.Pixels, compressed.Width, compressed.Height, out _));
        }

        private static string RandomLowercase(int length, int seed)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789-_:/.";
            var random = new Random(seed);
            return new string(Enumerable.Range(0, length)
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
        }

        private static byte[] ApplyChannelCalibration(byte[] source)
        {
            var result = (byte[])source.Clone();
            var black = new[] { 14, 9, 20 };
            var white = new[] { 205, 238, 182 };
            var random = new Random(101);
            for (var offset = 0; offset < result.Length; offset += 4)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    var mapped = black[channel] + source[offset + channel] *
                        (white[channel] - black[channel]) / 255;
                    result[offset + channel] = (byte)Math.Clamp(mapped + random.Next(-7, 8), 0, 255);
                }
            }
            return result;
        }
    }
}
