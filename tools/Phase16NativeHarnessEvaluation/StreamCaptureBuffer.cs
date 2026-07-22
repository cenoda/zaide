using System.Text;

namespace Phase16NativeHarnessEvaluation;

public sealed class StreamCaptureBuffer
{
    private readonly int _maxBytes;
    private readonly StringBuilder _builder = new();
    private bool _truncated;

    public StreamCaptureBuffer(int maxBytes)
    {
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        _maxBytes = maxBytes;
    }

    public bool Truncated => _truncated;

    public int CapturedByteCount => Encoding.UTF8.GetByteCount(_builder.ToString());

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var currentBytes = CapturedByteCount;
        if (currentBytes >= _maxBytes)
        {
            _truncated = true;
            return;
        }

        var remaining = _maxBytes - currentBytes;
        var textBytes = Encoding.UTF8.GetBytes(text);
        if (textBytes.Length <= remaining)
        {
            _builder.Append(text);
            return;
        }

        var truncatedText = Encoding.UTF8.GetString(textBytes, 0, remaining);
        _builder.Append(truncatedText);
        _truncated = true;
    }

    public string GetCapturedText() => _builder.ToString();
}
