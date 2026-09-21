namespace ColorZXing
{
    public sealed class ColorZXingDecodeResult
    {
        internal ColorZXingDecodeResult(string text, ColorZXingFormat format)
        {
            Text = text;
            Format = format;
        }

        public string Text { get; }

        public ColorZXingFormat Format { get; }
    }
}
