using System;
using System.IO;
using System.Text;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Plugin.Diagnostics;

internal sealed class JsonlEventWriter : IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;

    internal JsonlEventWriter(string outputDirectory, string sessionId)
    {
        Directory.CreateDirectory(outputDirectory);
        FilePath = Path.Combine(outputDirectory, $"{sessionId}.jsonl");
        var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    internal string FilePath { get; }

    internal void Write(SecurityTestEvent value)
    {
        lock (_sync)
        {
            _writer.WriteLine(SecurityTestEventJson.Serialize(value));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }
}

