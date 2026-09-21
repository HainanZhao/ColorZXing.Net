using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ColorZXing.QrValidation;

namespace NUnitTest;

[TestFixture]
public sealed class QrBaselineCorpusTests
{
    public static IEnumerable<TestCaseData> ModelCases => QrDatasets.Model2.Select(c => new TestCaseData(c).SetName(c.Name));
    public static IEnumerable<TestCaseData> MaskCases => QrDatasets.Masks.Select(c => new TestCaseData(c).SetName(c.Name));

    [TestCaseSource(nameof(ModelCases))]
    public void Baseline_round_trips_model2_versions(QrCase qrCase)
    {
        using var image = QrBaseline.Encode(qrCase);
        Assert.That(QrBaseline.Decode(image.Bitmap), Is.EqualTo(qrCase.Payload));
        Assert.That((image.Dimension - 17) / 4, Is.EqualTo(qrCase.Version));
    }

    [TestCaseSource(nameof(MaskCases))]
    public void Baseline_round_trips_forced_masks(QrCase qrCase)
    {
        // Keep several pixels per module so blur exercises sampling tolerance rather than deleting a module.
        using var image = QrBaseline.Encode(qrCase, moduleSize: 8);
        Assert.That(QrBaseline.Decode(image.Bitmap), Is.EqualTo(qrCase.Payload));
    }

    [Test]
    public void Baseline_handles_rotations()
    {
        var qrCase = QrDatasets.Model2[1];
        using var image = QrBaseline.Encode(qrCase);
        foreach (var rotation in new[] { RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipNone })
        {
            using var transformed = QrImageTransformations.Rotate(image.Bitmap, rotation);
            Assert.That(QrBaseline.Decode(transformed, tryRotations: true), Is.EqualTo(qrCase.Payload), rotation.ToString());
        }
    }

    [Test]
    public void Baseline_handles_adverse_but_decodable_images()
    {
        var qrCase = QrDatasets.Masks[0];
        using var image = QrBaseline.Encode(qrCase);
        using var gradient = QrImageTransformations.Gradient(image.Bitmap, 42);
        using var noise = QrImageTransformations.Noise(image.Bitmap, 7);
        using var blur = QrImageTransformations.Blur(image.Bitmap, 1);
        using var jpeg = QrImageTransformations.JpegRoundTrip(image.Bitmap, 88);
        using var affine = QrImageTransformations.AffinePerspectiveLike(image.Bitmap);

        Assert.That(QrBaseline.Decode(gradient), Is.EqualTo(qrCase.Payload), "gradient");
        Assert.That(QrBaseline.Decode(noise), Is.EqualTo(qrCase.Payload), "noise");
        Assert.That(QrBaseline.Decode(blur), Is.EqualTo(qrCase.Payload), "blur");
        Assert.That(QrBaseline.Decode(jpeg), Is.EqualTo(qrCase.Payload), "jpeg");
        Assert.That(QrBaseline.Decode(affine), Is.EqualTo(qrCase.Payload), "affine");
    }

    [Test]
    public void Error_correction_recovers_sparse_corruption()
    {
        var qrCase = QrDatasets.Layers[0];
        using var image = QrBaseline.Encode(qrCase);
        using var corrupted = QrImageTransformations.CorrectableCorruption(image, 6);
        Assert.That(QrBaseline.Decode(corrupted), Is.EqualTo(qrCase.Payload));
    }
}

[TestFixture]
public sealed class ManagedQrCoreAdapterTests
{
    [Test]
    [Explicit("Runs only when COLORZXING_QR_CORE_TYPE points at the new core adapter type.")]
    public void Reflection_adapter_is_opt_in()
    {
        var adapter = new ReflectionManagedQrCoreAdapter();
        if (!adapter.IsAvailable) Assert.Ignore("Managed core reflection adapter is not configured.");
        using var image = QrBaseline.Encode(QrDatasets.Model2[0]);
        var luminance = QrBaseline.Preprocess(image.Bitmap);
        var sampled = QrBaseline.DetectAndSample(QrBaseline.Binarize(luminance, image.Bitmap.Width, image.Bitmap.Height));
        var decoded = adapter.TryDecodeSampled(sampled, out var result) || adapter.TryDecode(image.Bitmap, out result);
        Assert.That(decoded, Is.True);
        Assert.That(result, Is.EqualTo(QrDatasets.Model2[0].Payload));
    }
}
