using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TAOM.Core.Infrastructure;

namespace TAOM.Features.HeroRace.Configuration;

public class RacePositionConfig
{
    public class RacePositionConfigItem
    {
        public string Race { get; set; }

        public float Horizontal { get; set; }

        public float Vertical { get; set; }

        public float Zoom { get; set; }
    }

    public List<RacePositionConfigItem> Items { get; private set; }

    public RacePositionConfig()
    {
        Items = new List<RacePositionConfigItem>();
    }

    private static string GetFileName(string name, IPathService pathService)
    {
        string path = pathService.ConfigPath;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return Path.Combine(path, String.Concat(name, ".json"));
    }

    public static RacePositionConfig LoadConfig(string name)
    {
        var pathService = IoC.Resolve<IPathService>();
        return LoadConfig(name, pathService);
    }

    public static RacePositionConfig LoadConfig(string name, IPathService pathService)
    {
        return LoadConfig(name, pathService, out _);
    }

    /// <summary>
    /// Reads, parses and SANITISES the named config. Parse success is not validation success:
    /// see <see cref="RacePositionConfigValidator"/> for why a syntactically valid NaN offset is
    /// the failure mode this path exists to stop. Never returns null and never throws.
    /// </summary>
    /// <param name="warnings">
    /// One entry per row the validator dropped or collapsed. Callers that have a logger should
    /// emit these; a silent drop is indistinguishable from a race that was never configured.
    /// </param>
    public static RacePositionConfig LoadConfig(
        string name,
        IPathService pathService,
        out IReadOnlyList<string> warnings)
    {
        RacePositionConfig config = null;

        try
        {
            string jsonString = File.ReadAllText(GetFileName(name, pathService));

            if (jsonString != null)
            {
                config = JsonConvert.DeserializeObject<RacePositionConfig>(jsonString);
            }
        }
        catch (FileNotFoundException)
        {
            // A config that has never been authored is the normal state for most races, not an
            // error worth a log line on every load.
            warnings = Array.Empty<string>();
            return new RacePositionConfig();
        }
        catch (DirectoryNotFoundException)
        {
            warnings = Array.Empty<string>();
            return new RacePositionConfig();
        }
        catch (Exception ex)
        {
            TaleWorlds.Library.Debug.PrintError(ex.Message);
            warnings = new[] { $"{name}: could not be read ({ex.GetType().Name}: {ex.Message}). Using vanilla framing." };
            return new RacePositionConfig();
        }

        return RacePositionConfigValidator.Sanitize(config, name, out warnings);
    }

    /// <summary>
    /// Writes a config back to <c>ModuleData/configs</c>, keeping the previous file as <c>.prev</c>.
    ///
    /// <para>Written through a temporary file and swapped in, rather than truncating the target in
    /// place. This is reachable from the in-game tuner, so the target is a file that SHIPS: a crash
    /// or a full disk part-way through a plain <c>File.WriteAllText</c> would leave a truncated
    /// config that the loader then treats as unparseable and silently replaces with vanilla framing
    /// for every race. The <c>.prev</c> copy also gives the player a way back after a load-then-save
    /// cycle, which drops any row the validator rejected and rewrites the file's formatting.</para>
    /// </summary>
    public static void WriteConfig(string name, RacePositionConfig config, IPathService pathService)
    {
        if (StageWrite(name, config, pathService))
        {
            CommitStagedWrite(name, pathService);
        }
    }

    /// <summary>
    /// Serialises <paramref name="config"/> to <c>&lt;name&gt;.json.tmp</c> without touching the live
    /// file. Returns false when there is nothing to write. Split from the swap so a caller writing
    /// several related files can stage them ALL before committing any: the configs are read back as a
    /// set, and a half-applied save leaves new data in one surface and old data in the other.
    /// </summary>
    public static bool StageWrite(string name, RacePositionConfig config, IPathService pathService)
    {
        if (config == null || pathService == null)
        {
            return false;
        }

        File.WriteAllText(GetFileName(name, pathService) + ".tmp", JsonConvert.SerializeObject(config));
        return true;
    }

    /// <summary>Swaps a staged temp file into place, keeping the previous file as <c>.prev</c>.</summary>
    public static void CommitStagedWrite(string name, IPathService pathService)
    {
        string path = GetFileName(name, pathService);
        string temp = path + ".tmp";

        if (!File.Exists(temp))
        {
            return;
        }

        if (File.Exists(path))
        {
            // Replace is atomic on NTFS and produces the backup in the same operation.
            // ignoreMetadataErrors keeps a mismatched ACL or stream from failing the swap.
            File.Replace(temp, path, path + ".prev", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }
}
