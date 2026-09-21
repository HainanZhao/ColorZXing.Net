using System.Drawing;
using System.Reflection;
using ZXing.Common;

namespace ColorZXing.QrValidation;

public interface IManagedQrCoreAdapter
{
    string Name { get; }
    bool IsAvailable { get; }
    bool TryDecode(Bitmap bitmap, out string value);
    bool TryDecodeSampled(BitMatrix sampled, out string value);
    bool TryDecodeSampled(byte[] rowMajorModules, int width, int height, out string value);
    bool TryEncode(QrCase qrCase, out object? encoded);
}

/// <summary>
/// Optional bridge for the concurrently-developed core. No compile-time reference or API shape is assumed.
/// Set COLORZXING_QR_CORE_TYPE to an assembly-qualified type. Decoder discovery prefers
/// DecodeSampled(BitMatrix), then Decode(BitMatrix), then DecodeSampled(byte[], int, int),
/// and finally Decode(byte[], int, int). Bitmap/Encode(string) remain optional compatibility paths.
/// </summary>
public sealed class ReflectionManagedQrCoreAdapter : IManagedQrCoreAdapter
{
    private readonly Type? _type;
    private readonly MethodInfo? _decode;
    private readonly MethodInfo? _sampledMatrixDecode;
    private readonly MethodInfo? _sampledBytesDecode;
    private readonly MethodInfo? _encode;

    public ReflectionManagedQrCoreAdapter(Type? type = null)
    {
        var configured = Environment.GetEnvironmentVariable("COLORZXING_QR_CORE_TYPE");
        _type = type ?? (configured is null ? null : Type.GetType(configured, throwOnError: false));
        _decode = _type?.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Bitmap) }, null);
        _sampledMatrixDecode = Find(new[] { "DecodeSampled", "Decode" }, typeof(BitMatrix));
        _sampledBytesDecode = Find(new[] { "DecodeSampled", "Decode" }, typeof(byte[]), typeof(int), typeof(int));
        _encode = _type?.GetMethod("Encode", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
    }

    public string Name => _type?.FullName ?? "managed-core (reflection unavailable)";
    public bool IsAvailable => _decode is not null || _sampledMatrixDecode is not null || _sampledBytesDecode is not null;

    public bool TryDecode(Bitmap bitmap, out string value)
    {
        value = string.Empty;
        if (_decode is null) return false;
        try { value = _decode.Invoke(null, new object?[] { bitmap }) as string ?? string.Empty; return true; }
        catch (TargetInvocationException) { return false; }
        catch (ArgumentException) { return false; }
    }

    public bool TryDecodeSampled(BitMatrix sampled, out string value)
        => TryInvoke(_sampledMatrixDecode, new object?[] { sampled }, out value);

    public bool TryDecodeSampled(byte[] rowMajorModules, int width, int height, out string value)
        => TryInvoke(_sampledBytesDecode, new object?[] { rowMajorModules, width, height }, out value);

    public bool TryEncode(QrCase qrCase, out object? encoded)
    {
        encoded = null;
        if (_encode is null) return false;
        try { encoded = _encode.Invoke(null, new object?[] { qrCase.Payload }); return true; }
        catch (TargetInvocationException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private MethodInfo? Find(string[] names, params Type[] parameterTypes)
    {
        if (_type is null) return null;
        foreach (var name in names)
        {
            var method = _type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            if (method is not null) return method;
        }
        return null;
    }

    private static bool TryInvoke(MethodInfo? method, object?[] arguments, out string value)
    {
        value = string.Empty;
        if (method is null) return false;
        try
        {
            var result = method.Invoke(null, arguments);
            value = result as string ?? result?.GetType().GetProperty("Text")?.GetValue(result) as string ?? string.Empty;
            return true;
        }
        catch (TargetInvocationException) { return false; }
        catch (ArgumentException) { return false; }
    }
}
