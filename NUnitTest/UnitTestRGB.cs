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

        private void TestRGB(string fileName, ImageFormat format)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingRGB.Encode(TestUtils.TextOriginal, 500, 500, 0);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, format);

            var bitmapRead = ColorZXing.Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingRGB.Decode(bitmapRead);

            Assert.AreEqual(TestUtils.TextOriginal, txtDecoded);
        }

        [Test]
        public void TestPngFile()
        {
            TestRGB("test.png", ImageFormat.Png);
        }

        [Test]
        public void TestJpgFile()
        {
            TestRGB("test.jpg", ImageFormat.Jpeg);
        }

    }
}