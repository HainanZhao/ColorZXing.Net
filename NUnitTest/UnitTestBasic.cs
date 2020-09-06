using ColorZXing;
using NUnit.Framework;
using System.Drawing.Imaging;


namespace NUnitTest
{
    class UnitTestBasic
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestBasic()
        {
            string filePath = TestUtils.GetFilePath("basic.png");

            var bitmapWrite = ColorZXingBasic.Encode(TestUtils.TextOriginal, 400, 400, 0);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, ImageFormat.Png);

            var bitmapRead = ColorZXing.Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingBasic.Decode(bitmapRead);

            Assert.AreEqual(TestUtils.TextOriginal, txtDecoded);
        }
    }
}
