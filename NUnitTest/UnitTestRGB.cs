using ColorZXing;
using NUnit.Framework;
using System.Drawing.Imaging;

namespace NUnitTest
{
    public class Tests
    {
        
        [SetUp]
        public void Setup()
        {
        }

        private void TestRGB(string value, string fileName, ImageFormat format)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingRGB.Encode(value, 400, 400, 0);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, format);

            var bitmapRead = ColorZXing.Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingRGB.Decode(bitmapRead);

            Assert.AreEqual(value, txtDecoded);
        }

        [Test]
        public void TestPngFile()
        {
            TestRGB(TestUtils.TextLong, "test.png", ImageFormat.Png);
        }

        [Test]
        public void TestJpgFile()
        {
            TestRGB(TestUtils.TextLong, "test.jpg", ImageFormat.Jpeg);
        }

        [Test]
        public void TestShortText()
        {
            TestRGB(TestUtils.TextShort, "shorttext.png", ImageFormat.Png);
        }
    }
}