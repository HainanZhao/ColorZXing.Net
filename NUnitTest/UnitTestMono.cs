using ColorZXing;
using NUnit.Framework;
using System.Drawing;
using System.Drawing.Imaging;

namespace NUnitTest
{
    public class UnitTestMono
    {

        [SetUp]
        public void Setup()
        {
        }

        private void TestMonoColor(string value, string fileName, Color color)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingMono.Encode(value, 400, 400, 0, color);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, ImageFormat.Png);

            var bitmapRead = Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingMono.Decode(bitmapRead);

            Assert.AreEqual(TestUtils.TextLong, txtDecoded);
        }

        private void TestMonoColor(string value, string fileName, Color color1, Color color2)
        {
            string filePath = TestUtils.GetFilePath(fileName);

            var bitmapWrite = ColorZXingMono.Encode(value, 400, 400, 0, color1, color2);
            ColorZXing.Utils.WriteBitMap(bitmapWrite, filePath, ImageFormat.Png);

            var bitmapRead = Utils.ReadBitMap(filePath);
            var txtDecoded = ColorZXingMono.Decode(bitmapRead);

            Assert.AreEqual(value, txtDecoded);
        }

        [Test]
        public void TestRed()
        {
            TestMonoColor(TestUtils.TextLong, "red.png", Color.Red);
        }

        [Test]
        public void TestPink()
        {
            TestMonoColor(TestUtils.TextLong, "pink.png", Color.Pink);
        }

        [Test]
        public void TestGreen()
        {
            TestMonoColor(TestUtils.TextLong, "green.png", Color.Green);
        }


        [Test]
        public void TestRedPink()
        {
            TestMonoColor(TestUtils.TextLong, "redpink.png", Color.Red, Color.Pink);
        }

        [Test]
        public void TestBlueYellow()
        {
            TestMonoColor(TestUtils.TextLong, "blueyellow.png", Color.Blue, Color.Yellow);
        }
    }
}
