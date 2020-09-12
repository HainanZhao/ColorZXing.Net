using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.QrCode;

namespace ColorZXing
{
    public class ColorZXingMono
    {       
        private static void SetBitmapData(Bitmap bitmap, byte[] bytedata, Color color)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            try
            {
                int width = bitmap.Width, height = bitmap.Height, stride = bmd.Stride;                
                var scan0 = bmd.Scan0;

                var isDarkColor = Utils.IsDarkColor(color);

                for (int y = 0; y < height; y++)
                {
                    var row = IntPtr.Add(scan0, (y * stride));                    
                    for (int x = 0; x < width; x++)
                    {
                        var imgIndex = IntPtr.Add(row, x * Constants.PixelSize);
                        var index = (x + y * width) * Constants.PixelSize;

                        if (isDarkColor)
                        {
                            if (bytedata[index] < 128)
                            {
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 0), color.B);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 1), color.G);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 2), color.R);
                            }
                            else
                            {
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 0), bytedata[index]);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 1), bytedata[index + 1]);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 2), bytedata[index + 2]);
                            }
                        }
                        else
                        {
                            if (bytedata[index] >= 128)
                            {
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 0), color.B);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 1), color.G);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 2), color.R);
                            }
                            else
                            {
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 0), bytedata[index]);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 1), bytedata[index + 1]);
                                Marshal.WriteByte(IntPtr.Add(imgIndex, 2), bytedata[index + 2]);
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

        private static void SetBitmapData(Bitmap bitmap, byte[] bytedata, Color darkColor, Color lightColor)
        {
            var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            try
            {
                int width = bitmap.Width, height = bitmap.Height, stride = bmd.Stride;
                var scan0 = bmd.Scan0;

                for (int y = 0; y < height; y++)
                {
                    var row = IntPtr.Add(scan0, (y * stride));
                    for (int x = 0; x < width; x++)
                    {
                        var imgIndex = IntPtr.Add(row, x * Constants.PixelSize);
                        var index = (x + y * width) * Constants.PixelSize;
                       
                        if (bytedata[index] < 128)
                        {
                            Marshal.WriteByte(IntPtr.Add(imgIndex, 0), darkColor.B);
                            Marshal.WriteByte(IntPtr.Add(imgIndex, 1), darkColor.G);
                            Marshal.WriteByte(IntPtr.Add(imgIndex, 2), darkColor.R);
                        }
                        else
                        {
                            Marshal.WriteByte(IntPtr.Add(imgIndex, 0), lightColor.B);
                            Marshal.WriteByte(IntPtr.Add(imgIndex, 1), lightColor.G);
                            Marshal.WriteByte(IntPtr.Add(imgIndex, 2), lightColor.R);
                        }                       
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmd);
            }
        }

        public static Bitmap Encode(string value, int width, int height, int margin, Color color)
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
            SetBitmapData(bitmap, pixelData.Pixels, color);

            return bitmap;
        }

        public static Bitmap Encode(string value, int width, int height, int margin, Color color1, Color color2)
        {
            var color1Gray = Utils.GetGrayScale(color1);
            var color2Gray = Utils.GetGrayScale(color2);
            Color darkColor, lightColor;
            if (color1Gray < color2Gray)
            {                
                darkColor = color1;
                lightColor = color2;
            }
            else
            {
                darkColor = color2;
                lightColor = color1;
            }

            if (Utils.GetGrayScale(darkColor) >= 128)
                throw new Exception(Constants.NoDarkColorError);

            if (Utils.GetGrayScale(lightColor) < 128)
                throw new Exception(Constants.NoLightColorError);
           
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
            SetBitmapData(bitmap, pixelData.Pixels, color1, color2);

            return bitmap;
        }
        
        public static string Decode(Bitmap bitmap)
        {
            return ColorZXingBasic.Decode(bitmap);
        }

        public static string Decode(byte[] bytes)
        {
            return ColorZXingBasic.Decode(bytes);
        }

        public static string Decode(Uri url)
        {
            return ColorZXingBasic.Decode(url);
        }
    }
}
