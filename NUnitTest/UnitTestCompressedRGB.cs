using System;
using System.IO;
using System.Linq;
using ColorZXing;
using NUnit.Framework;
using ZXing;

namespace NUnitTest
{
    public class UnitTestCompressedRGB
    {
        [Test]
        public void CompressionFlagFalsePreservesOriginalRgbFormat()
        {
            const string value = "The existing RGB wire format stays unchanged.";
            var original = ColorZXingRGB.EncodeRgba(value, 320, 320, 4);
            var explicitPlain = ColorZXingRGB.EncodeRgba(value, 320, 320, 4, compressed: false);

            Assert.AreEqual(original.Width, explicitPlain.Width);
            Assert.AreEqual(original.Height, explicitPlain.Height);
            Assert.AreEqual(original.Pixels, explicitPlain.Pixels);
            Assert.AreEqual(value, ColorZXingRGB.DecodeRgba(
                explicitPlain.Pixels, explicitPlain.Width, explicitPlain.Height, compressed: false));
        }

        [Test]
        public void EmptyPayloadRoundTrips()
        {
            var image = ColorZXingRGB.EncodeRgba(string.Empty, 240, 240, 4, compressed: true);
            Assert.AreEqual(string.Empty, ColorZXingRGB.DecodeRgba(
                image.Pixels, image.Width, image.Height, compressed: true));
        }

        [Test]
        public void CompressibleUnicodeTextRoundTripsThroughRgba()
        {
            var value = string.Concat(Enumerable.Repeat(
                "{\"type\":\"telemetry\",\"city\":\"上海\",\"active\":true,\"message\":\"hello 😀\"}\n",
                160));

            var info = ColorZXingRGB.AnalyzeCompression(value);
            var image = ColorZXingRGB.EncodeRgba(value, 400, 400, 4, compressed: true);

            Assert.IsTrue(info.IsCompressed);
            Assert.Greater(info.SavingsPercent, 90);
            Assert.AreEqual(value, ColorZXingRGB.DecodeRgba(
                image.Pixels, image.Width, image.Height, compressed: true));
        }

        [Test]
        public void CompressiblePayloadCanExceedPlainRgbCapacity()
        {
            var value = string.Concat(Enumerable.Repeat(
                "A repeated message can be compressed without adding more color levels. ",
                350));

            Assert.Throws<AggregateException>(() => ColorZXingRGB.EncodeRgba(value, 0, 0, 4));
            var info = ColorZXingRGB.AnalyzeCompression(value);
            var image = ColorZXingRGB.EncodeRgba(value, 400, 400, 4, compressed: true);

            Assert.Less(info.StoredBytes, info.OriginalBytes / 10);
            Assert.AreEqual(value, ColorZXingRGB.DecodeRgba(
                image.Pixels, image.Width, image.Height, compressed: true));
        }

        [Test]
        public void CompressedSymbolUsesFewerModulesThanPlainRgb()
        {
            var value = string.Concat(Enumerable.Repeat(
                "product=ColorZXing;region=ap-southeast;status=ready;", 80));

            var plain = ColorZXingRGB.EncodeRgba(value, 0, 0, 4);
            var info = ColorZXingRGB.AnalyzeCompression(value);
            var dense = ColorZXingRGB.EncodeRgba(value, 0, 0, 4, compressed: true);
            var scannableDense = ColorZXingRGB.EncodeRgba(value, 400, 400, 4, compressed: true);

            Assert.IsTrue(info.IsCompressed);
            Assert.Less(dense.Width, plain.Width);
            Assert.AreEqual(value, ColorZXingRGB.DecodeRgba(
                scannableDense.Pixels, scannableDense.Width, scannableDense.Height, compressed: true));
        }

        [Test]
        public void IncompressibleBinaryDataFallsBackToRawStorage()
        {
            var value = new byte[1024];
            new Random(42).NextBytes(value);

            var image = CompressedRgbCodec.EncodeBytesRgba(value, 400, 400, 4, out var info);

            Assert.IsFalse(info.IsCompressed);
            Assert.AreEqual(value, CompressedRgbCodec.DecodeBytesRgba(image.Pixels, image.Width, image.Height));
        }

        [Test]
        public void BitmapAndEncodedImageRoundTrip()
        {
            var value = string.Concat(Enumerable.Repeat("Compressed RGB bitmap payload 中文 🚀. ", 20));
            using var bitmap = ColorZXingRGB.Encode(value, 400, 400, 4, compressed: true);

            Assert.AreEqual(value, ColorZXingRGB.Decode(bitmap, compressed: true));

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            Assert.AreEqual(value, ColorZXingRGB.Decode(stream.ToArray(), compressed: true));
        }

        [Test]
        public void JpegAndRotationRoundTrip()
        {
            var value = string.Concat(Enumerable.Repeat("Camera-friendly compressed payload. ", 12));
            using var bitmap = ColorZXingRGB.Encode(value, 500, 500, 4, compressed: true);
            bitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);

            Assert.AreEqual(value, ColorZXingRGB.Decode(stream.ToArray(), compressed: true));
        }

        [Test]
        public void ShortPayloadRoundTrips()
        {
            const string value = "short compressed RGB message";

            var info = ColorZXingRGB.AnalyzeCompression(value);
            var image = ColorZXingRGB.EncodeRgba(value, 300, 300, 4, compressed: true);

            Assert.LessOrEqual(info.StoredBytes, info.OriginalBytes);
            Assert.AreEqual(value, ColorZXingRGB.DecodeRgba(
                image.Pixels, image.Width, image.Height, compressed: true));
        }

        [Test]
        public void PlainRgbSymbolIsRejectedByCompressedDecoder()
        {
            var image = ColorZXingRGB.EncodeRgba("This is a plain RGB symbol", 300, 300, 4);

            Assert.IsFalse(ColorZXingRGB.TryDecodeRgba(
                image.Pixels, image.Width, image.Height, compressed: true, out var decoded));
            Assert.AreEqual(string.Empty, decoded);
            Assert.Throws<InvalidDataException>(() => ColorZXingRGB.DecodeRgba(
                image.Pixels, image.Width, image.Height, compressed: true));
        }

        [Test]
        public void DamagedFrameFailsChecksumValidation()
        {
            var bytes = new byte[32];
            new Random(7).NextBytes(bytes);
            var frame = CompressedRgbCodec.CreateFrame(bytes, out var info);
            frame[^1] ^= 0x01;

            Assert.IsFalse(info.IsCompressed);
            Assert.Throws<InvalidDataException>(() => CompressedRgbCodec.ReadFrame(frame));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(65)]
        [TestCase(66)]
        [TestCase(67)]
        [TestCase(1024)]
        [TestCase(5000)]
        public void FrameCodecRoundTripsTokenBoundaries(int length)
        {
            var bytes = Enumerable.Range(0, length).Select(index => (byte)(index % 11)).ToArray();
            var frame = CompressedRgbCodec.CreateFrame(bytes, out _);

            Assert.AreEqual(bytes, CompressedRgbCodec.ReadFrame(frame));
        }

        [Test]
        public void InvalidRgbaDimensionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                ColorZXingRGB.DecodeRgba(new byte[15], 2, 2, compressed: true));
        }
    }
}
