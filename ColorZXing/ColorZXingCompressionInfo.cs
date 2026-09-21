namespace ColorZXing
{
    public sealed class ColorZXingCompressionInfo
    {
        internal ColorZXingCompressionInfo(int originalBytes, int storedBytes, int framedBytes, bool compressed)
        {
            OriginalBytes = originalBytes;
            StoredBytes = storedBytes;
            FramedBytes = framedBytes;
            IsCompressed = compressed;
        }

        public int OriginalBytes { get; }

        public int StoredBytes { get; }

        public int FramedBytes { get; }

        public bool IsCompressed { get; }

        public int EncodedBytes => FramedBytes + 6;

        public double CompressionSavingsPercent => OriginalBytes == 0
            ? 0
            : (1.0 - (double)StoredBytes / OriginalBytes) * 100.0;

        public double SavingsPercent => OriginalBytes == 0
            ? 0
            : (1.0 - (double)EncodedBytes / OriginalBytes) * 100.0;
    }
}
