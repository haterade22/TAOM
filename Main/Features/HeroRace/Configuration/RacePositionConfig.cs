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
        RacePositionConfig config = null;

        try
        {
            string jsonString = File.ReadAllText(GetFileName(name, pathService));

            if (jsonString != null)
            {
                config = JsonConvert.DeserializeObject<RacePositionConfig>(jsonString);
            }
        }
        catch (Exception ex)
        {
            TaleWorlds.Library.Debug.PrintError(ex.Message);
        }

        return config ?? new RacePositionConfig();
    }

    public static void WriteConfig(string name, RacePositionConfig config)
    {
        if (config != null)
        {
            var pathService = IoC.Resolve<IPathService>();
            string jsonstring = JsonConvert.SerializeObject(config);
            File.WriteAllText(GetFileName(name, pathService), jsonstring);
        }
    }
}
