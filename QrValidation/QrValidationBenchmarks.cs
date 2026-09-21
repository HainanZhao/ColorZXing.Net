using System.Diagnostics;
using System.Drawing;

namespace ColorZXing.QrValidation;

public sealed record QrTiming(string Stage, string Case, int Layers, double Milliseconds, double AllocatedKiB);

public static class QrValidationBenchmarks
{
    public static IReadOnlyList<QrTiming> Run(TextWriter output, int iterations = 10)
    {
        var rows = new List<QrTiming>();
        foreach (var qrCase in QrDatasets.Model2.Take(3))
        {
            using var image = QrBaseline.Encode(qrCase, 4);
            var luminance = QrBaseline.Preprocess(image.Bitmap);
            var binary = QrBaseline.Binarize(luminance, image.Bitmap.Width, image.Bitmap.Height);
            var sampled = QrBaseline.DetectAndSample(binary);
            Add(rows, output, "preprocess", qrCase.Name, 1, iterations, () => QrBaseline.Preprocess(image.Bitmap));
            Add(rows, output, "detection+sampling", qrCase.Name, 1, iterations, () => QrBaseline.DetectAndSample(binary));
            Add(rows, output, "sampled-module-decode", qrCase.Name, 1, iterations, () => QrBaseline.DecodeSampled(sampled));
            Add(rows, output, "encoding", qrCase.Name, 1, iterations, () => QrBaseline.Encode(qrCase, 4));
            Add(rows, output, "end-to-end", qrCase.Name, 1, iterations, () => QrBaseline.Decode(image.Bitmap));
        }

        foreach (var layers in new[] { 1, 3, 6 })
        {
            var images = QrDatasets.Layers.Take(layers).Select(c => QrBaseline.Encode(c, 4)).ToArray();
            try
            {
                Add(rows, output, "end-to-end", $"{layers}-layer", layers, iterations, () =>
                {
                    foreach (var image in images) _ = QrBaseline.Decode(image.Bitmap);
                    return layers;
                });
            }
            finally { foreach (var image in images) image.Dispose(); }
        }
        return rows;
    }

    private static void Add(List<QrTiming> rows, TextWriter output, string stage, string name, int layers,
        int iterations, Func<object> operation)
    {
        _ = operation();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        object? result = null;
        for (var i = 0; i < iterations; i++) result = operation();
        watch.Stop();
        var timing = new QrTiming(stage, name, layers, watch.Elapsed.TotalMilliseconds / iterations,
            (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations / 1024);
        rows.Add(timing);
        output.WriteLine($"QR {stage,-22} {name,-18} layers={layers} {timing.Milliseconds,8:F3} ms/op {timing.AllocatedKiB,8:F1} KiB/op");
        if (result is QrBaselineImage image) image.Dispose();
    }
}
