using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Rendering;
using static ZXing.RGBLuminanceSource;

namespace ColorZXing
{
    public class ColorZXingBasic
    {
        private static void SetBitmap(Bitmap bitmap, PixelData pixelData)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                Marshal.Copy(pixelData.Pixels, 0, bmd.Scan0, pixelData.Pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bmd);
            }
        }
        private static void GetGray8ByteArrayFromBitmap(Bitmap bitmap, byte[] bytedata)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var stride = bmd.Stride;
                unsafe
                {
                    var src = (byte*)bmd.Scan0;
                    fixed (byte* pd = bytedata)
                    {
                        var d = pd;
                        for (int y = 0; y < height; y++)
                        {
                            var row = src + y * stride;
                            for (int x = 0; x < width; x++)
                            {
                                var q = row + x * Constants.PixelSize;
                                var grayScale = (*q + q[1] + q[2]) / 3;
                                *d++ = grayScale < 128 ? (byte)0 : (byte)255;
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmd);
            }
        }

        public static Bitmap Encode(string value, int width, int height, int margin)
        {
            var qrCodeWriter = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = margin
                }
            };

            var pixelData = qrCodeWriter.Write(value);
            var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
            SetBitmap(bitmap, pixelData);
            return bitmap;
        }

        public static string Decode(byte[] bytes, int width, int height, BitmapFormat format)
        {
            var source = new RGBLuminanceSource(bytes, width, height, format);
            BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
            Result qrResult = new MultiFormatReader().decode(bitmap);

            if (qrResult != null)
            {
                return qrResult.Text;
            }
            return string.Empty;
        }

        public static string Decode(Bitmap bitmap)
        {
            var byteSize = bitmap.Width * bitmap.Height * Constants.Gray8PixelSize;
            byte[] byteData = new byte[byteSize];
            GetGray8ByteArrayFromBitmap(bitmap, byteData);
            return Decode(byteData, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);
        }

        public static string Decode(byte[] bytes)
        {
            var bitmap = Utils.CreateBitmap(bytes);
            return Decode(bitmap);
        }

        public static string Decode(Uri url)
        {
            var bitmap = Utils.DownloadBitmap(url);
            return Decode(bitmap);
        }
    }
}
