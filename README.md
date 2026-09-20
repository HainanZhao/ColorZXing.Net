# ColorZXing.Net

[![NuGet](https://img.shields.io/nuget/v/ColorZXing.Net.svg)](https://www.nuget.org/packages/ColorZXing.Net/)

Generate and decode color QR codes in C# using ZXing.Net. Choose a conventional
black-and-white QR, custom foreground/background colors, or an RGB image
carrying three independent QR payloads.

![RGB QR example](https://raw.githubusercontent.com/HainanZhao/ColorZXing.Net/master/Images/test.png)

## Install

Requires .NET 8 or later.

```sh
dotnet add package ColorZXing.Net --version 0.3.0
```

The bitmap API uses System.Drawing.Common, currently pinned to 4.7.3. Windows is
the recommended deployment platform. Tests and benchmarks also run on Linux
with this pinned package and libgdiplus; this does not imply support with modern
System.Drawing.Common versions, which Microsoft supports
[only on Windows](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only).

## RGB QR codes

```csharp
using ColorZXing;
using System.Drawing.Imaging;

const string text = "Color QR codes: hello, 中文, 😀!";
using var bitmap = ColorZXingRGB.Encode(text, width: 400, height: 400, margin: 4);
bitmap.Save("color-qr.png", ImageFormat.Png);

string decoded = ColorZXingRGB.Decode(bitmap);
Console.WriteLine(decoded);
```

RGB mode splits the text into three chunks stored in the blue, green, and red
components, in that order. Each component contains an ordinary QR symbol. A
conventional grayscale scanner is not expected to reconstruct the whole
message; use ColorZXingRGB.Decode.

Version 0.3.0 balances chunks by UTF-8 byte count without splitting Unicode
scalars, then aligns the three QR versions. Text must contain at least three
Unicode scalar values. The encoder uses QR error-correction level L. Width and
height are requested minimum pixel dimensions; the result can grow to fit the
module grid and margin. Margin is measured in QR modules; four is a practical
default for a quiet zone.

Prefer PNG for lossless storage. JPEG, blur, printing, illumination, and camera
color mixing can reduce channel separation. Three channels offer up to roughly
three times the payload capacity of a comparable monochrome symbol under ideal
conditions, not a guaranteed threefold gain in real-world scanning.

## Monochrome and custom colors

```csharp
using ColorZXing;
using System.Drawing;

using var basic = ColorZXingBasic.Encode("Standard QR", 400, 400, 4);
string basicText = ColorZXingBasic.Decode(basic);

using var green = ColorZXingMono.Encode("Green QR", 400, 400, 4, Color.Green);
string greenText = ColorZXingMono.Decode(green);

using var twoColors = ColorZXingMono.Encode(
    "Two-color QR", 400, 400, 4, Color.DarkBlue, Color.LightYellow);
string twoColorText = ColorZXingMono.Decode(twoColors);
```

For two-color encoding, pass the dark color first and the light color second.
Current validation uses the average of R, G, and B: the dark color must be below
128 and the light color at least 128. Strong contrast helps decoding.

## Decode images

All three classes accept a Bitmap, encoded image bytes, or a Uri:

```csharp
using ColorZXing;
using System.Drawing;

using var bitmap = new Bitmap("color-qr.png");
string fromBitmap = ColorZXingRGB.Decode(bitmap);
string fromBytes = ColorZXingRGB.Decode(File.ReadAllBytes("color-qr.png"));
string fromUrl = ColorZXingRGB.Decode(new Uri("https://example.com/color-qr.png"));
```

The byte overloads above take encoded files such as PNG or JPEG, not raw RGB
pixels. URL downloads are synchronous. Dispose bitmaps you create or receive
from an encoder; decoding a caller-provided bitmap leaves it open.

The shared RGB decoder falls back to independent channel decoding when it cannot
decode all layers, including older symbols with different layer geometry. The
legacy fallback can return a partial concatenated string if a channel fails.
This format has no whole-message checksum or length framing; applications
needing integrity should validate the payload themselves.

## Performance and implementation

Encoding builds three compact QR module matrices in parallel and renders one
combined bitmap. Decoding extracts color planes, detects geometry on the blue
plane once, and samples all three channels at shared module centers. Per-channel
Otsu thresholds classify those samples. Three QR payload/error-correction
decoders still run independently in parallel.

Recorded Release-build measurements on Linux x64, .NET 8.0.31, 400×400 images,
200 ASCII characters, median of seven process runs (30 operations each):

| Operation | Previous implementation | Shared pipeline |
|---|---:|---:|
| Decode | 8.760 ms | 2.530 ms |
| Encode | 7.513 ms | 3.082 ms |
| Decode managed allocations | 1,115 KiB | 514 KiB |
| Encode managed allocations | 2,139 KiB | 214 KiB |

These are workload-specific measurements, not performance guarantees. They
combine algorithm changes and CPU parallelism; they do not isolate either
benefit. Difficult images may incur both the shared attempt and legacy fallback.
Synthetic image tests do not establish camera or print robustness.

See [the algorithm and benchmark notes](https://github.com/HainanZhao/ColorZXing.Net/blob/master/PERFORMANCE.md)
for the math and limitations. No GPU is required.

## Develop and test

```sh
dotnet test ColorZXing.sln -c Release
dotnet run --project Bench/Bench.csproj -c Release
```

Set COLORZXING_BENCH_LENGTH=1200 to benchmark a longer payload. The benchmark
checks round-trip correctness before timing and retains the old implementation
for comparison. Tests cover image formats, Unicode, rotations, mirroring,
concurrency, mixed encoding modes, legacy fallback, and invalid input.

## License

MIT. Built on [ZXing.Net](https://github.com/micjahn/ZXing.Net), licensed under Apache-2.0.
