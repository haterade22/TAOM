using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace TAOM.Features.XmlMerge;

/// <summary>
/// The engine's <c>MBObjectManager.ApplyXslt</c> (v1.5.3, MBObjectManager.cs:980-991) line for line, except that the
/// compiled stylesheet is cached. The engine compiles every stylesheet on every call; TAOM's lords.xslt alone takes 136
/// to 212 ms to compile. The cache is keyed by the path string exactly as the engine receives it and re-validated by
/// the file's last-write time (UTC) and length, so a stylesheet edited between two loads of one process recompiles.
/// Compilation reads the file the engine's way (<c>XmlReader.Create(new StreamReader(path))</c>, default settings and
/// default <c>XsltSettings</c>), so a missing or malformed stylesheet throws what the engine throws, and nothing broken
/// is ever cached. <c>XslCompiledTransform.Transform</c> is thread-safe once loaded; the lock guards the dictionary and
/// the counters.
///
/// <para>Trade-off: a compiled stylesheet stays alive for the process (lords.xslt's holds about 11 MB of managed heap
/// once run), and a reused instance also skips the first-run JIT of its templates, about 1 s for lords.xslt. Not
/// re-validated: stylesheets pulled in by <c>xsl:include</c> or <c>xsl:import</c> (no installed one uses either), and
/// a rewrite that keeps both the length and the last-write time.</para>
/// </summary>
public sealed class XsltTransformCache
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private long _compiles;
    private long _cacheHits;

    /// <summary>Stylesheets compiled since the process started.</summary>
    public long Compiles
    {
        get { lock (_gate) return _compiles; }
    }

    /// <summary>Applications served by an already compiled stylesheet.</summary>
    public long CacheHits
    {
        get { lock (_gate) return _cacheHits; }
    }

    public XmlDocument Apply(string xsltPath, XmlDocument baseDocument)
    {
        XslCompiledTransform transform = GetOrCompile(xsltPath);

        XmlReader input = new XmlNodeReader(baseDocument);
        XmlDocument output = new XmlDocument(baseDocument.CreateNavigator().NameTable);
        using XmlWriter writer = output.CreateNavigator().AppendChild();
        transform.Transform(input, writer);
        writer.Close();
        return output;
    }

    private XslCompiledTransform GetOrCompile(string xsltPath)
    {
        var file = new FileInfo(xsltPath);
        bool exists = file.Exists;
        DateTime stamp = exists ? file.LastWriteTimeUtc : default;
        long length = exists ? file.Length : -1;

        lock (_gate)
        {
            if (exists && _entries.TryGetValue(xsltPath, out var cached) && cached.Stamp == stamp && cached.Length == length)
            {
                _cacheHits++;
                return cached.Transform;
            }
        }

        // Compiled outside the lock: a missing file throws FileNotFoundException from the StreamReader, as in the engine.
        var transform = new XslCompiledTransform();
        using (var text = new StreamReader(xsltPath))
        using (var stylesheet = XmlReader.Create(text))
        {
            transform.Load(stylesheet);
        }

        lock (_gate)
        {
            _compiles++;
            _entries[xsltPath] = new Entry(transform, stamp, length);
        }
        return transform;
    }

    private sealed class Entry
    {
        public Entry(XslCompiledTransform transform, DateTime stamp, long length)
        {
            Transform = transform;
            Stamp = stamp;
            Length = length;
        }

        public XslCompiledTransform Transform { get; }

        public DateTime Stamp { get; }

        public long Length { get; }
    }
}
