using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Rendering;
using static ZXing.RGBLuminanceSource;

namespace ColorZXing
{
    public class ColorZXingBasic
    {
        public static Bitmap Encode(string value, int width, int height, int margin)
        {
            var hints = new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.MARGIN] = margin,
                [EncodeHintType.CHARACTER_SET] = Encoding.UTF8.WebName
            };
            var matrix = new QRCodeWriter().encode(value, BarcodeFormat.QR_CODE, width, height, hints);
            return Render(matrix);
        }

        internal static Bitmap EncodeLegacy(string value, int width, int height, int margin)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = margin
                }
            };

            var pixelData = writer.Write(value);
            var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
            SetBitmap(bitmap, pixelData);
            return bitmap;
        }

        public static string Decode(byte[] bytes, int width, int height, BitmapFormat format)
        {
            var source = new RGBLuminanceSource(bytes, width, height, format);
            var qrResult = DecodeQr(source);
            if (qrResult != null)
                return qrResult;

            return DecodeGeneric(new RGBLuminanceSource(bytes, width, height, format));
        }

        public static string Decode(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            return TryDecodeQr(bitmap) ?? DecodeLegacy(bitmap);
        }

        internal static string TryDecodeQr(Bitmap bitmap)
        {
            var length = checked(bitmap.Width * bitmap.Height);
            var luminance = new byte[length];
            GetBlueChannelFromBitmap(bitmap, luminance);
            var result = DecodeQr(new ArrayLuminanceSource(luminance, bitmap.Width, bitmap.Height));
            if (result != null)
                return result;

            GetLuminanceFromBitmap(bitmap, luminance, threshold: false);
            return DecodeQr(new ArrayLuminanceSource(luminance, bitmap.Width, bitmap.Height));
        }

        internal static string DecodeLegacy(Bitmap bitmap)
        {
            var luminance = new byte[checked(bitmap.Width * bitmap.Height)];
            GetLuminanceFromBitmap(bitmap, luminance, threshold: true);
            return DecodeGeneric(new ArrayLuminanceSource(luminance, bitmap.Width, bitmap.Height));
        }

        public static string Decode(byte[] bytes)
        {
            using var bitmap = Utils.CreateBitmap(bytes);
            return Decode(bitmap);
        }

        public static string Decode(Uri url)
        {
            using var bitmap = Utils.DownloadBitmap(url);
            return Decode(bitmap);
        }

        private static Bitmap Render(BitMatrix matrix)
        {
            var bitmap = new Bitmap(matrix.Width, matrix.Height, PixelFormat.Format32bppRgb);
            try
            {
                var data = bitmap.LockBits(
                    new Rectangle(0, 0, matrix.Width, matrix.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppRgb);
                try
                {
                    unsafe
                    {
                        var scan0 = (byte*)data.Scan0;
                        for (var y = 0; y < matrix.Height; y++)
                        {
                            var row = scan0 + y * data.Stride;
                            for (var x = 0; x < matrix.Width; x++)
                                *(uint*)(row + x * Constants.PixelSize) = matrix[x, y] ? 0u : 0x00FFFFFFu;
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private static void SetBitmap(Bitmap bitmap, PixelData pixelData)
        {
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppRgb);
            try
            {
                Marshal.Copy(pixelData.Pixels, 0, data.Scan0, pixelData.Pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void GetLuminanceFromBitmap(Bitmap bitmap, byte[] output, bool threshold)
        {
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppRgb);
            try
            {
                unsafe
                {
                    var source = (byte*)data.Scan0;
                    fixed (byte* outputStart = output)
                    {
                        var target = outputStart;
                        for (var y = 0; y < bitmap.Height; y++)
                        {
                            var row = source + y * data.Stride;
                            for (var x = 0; x < bitmap.Width; x++)
                            {
                                var pixel = row + x * Constants.PixelSize;
                                var value = (pixel[0] + pixel[1] + pixel[2]) / 3;
                                *target++ = threshold
                                    ? value < 128 ? (byte)0 : (byte)255
                                    : (byte)value;
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void GetBlueChannelFromBitmap(Bitmap bitmap, byte[] output)
        {
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppRgb);
            try
            {
                unsafe
                {
                    var source = (byte*)data.Scan0;
                    fixed (byte* outputStart = output)
                    {
                        var target = outputStart;
                        var simdPixels = bitmap.Width & ~3;
                        if (Ssse3.IsSupported)
                        {
                            var mask = Vector128.Create(
                                (byte)0, 4, 8, 12,
                                0, 4, 8, 12,
                                0, 4, 8, 12,
                                0, 4, 8, 12);
                            for (var y = 0; y < bitmap.Height; y++)
                            {
                                var row = source + y * data.Stride;
                                var x = 0;
                                for (; x < simdPixels; x += 4)
                                {
                                    var pixels = Sse2.LoadVector128(row + x * Constants.PixelSize);
                                    Write4(ref target, Ssse3.Shuffle(pixels, mask));
                                }
                                for (; x < bitmap.Width; x++)
                                    *target++ = row[x * Constants.PixelSize];
                            }
                        }
                        else
                        {
                            for (var y = 0; y < bitmap.Height; y++)
                            {
                                var row = source + y * data.Stride;
                                for (var x = 0; x < bitmap.Width; x++)
                                    *target++ = row[x * Constants.PixelSize];
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Write4(ref byte* target, Vector128<byte> value)
        {
            *(uint*)target = (uint)Vector128.AsUInt64(value).GetElement(0);
            target += 4;
        }

        private static string DecodeQr(LuminanceSource source)
        {
            var bitmap = new BinaryBitmap(new HybridBinarizer(source));
            return new QRCodeReader().decode(bitmap)?.Text;
        }

        private static string DecodeGeneric(LuminanceSource source)
        {
            var bitmap = new BinaryBitmap(new HybridBinarizer(source));
            return new MultiFormatReader().decode(bitmap)?.Text ?? string.Empty;
        }

        private sealed class ArrayLuminanceSource : LuminanceSource
        {
            private readonly byte[] values;

            public ArrayLuminanceSource(byte[] values, int width, int height)
                : base(width, height)
            {
                this.values = values;
            }

            public override byte[] Matrix => values;

            public override byte[] getRow(int y, byte[] row)
            {
                if ((uint)y >= (uint)Height)
                    throw new ArgumentOutOfRangeException(nameof(y));
                if (row == null || row.Length < Width)
                    row = new byte[Width];
                Buffer.BlockCopy(values, y * Width, row, 0, Width);
                return row;
            }
        }
    }
}
