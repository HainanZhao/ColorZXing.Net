using System.Drawing;
using System.Text;
using ZXing;
using ZXing.QrCode.Internal;

namespace ColorZXing.QrValidation;

public sealed record QrCase(
    string Name,
    string Payload,
    string CharacterSet,
    int Version,
    ErrorCorrectionLevel ErrorCorrection = null!,
    int? Mask = null)
{
    public System.Text.Encoding Encoding => System.Text.Encoding.GetEncoding(CharacterSet);
}

/// <summary>Deterministic Model 2 corpus. Versions are forced through the ZXing.Net hint.</summary>
public static class QrDatasets
{
    public static IReadOnlyList<QrCase> Model2 { get; } = BuildModel2();

    public static IReadOnlyList<QrCase> Masks { get; } = Enumerable.Range(0, 8)
        .Select(mask => new QrCase($"mask-{mask}", "MASK-" + mask, "UTF-8", 2, ErrorCorrectionLevel.M, mask))
        .ToArray();

    public static IReadOnlyList<QrCase> Layers { get; } = new[]
    {
        new QrCase("layer-1", "layer-one: 0123456789", "UTF-8", 4, ErrorCorrectionLevel.M),
        new QrCase("layer-2", "layer-two: café / latin-1", "ISO-8859-1", 4, ErrorCorrectionLevel.M),
        new QrCase("layer-3", "layer-three: UTF-8 日本語 😀", "UTF-8", 4, ErrorCorrectionLevel.M),
        new QrCase("layer-4", "layer-four: alpha-NUM 123 $%*+-./:", "UTF-8", 4, ErrorCorrectionLevel.M),
        new QrCase("layer-5", "layer-five: 3141592653589793238462643383279", "UTF-8", 4, ErrorCorrectionLevel.M),
        new QrCase("layer-6", "layer-six: affine/gradient/noise fixture", "UTF-8", 4, ErrorCorrectionLevel.M),
    };

    private static IReadOnlyList<QrCase> BuildModel2()
    {
        var numeric = string.Concat(Enumerable.Range(0, 1100).Select(i => (i % 10).ToString()));
        var alpha = string.Concat(Enumerable.Range(0, 1200).Select(i => "A0B1C2D3E4F5-$%*+-./:"[i % 21]));
        const string latinAlphabet = "café déjà vu -";
        const string utf8Alphabet = "日本語é";
        var latin = string.Concat(Enumerable.Range(0, 2200).Select(i => latinAlphabet[i % latinAlphabet.Length]));
        var utf8 = string.Concat(Enumerable.Range(0, 700).Select(i => utf8Alphabet[i % utf8Alphabet.Length]));

        return new[]
        {
            new QrCase("numeric-v1", "0123456789", "UTF-8", 1, ErrorCorrectionLevel.L),
            new QrCase("alphanumeric-v4", alpha[..60], "UTF-8", 4, ErrorCorrectionLevel.M),
            new QrCase("utf8-v10", utf8[..70], "UTF-8", 10, ErrorCorrectionLevel.M),
            new QrCase("latin1-v20", latin[..500], "ISO-8859-1", 20, ErrorCorrectionLevel.L),
            new QrCase("numeric-v23", numeric[..1024], "UTF-8", 23, ErrorCorrectionLevel.L),
            new QrCase("utf8-v36", utf8[..560], "UTF-8", 36, ErrorCorrectionLevel.L),
            new QrCase("latin1-v40", latin[..2000], "ISO-8859-1", 40, ErrorCorrectionLevel.L),
        };
    }
}

public sealed class QrBaselineImage : IDisposable
{
    public QrBaselineImage(Bitmap bitmap, int moduleSize, int quietZone, int dimension, QrCase qrCase)
    {
        Bitmap = bitmap;
        ModuleSize = moduleSize;
        QuietZone = quietZone;
        Dimension = dimension;
        Case = qrCase;
    }

    public Bitmap Bitmap { get; }
    public int ModuleSize { get; }
    public int QuietZone { get; }
    public int Dimension { get; }
    public QrCase Case { get; }
    public void Dispose() => Bitmap.Dispose();
}
