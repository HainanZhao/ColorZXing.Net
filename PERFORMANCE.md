# RGB QR pipeline and performance

ColorZXing stores three ordinary binary QR symbols in the blue, green, and red
components of one image. If `M_c(x,y)` is the black/white module value for
channel `c`, the rendered component is

```text
C_c(x,y) = 255 * (1 - M_c(x,y)).
```

## What can be shared

A QR version `v` has `N = 21 + 4(v - 1)` modules per side. When all three
channels use the same version, their finder, timing, alignment, and module-center
geometry coincide. A photographed module center `(i,j)` therefore has one image
coordinate for every component:

```text
p(i,j) = T(i + 0.5, j + 0.5),
```

where `T` is the perspective transform found from the shared finder/alignment
patterns. The optimized decoder detects `T` once and reads B, G, and R at every
transformed module center in the same loop.

Each sampled component is classified with an Otsu threshold. For threshold `t`,
the selected value maximizes between-class variance:

```text
sigma_b^2(t) = w_0(t) * w_1(t) * (mu_0(t) - mu_1(t))^2.
```

Only the three QR payload decoders remain separate. Each color has different
masked codewords and a different Reed-Solomon syndrome, so three standards-
compatible QR layers cannot be reduced to one Reed-Solomon decode.

The optimized encoder similarly creates three module matrices but renders them
to the output bitmap in one raster pass. It balances chunks by UTF-8 bytes,
never splits a Unicode scalar, and forces a common QR version.

## Measured results

Release build, .NET 8.0.31, Linux x64, 400x400 output, 30 operations per sample.
The table uses the median of seven process runs for a 200-character payload.

| Operation | Previous | Optimized | Change |
|---|---:|---:|---:|
| Decode | 8.760 ms | 2.530 ms | 3.46x faster |
| Encode | 7.513 ms | 3.082 ms | 2.44x faster |
| Decode allocation | 1,115 KiB | 514 KiB | 54% lower |
| Encode allocation | 2,139 KiB | 214 KiB | 90% lower |

For a 1,200-character payload, three runs gave median decode times of 9.464 ms
versus 2.626 ms and median encode times of 11.278 ms versus 4.241 ms.

Run the benchmark with:

```sh
dotnet run --project Bench/Bench.csproj -c Release
COLORZXING_BENCH_LENGTH=1200 dotnet run --project Bench/Bench.csproj -c Release
```

The benchmark keeps the legacy paths internally so the comparison covers the
same process, runtime, image, and payload.

## Why the GPU is not the first optimization

After geometry is known, only `3 * N^2` component samples are needed; typical QR
module matrices are tiny compared with their raster images. Uploading a CPU
bitmap and retrieving three small matrices normally costs more than this loop.
GPU preprocessing becomes attractive for batches or video only when frames are
already GPU-resident. In that case color correction, thresholding, finder
detection, and perspective sampling should remain on the GPU, with only the
module matrices transferred to the CPU decoders.

An alternative custom color-code format could treat each module as a three-bit
symbol and use one new error-correction stream. That would no longer contain
three standards-compatible QR symbols and would require a new capacity table,
mask scoring rules, error model, and decoder. It is a separate format design,
not a safe optimization of this one.

## Conventional QR fast path

`ColorZXingBasic` and `ColorZXingMono` now try a QR-specific path before the
generic barcode reader. It preserves continuous luminance values for adaptive
binarization instead of applying a fixed threshold first. For ordinary black
and white images, SSSE3 extracts one component because all components are
equal. Colored symbols that cannot be decoded from that component are retried
using average RGB luminance. The raw pixel-buffer overload retains the generic
multi-format reader as fallback, preserving non-QR barcode compatibility.

For a 200-character payload and 400x400 image, the median of seven process runs
was 5.744 ms versus 1.810 ms for decode (3.17x faster), and 3.372 ms versus
2.203 ms for encode (1.53x faster). Decode allocations fell from 217 KiB to
195 KiB; encode allocations fell from 883 KiB to 258 KiB. At 1,200 characters,
three runs gave median decode times of 6.840 ms versus 2.387 ms and median
encode times of 5.842 ms versus 5.273 ms.
