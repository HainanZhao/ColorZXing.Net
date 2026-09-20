using System;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing.QrCode;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
                unsafe
                {
                    var dst = (byte*)bmd.Scan0;
                    fixed (byte* pr = red, pg = green, pb = blue)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var row = dst + y * stride;
                            for (int x = 0; x < width; x++)
                            {
                                var index = (y * width + x) * Constants.PixelSize;
                                var q = row + x * Constants.PixelSize;
                                q[0] = pr[index];
                                q[1] = pg[index + 1];
                                q[2] = pb[index + 2];
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

        ///Use the IntPtr method instead of bitmap.SetPixel, it's 2x faster.
        private static void GetRGBByteArrayFromBitmap(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
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
                    fixed (byte* pb = blue, pg = green, pr = red)
                    {
                        var db = pb;
                        var dg = pg;
                        var dr = pr;
                        if (Ssse3.IsSupported)
                        {
                            var maskB = Vector128.Create((byte)0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12);
                            var maskG = Vector128.Create((byte)1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13);
                            var maskR = Vector128.Create((byte)2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14);
                            var simdPixels = width & ~3;
                            for (int y = 0; y < height; y++)
                            {
                                var row = src + y * stride;
                                for (int x = 0; x < simdPixels; x += 4)
                                {
                                    var v = Sse2.LoadVector128(row + x * Constants.PixelSize);
                                    Write4(ref db, Ssse3.Shuffle(v, maskB));
                                    Write4(ref dg, Ssse3.Shuffle(v, maskG));
                                    Write4(ref dr, Ssse3.Shuffle(v, maskR));
                                }
                                for (int x = simdPixels; x < width; x++)
                                {
                                    var q = row + x * Constants.PixelSize;
                                    *db++ = q[0];
                                    *dg++ = q[1];
                                    *dr++ = q[2];
                                }
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                var row = src + y * stride;
                                for (int x = 0; x < width; x++)
                                {
                                    var q = row + x * Constants.PixelSize;
                                    *db++ = q[0];
                                    *dg++ = q[1];
                                    *dr++ = q[2];
                                }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Write4(ref byte* d, Vector128<byte> v)
        {
            *(uint*)d = (uint)Vector128.AsUInt64(v).GetElement(0);
            d += 4;
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
