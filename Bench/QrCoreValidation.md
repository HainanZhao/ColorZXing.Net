# QR core validation acceptance matrix

The harness in `QrValidation` uses ZXing.Net **0.16.11 only** as the correctness
oracle. It deliberately has no compile-time dependency on the new managed QR
core. Set `COLORZXING_QR_CORE_TYPE` to an assembly-qualified adapter type when
the core exposes a stable `static string Decode(Bitmap)` and
`static object Encode(string)` surface; the explicit NUnit test and benchmark
can then be enabled without changing this branch.

## Correctness matrix

| Dimension | Required corpus | Acceptance |
| --- | --- | --- |
| QR Model 2 | Versions 1, 4, 10, 20, 23, 36, 40 | Payload, version, and UTF-8/Latin-1 text exact |
| Modes | Numeric, alphanumeric, UTF-8, Latin-1 | Exact payload; no replacement characters |
| Masking | Forced masks 0 through 7 | Exact payload for each mask |
| Geometry | 90/180/270° and mild affine skew | Exact payload |
| Image quality | Horizontal gradient, blur, seeded noise, JPEG Q88 | Exact payload |
| Error correction | Six sparse module corruptions at EC-M | Exact payload |
| Workloads | 1, 3, and 6 independent layers | Every layer exact; report aggregate timing |

## Performance gates

Run `COLORZXING_QR_VALIDATION_ONLY=1 dotnet run -c Release --project Bench`.
The command reports preprocessing, detection/sampling, sampled-module decode,
encoding, allocation, and end-to-end timings separately. A candidate passes when
the median of five warm runs is no more than **1.25x ZXing baseline** for each
stage, allocations are no more than **1.50x baseline**, and all correctness
cases pass. End-to-end 1/3/6-layer timings are compared at each layer count,
not only by their total.

The acceptance anchors recorded from the current host are approximately:

| Stage | Small (v4/64 chars) | Medium (v23/1024 chars) | Large (v36/2400 chars) |
| --- | ---: | ---: | ---: |
| Sampled-module decode | 0.06 ms, 2.7 KiB | 0.30 ms, 23 KiB | 0.70 ms, 51 KiB |
| 400×400 preprocessing | 0.50 ms | — | — |
| 400×400 detector (cached binary) | 0.14 ms | — | — |

Numbers are machine-dependent; the executable output is the authoritative
baseline for a comparison run. No ZXingCpp measurements are included.
