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
        if (config == null || pathService == null)
        {
            return;
        }

        string path = GetFileName(name, pathService);
        string temp = path + ".tmp";
        string backup = path + ".prev";

        File.WriteAllText(temp, JsonConvert.SerializeObject(config));

        if (File.Exists(path))
        {
            // Replace is atomic on NTFS and produces the backup in the same operation.
            // ignoreMetadataErrors keeps a mismatched ACL or stream from failing the swap.
            File.Replace(temp, path, backup, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }
}
