using System;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The best run of each fortress, one small JSON file: <c>{ "helms": { "waves": 4, "kills": 321 } }</c>. Nothing here throws, because a
/// mission callback must not die on a full disk; a failed write is reported to the caller.
/// </summary>
internal sealed class LongDefeatBestRunStore
{
    internal const string FileName = "long_defeat_best.json";

    private readonly string _directory;

    internal LongDefeatBestRunStore(string directory) => _directory = directory;

    internal string FilePath => Path.Combine(_directory, FileName);

    /// <summary>Null when the file is missing or unreadable, or the row is not two whole, non-negative numbers. A NaN or a fraction is rejected, never rounded.</summary>
    internal BestRun? Load(string fortressId)
    {
        try
        {
            return ReadRow(ReadDocument()?[fortressId]);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Writes the row, keeping every other fortress's row, then replaces the file in one step.</summary>
    internal bool TrySave(string fortressId, BestRun run, out string? error)
    {
        try
        {
            var document = ReadDocument() ?? new JObject();
            document[fortressId] = new JObject { ["waves"] = run.Waves, ["kills"] = run.Kills };
            LongDefeatFiles.WriteAtomic(FilePath, document.ToString(Newtonsoft.Json.Formatting.Indented));
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private JObject? ReadDocument()
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            return JToken.Parse(File.ReadAllText(FilePath)) as JObject;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static BestRun? ReadRow(JToken? row)
    {
        if (row is not JObject o || !WholeNumber(o["waves"], out var waves) || !WholeNumber(o["kills"], out var kills))
            return null;

        return new BestRun(waves, kills);
    }

    private static bool WholeNumber(JToken? token, out int value)
    {
        value = 0;
        if (token == null || token.Type != JTokenType.Integer)
            return false;

        try
        {
            var wide = token.Value<long>();
            if (wide < 0 || wide > int.MaxValue)
                return false;

            value = (int)wide;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
