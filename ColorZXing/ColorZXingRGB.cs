using System;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing.QrCode;
using System.Runtime.InteropServices;
using static ZXing.RGBLuminanceSource;

namespace ColorZXing
{    
    public class ColorZXingRGB
    {        
        ///Use the IntPtr method instead of bitmap.GetPixel, it's 6x faster.
        private static void SetBitmap(Bitmap bitmap, byte[] red, byte[] green, byte[] blue)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var stride = bmd.Stride;
                var scan0 = bmd.Scan0;

                for (int y = 0; y < height; y++)
                {
                    var row = IntPtr.Add(scan0, (y * stride));
                    for (int x = 0; x < width; x++)
                    {
                        var imgIndex = IntPtr.Add(row, x * Constants.PixelSize);
                        var index = (y * width + x) * Constants.PixelSize;
                        Marshal.WriteByte(IntPtr.Add(imgIndex, 0), red[index]);
                        Marshal.WriteByte(IntPtr.Add(imgIndex, 1), green[index + 1]);
                        Marshal.WriteByte(IntPtr.Add(imgIndex, 2), blue[index + 2]);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmd);
            }
        }

        ///Use the IntPtr method instead of bitmap.SetPixel, it's 2x faster.
        private static void GetRGBByteArrayFromBitmap(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            var width = bitmap.Width;
            var height = bitmap.Height;
            var stride = bmd.Stride;
            var scan0 = bmd.Scan0;
            
            for (int y = 0; y < height; y++)            
            {
                var row = IntPtr.Add(scan0, (y * stride));
                for (int x = 0; x < width; x++)
                {
                    var imgIndex = IntPtr.Add(row, x * Constants.PixelSize);
                    var index = (y * width + x) * Constants.Gray8PixelSize;

                    blue[index] = Marshal.ReadByte(IntPtr.Add(imgIndex, 0));
                    green[index] = Marshal.ReadByte(IntPtr.Add(imgIndex, 1));
                    red[index] = Marshal.ReadByte(IntPtr.Add(imgIndex, 2));
                }
            };
        }

        public static Bitmap Encode(string value, int width, int height, int margin)
        {

            int subStringSize = value.Length / 3;
            var str1 = value.Substring(0, subStringSize);
            var str2 = value.Substring(subStringSize, subStringSize);
            var str3 = value.Substring(subStringSize * 2, value.Length - subStringSize * 2);

            var qrCodeWriter = new ZXing.BarcodeWriterPixelData
            {

                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = margin
                }
            };

            var red = qrCodeWriter.Write(str1);
            var green = qrCodeWriter.Write(str2);
            var blue = qrCodeWriter.Write(str3);

            var pixelWidth = blue.Width;
            var pixelHeight = blue.Height;

            var bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppRgb);

            SetBitmap(bitmap, red.Pixels, green.Pixels, blue.Pixels);

            return bitmap;
        }
        
        public static string Decode(Bitmap bitmap)
        {
            var byteSize = bitmap.Width * bitmap.Height * Constants.Gray8PixelSize;

            byte[] blue = new byte[byteSize];
            byte[] green = new byte[byteSize];
            byte[] red = new byte[byteSize];

            GetRGBByteArrayFromBitmap(bitmap, blue, green, red);
            var str1 = ColorZXingBasic.Decode(blue, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);
            var str2 = ColorZXingBasic.Decode(green, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);
            var str3 = ColorZXingBasic.Decode(red, bitmap.Width, bitmap.Height, BitmapFormat.Gray8);

            return str1 + str2 + str3;
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
