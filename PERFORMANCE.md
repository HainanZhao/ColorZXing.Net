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

## Optional RGB compression

The existing RGB symbol has eight possible module colors, so its physical
alphabet carries exactly `log2(8) = 3` bits per module before QR structure and
error correction. A lossless format cannot exceed that bound for arbitrary
input without adding more reliably distinguishable colors or changing the
module grid.

`ColorZXingRGB` can instead reduce the number of input bits when callers pass
`compressed: true`. It applies a managed LZ block codec to UTF-8 data and keeps it only
when it is smaller. A versioned 18-byte frame records compression flags,
original and stored lengths, and CRC-32; two marker bytes on each color layer
identify channel order and force binary QR encoding. Each layer still receives
normal QR Reed-Solomon protection and the rendered symbol still uses the same
eight RGB colors.

This is most effective for JSON, XML, logs, repeated prose, and other structured
content. Encrypted, pre-compressed, and random-looking text uses raw storage and pays the
24-byte frame/layer overhead. The compressed wire format is deliberately
separate and requires the same flag on `ColorZXingRGB.Decode`.

In one Release run of the benchmark's repeated 1,200-character payload, the
codec stored 68 bytes, saved 92.3% after framing/layer markers, and reduced the
native symbol from 69 to 33 modules/pixels per side. It encoded in 2.350 ms and
decoded in 2.020 ms. This illustrates a highly compressible workload rather
than a universal capacity multiplier; the Bench project prints current results.

## Six-layer spectrum muxing

`ColorZXingHighDensity` raises the physical alphabet from 8 to 64 colors by
multiplexing two binary QR layers into each component. For a channel's low and
high bitplanes, the rendered value is

```text
intensity = 85 * low + 170 * high
```

which produces the uniform levels `0`, `85`, `170`, and `255`. Uniform spacing
maximizes the minimum one-dimensional decision margin: an ideal sample can move
up to 42 intensity units before crossing a threshold. Across RGB this carries
six raw bits per module, twice the three-layer format's physical payload.

During decoding, the 2nd and 98th percentile module samples estimate black and
white separately for each channel. A sample `x` is normalized as

```text
normalized = clamp((x - black) * 255 / (white - black), 0, 255)
level = round(normalized / 85)
```

The two bits are recovered from `level`, then six ordinary QR decoders apply
their own Reed-Solomon correction. Tests cover per-channel black offsets,
unequal white levels, ±7 intensity noise, rotation, and JPEG at a large module
scale. The smaller 42-unit margin makes this mode more capture-sensitive than
binary-channel RGB, so increased capacity is not free.

For a deterministic, poorly compressible 1,200-character payload, one Release
run reduced the native symbol from 77 to 61 modules/pixels per side. The full
six-layer pipeline encoded the benchmark payload in 3.275 ms and decoded it in
2.389 ms. At the maximum QR version, the raw layer capacity approaches twice
that of the three-layer format; QR headers, framing, version steps, and error
correction make the realized gain payload-dependent.

## Why the GPU is not the first optimization

After geometry is known, only `3 * N^2` component samples are needed; typical QR
module matrices are tiny compared with their raster images. Uploading a CPU
bitmap and retrieving three small matrices normally costs more than this loop.
GPU preprocessing becomes attractive for batches or video only when frames are
already GPU-resident. In that case color correction, thresholding, finder
detection, and perspective sampling should remain on the GPU, with only the
module matrices transferred to the CPU decoders.

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
