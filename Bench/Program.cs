using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ColorZXing;

namespace Bench;

internal static class Legacy
{
    internal static void GetRGBByteArrayFromBitmap(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
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
        }
        bitmap.UnlockBits(bmd);
    }

    internal static void GetGray8ByteArrayFromBitmap(Bitmap bitmap, byte[] bytedata)
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
                int grayScale = Utils.GetGrayScale(Marshal.ReadByte(IntPtr.Add(imgIndex, 0)), Marshal.ReadByte(IntPtr.Add(imgIndex, 1)), Marshal.ReadByte(IntPtr.Add(imgIndex, 2)));
                byte blackOrWhite = grayScale < 128 ? (byte)0 : (byte)255;
                bytedata[index] = blackOrWhite;
            }
        }
        bitmap.UnlockBits(bmd);
    }
}


internal static class Program
{
    private const int Iterations = 30;
    private const int W = 400, H = 400;
    private static readonly byte[] bufA = new byte[W * H * 4];
    private static readonly byte[] bufB = new byte[W * H * 4];
    private static readonly byte[] bufC = new byte[W * H * 4];
    private static readonly byte[] bufD = new byte[W * H];

    private static void Main()
    {
        using var bitmap = ColorZXingRGB.Encode(TestString(200), W, H, 0);

        time("RGB v0 legacy (Marshal.ReadByte x3)", () => Legacy.GetRGBByteArrayFromBitmap(bitmap, bufA, bufB, bufC));
        time("RGB v1 row-copy  (current PR-style) ", () => RowCopyRGB(bitmap, bufA, bufB, bufC));
        time("RGB v2 unsafe pointers              ", () => UnsafeRGB(bitmap, bufA, bufB, bufC));
        time("RGB v3 SIMD Ssse3 shuffle           ", () => SimdRGB(bitmap, bufA, bufB, bufC));

        time("Gray v0 legacy                      ", () => Legacy.GetGray8ByteArrayFromBitmap(bitmap, bufD));
        time("Gray v1 row-copy (current)          ", () => RowCopyGray(bitmap, bufD));
        time("Gray v2 unsafe pointers             ", () => UnsafeGray(bitmap, bufD));
        time("Gray v3 SIMD Ssse3 shuffle          ", () => SimdGray(bitmap, bufD));

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) ColorZXingRGB.Decode(bitmap);
        sw.Stop();
        Console.WriteLine($"\nEnd-to-end ColorZXingRGB.Decode 400x400 (library as-is): {sw.ElapsedMilliseconds / (double)Iterations:F2} ms/op");
    }

    private static void time(string name, Action act)
    {
        act();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) act();
        Console.WriteLine($"{name} : {sw.Elapsed.TotalMilliseconds / Iterations:F3} ms/op");
    }

    private static string TestString(int len)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < len; i++) sb.Append((char)('A' + i % 26));
        return sb.ToString();
    }

    private static void RowCopyRGB(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride; var scan0 = bmd.Scan0;
            var rowSize = width * 4; var rowBuffer = new byte[rowSize];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(scan0, y * stride), rowBuffer, 0, rowSize);
                var outputOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    var source = x * 4;
                    var target = outputOffset + x;
                    blue[target] = rowBuffer[source];
                    green[target] = rowBuffer[source + 1];
                    red[target] = rowBuffer[source + 2];
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static void UnsafeRGB(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride;
            unsafe
            {
                var src = (byte*)bmd.Scan0;
                fixed (byte* pb = blue, pg = green, pr = red)
                {
                    var db = pb; var dg = pg; var dr = pr;
                    for (int y = 0; y < height; y++)
                    {
                        var row = src + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            var q = row + x * 4;
                            *db++ = q[0];
                            *dg++ = q[1];
                            *dr++ = q[2];
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static unsafe void SimdRGB(Bitmap bitmap, byte[] blue, byte[] green, byte[] red)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride;
            unsafe
            {
                var src = (byte*)bmd.Scan0;
                fixed (byte* pb = blue, pg = green, pr = red)
                {
                    if (System.Runtime.Intrinsics.X86.Ssse3.IsSupported)
                    {
                        var maskB = Vector128.Create((byte)0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12);
                        var maskG = Vector128.Create((byte)1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13, 1, 5, 9, 13);
                        var maskR = Vector128.Create((byte)2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14, 2, 6, 10, 14);
                        var db = pb; var dg = pg; var dr = pr;
                        var simdPixels = width & ~3;
                        for (int y = 0; y < height; y++)
                        {
                            var row = src + y * stride;
                            for (int x = 0; x < simdPixels; x += 4)
                            {
                                var v = Sse2.LoadVector128(row + x * 4);
                                var lo = Ssse3.Shuffle(v, maskB);
                                var hi = Ssse3.Shuffle(v, maskG);
                                var rr = Ssse3.Shuffle(v, maskR);
                                Write4(db, lo); Write4(dg, hi); Write4(dr, rr);
                                db += 4; dg += 4; dr += 4;
                            }
                            for (int x = simdPixels; x < width; x++)
                            {
                                var q = row + x * 4;
                                *db++ = q[0]; *dg++ = q[1]; *dr++ = q[2];
                            }
                        }
                    }
                    else
                    {
                        var db = pb; var dg = pg; var dr = pr;
                        for (int y = 0; y < height; y++)
                        {
                            var row = src + y * stride;
                            for (int x = 0; x < width; x++)
                            {
                                var q = row + x * 4;
                                *db++ = q[0]; *dg++ = q[1]; *dr++ = q[2];
                            }
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Write4(byte* d, System.Runtime.Intrinsics.Vector128<byte> v)
    {
        ulong lo = System.Runtime.Intrinsics.Vector128.AsUInt64(v).ToScalar();
        *(uint*)d = (uint)lo;
    }

    private static void RowCopyGray(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
            var stride = bmd.Stride; var scan0 = bmd.Scan0;
            var rowSize = width * 4; var rowBuffer = new byte[rowSize];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(scan0, y * stride), rowBuffer, 0, rowSize);
                var outputOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    var s = x * 4;
                    var grayScale = (rowBuffer[s] + rowBuffer[s + 1] + rowBuffer[s + 2]) / 3;
                    bytedata[outputOffset + x] = grayScale < 128 ? (byte)0 : (byte)255;
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static void UnsafeGray(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
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
                            var q = row + x * 4;
                            var grayScale = (*q + q[1] + q[2]) / 3;
                            *d++ = grayScale < 128 ? (byte)0 : (byte)255;
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }

    private static unsafe void SimdGray(Bitmap bitmap, byte[] bytedata)
    {
        var bmd = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var width = bitmap.Width; var height = bitmap.Height;
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
                            var q = row + x * 4;
                            var grayScale = (*q + q[1] + q[2]) / 3;
                            *d++ = grayScale < 128 ? (byte)0 : (byte)255;
                        }
                    }
                }
            }
        }
        finally { bitmap.UnlockBits(bmd); }
    }
}
