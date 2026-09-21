using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZXing;
using ZXing.QrCode.Internal;
using QrEncoder = ZXing.QrCode.Internal.Encoder;

namespace ColorZXing
{
    /// <summary>
    /// A compressed, binary-safe RGB QR format. It uses the same eight RGB colors and
    /// QR error correction as ColorZXingRGB, but is a distinct framed format.
    /// </summary>
    internal static class CompressedRgbCodec
    {
        private const int FrameHeaderSize = 18;
        private const byte FormatVersion = 1;
        private const byte CompressedFlag = 1;
        private const byte LayerMarker = 0xFF;
        private const int MaximumDecodedBytes = 64 * 1024 * 1024;
        private static readonly byte[] FrameMagic = { (byte)'C', (byte)'Z', (byte)'H', (byte)'D' };
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static Bitmap Encode(string value, int width, int height, int margin)
        {
            return Encode(value, width, height, margin, out _);
        }

        public static Bitmap Encode(string value, int width, int height, int margin, out ColorZXingCompressionInfo info)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return EncodeBytes(Encoding.UTF8.GetBytes(value), width, height, margin, out info);
        }

        public static Bitmap EncodeBytes(byte[] value, int width, int height, int margin)
        {
            return EncodeBytes(value, width, height, margin, out _);
        }

        public static Bitmap EncodeBytes(byte[] value, int width, int height, int margin, out ColorZXingCompressionInfo info)
        {
            var frame = CreateFrame(value, out info);
            return ColorZXingRGB.RenderCombined(EncodeLayers(frame), width, height, margin);
        }

        public static ColorZXingPixelData EncodeRgba(string value, int width, int height, int margin)
        {
            return EncodeRgba(value, width, height, margin, out _);
        }

