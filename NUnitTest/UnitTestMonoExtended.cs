using System;
using ColorZXing;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NUnit.Framework;

namespace NUnitTest
{
    public class UnitTestMonoExtended
    {
        private void RoundTrip(string value, int width, int height, string fileName, Color color)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingMono.Encode(value, width, height, 0, color);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, ImageFormat.Png);

            var bitmapRead = ColorZXing.Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingMono.Decode(bitmapRead);

            Assert.AreEqual(value, txtDecoded);
        }

        private void RoundTrip(string value, int width, int height, string fileName, Color color1, Color color2)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingMono.Encode(value, width, height, 0, color1, color2);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, ImageFormat.Png);

            var bitmapRead = ColorZXing.Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingMono.Decode(bitmapRead);

            Assert.AreEqual(value, txtDecoded);
        }

        [Test]
        public void TestBlackWhite()
        {
            RoundTrip(TestUtils.TextShort, 400, 400, "blackwhite.png", Color.Black, Color.White);
        }

        [Test]
        public void TestTwoDarkColorsRejected()
        {
            Assert.Throws<Exception>(() => ColorZXingMono.Encode(TestUtils.TextShort, 400, 400, 0, Color.Black, Color.DarkBlue));
        }

        [Test]
        public void TestTwoLightColorsRejected()
        {
            Assert.Throws<Exception>(() => ColorZXingMono.Encode(TestUtils.TextShort, 400, 400, 0, Color.White, Color.LightYellow));
        }

        [Test]
        public void TestOrangePurple()
        {
            RoundTrip(TestUtils.TextShort, 400, 400, "orangepurple.png", Color.Purple, Color.Orange);
        }

        [Test]
        public void TestSmallSize()
        {
            RoundTrip(TestUtils.TextShort, 200, 200, "monosmall.png", Color.Red);
        }

        [Test]
        public void TestDecodeFromBytes()
        {
            var bitmap = ColorZXingMono.Encode(TestUtils.TextShort, 400, 400, 0, Color.Black, Color.White);
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                bytes = ms.ToArray();
            }

            var txtDecoded = ColorZXingMono.Decode(bytes);
            Assert.AreEqual(TestUtils.TextShort, txtDecoded);
        }
    }
}
