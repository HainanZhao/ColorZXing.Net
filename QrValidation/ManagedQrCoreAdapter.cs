using System.Drawing;
using System.Reflection;

namespace ColorZXing.QrValidation;

public interface IManagedQrCoreAdapter
{
    string Name { get; }
    bool IsAvailable { get; }
    bool TryDecode(Bitmap bitmap, out string value);
    bool TryEncode(QrCase qrCase, out object? encoded);
}

/// <summary>
/// Optional bridge for the concurrently-developed core. No compile-time reference or API shape is assumed.
/// Set COLORZXING_QR_CORE_TYPE to an assembly-qualified type exposing static Decode(Bitmap) and Encode(string).
/// </summary>
public sealed class ReflectionManagedQrCoreAdapter : IManagedQrCoreAdapter
{
    private readonly Type? _type;
    private readonly MethodInfo? _decode;
    private readonly MethodInfo? _encode;

    public ReflectionManagedQrCoreAdapter(Type? type = null)
    {
        var configured = Environment.GetEnvironmentVariable("COLORZXING_QR_CORE_TYPE");
        _type = type ?? (configured is null ? null : Type.GetType(configured, throwOnError: false));
        _decode = _type?.GetMethod("Decode", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Bitmap) }, null);
        _encode = _type?.GetMethod("Encode", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
    }

    public string Name => _type?.FullName ?? "managed-core (reflection unavailable)";
    public bool IsAvailable => _decode is not null && _encode is not null;

    public bool TryDecode(Bitmap bitmap, out string value)
    {
        value = string.Empty;
        if (_decode is null) return false;
        try { value = _decode.Invoke(null, new object?[] { bitmap }) as string ?? string.Empty; return true; }
        catch (TargetInvocationException) { return false; }
        catch (ArgumentException) { return false; }
    }

    public bool TryEncode(QrCase qrCase, out object? encoded)
    {
        encoded = null;
        if (_encode is null) return false;
        try { encoded = _encode.Invoke(null, new object?[] { qrCase.Payload }); return true; }
        catch (TargetInvocationException) { return false; }
        catch (ArgumentException) { return false; }
    }
}
