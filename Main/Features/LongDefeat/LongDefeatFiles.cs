using System.IO;

namespace TAOM.Features.LongDefeat;

/// <summary>The one way the feature writes a file that something else may read while the game runs: a temp file, then a swap.</summary>
internal static class LongDefeatFiles
{
    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, creating its folder, so a crash never leaves half a document.</summary>
    internal static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }
}
