using ColorZXing;
using NUnit.Framework;
using System.Drawing.Imaging;
using System.IO;

namespace NUnitTest
{
    public class UnitTestRGBExtended
    {
        private void RoundTrip(string value, int width, int height, int margin, string fileName, ImageFormat format)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingRGB.Encode(value, width, height, margin);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, format);

            var bitmapRead = ColorZXing.Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingRGB.Decode(bitmapRead);

            Assert.AreEqual(value, txtDecoded);
        }

        [Test]
        public void TestShortTextJpeg()
        {
            RoundTrip(TestUtils.TextShort, 400, 400, 0, "shorttext.jpg", ImageFormat.Jpeg);
        }

        [Test]
        public void TestShortTextGif()
        {
            RoundTrip(TestUtils.TextShort, 400, 400, 0, "shorttext.gif", ImageFormat.Gif);
        }

        [Test]
        public void TestShortTextBmp()
        {
            RoundTrip(TestUtils.TextShort, 400, 400, 0, "shorttext.bmp", ImageFormat.Bmp);
        }

        [Test]
        public void TestMargin()
        {
            RoundTrip(TestUtils.TextShort, 400, 400, 4, "shortmargin.png", ImageFormat.Png);
        }

        [Test]
        public void TestNonSquare()
        {
            RoundTrip(TestUtils.TextShort, 400, 300, 1, "nonsquare.png", ImageFormat.Png);
        }

        [Test]
        public void TestSmallSize()
        {
            RoundTrip(TestUtils.TextShort, 200, 200, 0, "small.png", ImageFormat.Png);
        }

        [Test]
        public void TestDecodeFromBytes()
        {
            var bitmap = ColorZXingRGB.Encode(TestUtils.TextShort, 400, 400, 0);
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                bytes = ms.ToArray();
            }

            var txtDecoded = ColorZXingRGB.Decode(bytes);
            Assert.AreEqual(TestUtils.TextShort, txtDecoded);
        }
    }
}