        public static ColorZXingPixelData EncodeRgba(string value, int width, int height, int margin, out ColorZXingCompressionInfo info)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return EncodeBytesRgba(Encoding.UTF8.GetBytes(value), width, height, margin, out info);
        }

        public static ColorZXingPixelData EncodeBytesRgba(byte[] value, int width, int height, int margin)
        {
            return EncodeBytesRgba(value, width, height, margin, out _);
        }

        public static ColorZXingPixelData EncodeBytesRgba(byte[] value, int width, int height, int margin, out ColorZXingCompressionInfo info)
        {
            var frame = CreateFrame(value, out info);
            return ColorZXingRGB.RenderCombinedRgba(EncodeLayers(frame), width, height, margin);
        }

        public static string Decode(Bitmap bitmap)
        {
            return StrictUtf8.GetString(DecodeBytes(bitmap));
        }

        public static byte[] DecodeBytes(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));
            return DecodePlanes(ColorZXingRGB.GetPlanes(bitmap), bitmap.Width, bitmap.Height);
        }

        public static string DecodeRgba(byte[] rgba, int width, int height)
        {
            return StrictUtf8.GetString(DecodeBytesRgba(rgba, width, height));
        }

        public static byte[] DecodeBytesRgba(byte[] rgba, int width, int height)
        {
            return DecodePlanes(ColorZXingRGB.GetPlanesFromRgba(rgba, width, height), width, height);
        }

        public static string Decode(byte[] encodedImage)
        {
            using var bitmap = Utils.CreateBitmap(encodedImage);
            return Decode(bitmap);
        }

        public static string Decode(Uri url)
        {
            using var bitmap = Utils.DownloadBitmap(url);
            return Decode(bitmap);
        }

        public static bool TryDecode(Bitmap bitmap, out string value)
        {
            try
            {
                value = Decode(bitmap);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException ||
                                              exception is ArgumentException ||
                                              exception is DecoderFallbackException)
            {
                value = string.Empty;
                return false;
            }
        }

        public static bool TryDecodeRgba(byte[] rgba, int width, int height, out string value)
        {
            try
            {
                value = DecodeRgba(rgba, width, height);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException ||
                                              exception is ArgumentException ||
                                              exception is DecoderFallbackException)
            {
                value = string.Empty;
                return false;
            }
        }

        public static ColorZXingCompressionInfo Analyze(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            CreateFrame(Encoding.UTF8.GetBytes(value), out var info);
            return info;
        }

        public static ColorZXingCompressionInfo AnalyzeBytes(byte[] value)
        {
            CreateFrame(value, out var info);
            return info;
        }

        private static QRCode[] EncodeLayers(byte[] frame)
        {
            var chunks = SplitFrame(frame, 3, 0xF0);
            var codes = new QRCode[3];
            Parallel.For(0, codes.Length, channel =>
            {
                codes[channel] = QrEncoder.encode(
                    Encoding.Latin1.GetString(chunks[channel]),
                    ErrorCorrectionLevel.L,
                    BinaryHints());
            });

            var version = codes.Max(code => code.Version.VersionNumber);
            Parallel.For(0, codes.Length, channel =>
            {
                if (codes[channel].Version.VersionNumber == version)
                    return;
                var hints = BinaryHints();
                hints[EncodeHintType.QR_VERSION] = version;
                codes[channel] = QrEncoder.encode(
                    Encoding.Latin1.GetString(chunks[channel]),
                    ErrorCorrectionLevel.L,
                    hints);
            });
            return codes;
        }

        internal static IDictionary<EncodeHintType, object> BinaryHints()
        {
            return new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.CHARACTER_SET] = Encoding.Latin1.WebName
            };
        }

        internal static byte[][] SplitFrame(byte[] frame, int layerCount, byte formatMarker)
        {
            if (layerCount < 1 || layerCount > 15)
                throw new ArgumentOutOfRangeException(nameof(layerCount));
            var chunks = new byte[layerCount][];
            var sourceOffset = 0;
            for (var channel = 0; channel < chunks.Length; channel++)
            {
                var channelsRemaining = chunks.Length - channel;
                var bytesRemaining = frame.Length - sourceOffset;
                var payloadLength = (bytesRemaining + channelsRemaining - 1) / channelsRemaining;
                var chunk = new byte[payloadLength + 2];
                chunk[0] = LayerMarker;
                chunk[1] = (byte)(formatMarker | channel);
                Buffer.BlockCopy(frame, sourceOffset, chunk, 2, payloadLength);
                chunks[channel] = chunk;
                sourceOffset += payloadLength;
            }
            return chunks;
        }

        private static byte[] DecodePlanes(byte[][] planes, int width, int height)
        {
            var layers = ColorZXingRGB.TryDecodeSharedLayers(planes, width, height)
                         ?? ColorZXingRGB.DecodePlanesIndependently(planes, width, height);
            return DecodeLayerTexts(layers, 0xF0);
        }

        internal static byte[] DecodeLayerTexts(string[] layers, byte formatMarker)
        {
            using var stream = new MemoryStream();
            for (var channel = 0; channel < layers.Length; channel++)
            {
                if (string.IsNullOrEmpty(layers[channel]))
                    throw new InvalidDataException($"RGB layer {channel} could not be decoded.");
                var bytes = Encoding.Latin1.GetBytes(layers[channel]);
                if (bytes.Length < 2 || bytes[0] != LayerMarker || bytes[1] != (byte)(formatMarker | channel))
                    throw new InvalidDataException("This image is not a ColorZXing compressed RGB symbol.");
                stream.Write(bytes, 2, bytes.Length - 2);
            }
            return ReadFrame(stream.ToArray());
        }

        internal static byte[] CreateFrame(byte[] value, out ColorZXingCompressionInfo info)
        {
            return CreateFrame(value, allowCompression: true, out info);
        }

        internal static byte[] CreateFrame(byte[] value, bool allowCompression, out ColorZXingCompressionInfo info)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length > MaximumDecodedBytes)
                throw new ArgumentException($"Compressed RGB payloads cannot exceed {MaximumDecodedBytes} bytes.", nameof(value));

            var compressed = allowCompression ? Compress(value) : Array.Empty<byte>();
            var useCompression = allowCompression && compressed.Length < value.Length;
            var payload = useCompression ? compressed : value;
            var frame = new byte[checked(FrameHeaderSize + payload.Length)];
            FrameMagic.CopyTo(frame, 0);
            frame[4] = FormatVersion;
            frame[5] = useCompression ? CompressedFlag : (byte)0;
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(6, 4), value.Length);
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(10, 4), payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(14, 4), ComputeCrc32(value));
            payload.CopyTo(frame, FrameHeaderSize);
            info = new ColorZXingCompressionInfo(value.Length, payload.Length, frame.Length, useCompression);
            return frame;
        }

        internal static byte[] ReadFrame(byte[] frame)
        {
            if (frame.Length < FrameHeaderSize || !frame.AsSpan(0, 4).SequenceEqual(FrameMagic))
                throw new InvalidDataException("Compressed RGB frame header is missing or damaged.");
            if (frame[4] != FormatVersion)
                throw new InvalidDataException($"Compressed RGB format version {frame[4]} is not supported.");
            if ((frame[5] & ~CompressedFlag) != 0)
                throw new InvalidDataException("Compressed RGB frame contains unsupported flags.");

            var originalLength = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(6, 4));
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(10, 4));
            var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(14, 4));
            if (originalLength < 0 || originalLength > MaximumDecodedBytes || payloadLength < 0 ||
                frame.Length != FrameHeaderSize + payloadLength)
                throw new InvalidDataException("Compressed RGB frame lengths are invalid.");

            var payload = frame.AsSpan(FrameHeaderSize, payloadLength);
            byte[] result;
            if ((frame[5] & CompressedFlag) != 0)
            {
                result = Decompress(payload, originalLength);
            }
            else
            {
                if (payloadLength != originalLength)
                    throw new InvalidDataException("Uncompressed compressed RGB frame lengths do not match.");
                result = payload.ToArray();
            }

            if (ComputeCrc32(result) != expectedCrc)
                throw new InvalidDataException("Compressed RGB payload checksum failed.");
            return result;
        }

        // Compact LZ block codec designed for identical behavior on desktop and browser WASM.
        // Match tokens use a 10-bit backward offset and a 6-bit length (3..66 bytes).
        private static byte[] Compress(byte[] source)
        {
            if (source.Length == 0)
                return Array.Empty<byte>();

            var output = new List<byte>(source.Length + (source.Length + 7) / 8);
            var lastPosition = new int[1 << 16];
            Array.Fill(lastPosition, -1);
            var position = 0;
            while (position < source.Length)
            {
                var controlIndex = output.Count;
                output.Add(0);
                byte control = 0;
                for (var token = 0; token < 8 && position < source.Length; token++)
                {
                    var matchPosition = -1;
                    var matchLength = 0;
                    if (position + 2 < source.Length)
                    {
                        var hash = Hash(source, position);
                        var candidate = lastPosition[hash];
                        if (candidate >= 0 && position - candidate <= 1024)
                        {
                            var maximum = Math.Min(66, source.Length - position);
                            var length = 0;
                            while (length < maximum && source[candidate + length] == source[position + length])
                                length++;
                            if (length >= 3)
                            {
                                matchPosition = candidate;
                                matchLength = length;
                            }
                        }
                    }

                    var consumed = matchLength >= 3 ? matchLength : 1;
                    if (matchLength >= 3)
                    {
                        control |= (byte)(1 << token);
                        var offset = position - matchPosition;
                        var packed = ((offset - 1) << 6) | (matchLength - 3);
                        output.Add((byte)(packed >> 8));
                        output.Add((byte)packed);
                    }
                    else
                    {
                        output.Add(source[position]);
                    }

                    var end = Math.Min(position + consumed, source.Length - 2);
                    for (var update = position; update < end; update++)
                        lastPosition[Hash(source, update)] = update;
                    position += consumed;
                }
                output[controlIndex] = control;
            }
            return output.ToArray();
        }

        private static byte[] Decompress(ReadOnlySpan<byte> source, int expectedLength)
        {
            var output = new byte[expectedLength];
            var sourceOffset = 0;
            var outputOffset = 0;
            while (sourceOffset < source.Length)
            {
                var control = source[sourceOffset++];
                for (var token = 0; token < 8 && sourceOffset < source.Length; token++)
                {
                    if ((control & (1 << token)) == 0)
                    {
                        if (outputOffset >= output.Length)
                            throw new InvalidDataException("Compressed RGB payload exceeds its declared length.");
                        output[outputOffset++] = source[sourceOffset++];
                        continue;
                    }

                    if (sourceOffset + 1 >= source.Length)
                        throw new InvalidDataException("Compressed RGB match token is truncated.");
                    var packed = (source[sourceOffset++] << 8) | source[sourceOffset++];
                    var offset = (packed >> 6) + 1;
                    var length = (packed & 0x3F) + 3;
                    if (offset > outputOffset || outputOffset + length > output.Length)
                        throw new InvalidDataException("Compressed RGB match token is invalid.");
                    for (var index = 0; index < length; index++)
                    {
                        output[outputOffset] = output[outputOffset - offset];
                        outputOffset++;
                    }
                }
            }

            if (outputOffset != expectedLength)
                throw new InvalidDataException("Compressed RGB payload length does not match its frame.");
            return output;
        }

        private static int Hash(byte[] source, int offset)
        {
            var value = (uint)(source[offset] << 16 | source[offset + 1] << 8 | source[offset + 2]);
            return (int)((value * 2654435761u) >> 16);
        }

        private static uint ComputeCrc32(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in bytes)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
            }
            return ~crc;
        }
    }
}
