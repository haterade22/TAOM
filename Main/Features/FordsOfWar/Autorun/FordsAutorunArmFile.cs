using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>The parser of the arm file's <c>key=value</c> lines; <see cref="FordsAutorunPlan.Resolve(Func{string, string?}, Func{string, bool}, Func{string, string})"/> reads the file and calls it.</summary>
internal static class FordsAutorunArmFile
{
    internal static FordsAutorunPlan Parse(string path, string text)
    {
        var warnings = new List<string>();
        string? west = null, east = null, ai = null, scene = null, outFolder = null;
        double liveSeconds = FordsAutorunPlan.DefaultLiveSeconds, liveWait = FordsAutorunPlan.DefaultLiveWaitSeconds, wallLimit = FordsAutorunPlan.DefaultWallLimitSeconds;
        var quit = false;
        var westAi = false;
        var speed = 1;
        var controlCheck = 0.0;
        var screenshots = new List<double>();

        foreach (var raw in text.TrimStart('\uFEFF').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var split = line.IndexOf('=');
            if (split <= 0)
            {
                warnings.Add("ignored line without a key: " + line);
                continue;
            }

            var key = line.Substring(0, split).Trim().ToLowerInvariant();
            var value = line.Substring(split + 1).Trim();
            switch (key)
            {
                case "west": west = value; break;
                case "east": east = value; break;
                case "ai": ai = value; break;
                case "scene": scene = value; break;
                case "out": outFolder = value; break;
                case "live_seconds": liveSeconds = Seconds(key, value, liveSeconds, warnings); break;
                case "live_wait_seconds": liveWait = Seconds(key, value, liveWait, warnings); break;
                case "wall_limit_seconds": wallLimit = Seconds(key, value, wallLimit, warnings); break;
                case "quit": quit = Flag(key, value, quit, warnings); break;
                case "west_ai": westAi = Flag(key, value, westAi, warnings); break;
                case "speed": speed = Speed(key, value, speed, warnings); break;
                case "control_check": controlCheck = Seconds(key, value, controlCheck, warnings); break;
                case "screenshots": screenshots = SecondsList(key, value, warnings); break;
                default: warnings.Add("unknown key '" + key + "' ignored"); break;
            }
        }

        if (string.IsNullOrEmpty(outFolder) || !IsAbsolute(outFolder!))
            return FordsAutorunPlan.Unarmed("the arm file " + path + " needs an absolute out=<folder> for the done file");

        if (liveSeconds >= wallLimit)
            warnings.Add("live_seconds=" + FordsAutorunPlan.Num(liveSeconds) + " is not below wall_limit_seconds=" + FordsAutorunPlan.Num(wallLimit)
                + ", so the wall limit ends the match first unless the mission clock runs faster than the wall clock");

        return new FordsAutorunPlan
        {
            Armed = true, Warnings = warnings, ArmPath = path, West = west, East = east, Ai = ai, Scene = scene, OutFolder = outFolder!,
            ControlCheckSeconds = controlCheck, LiveSeconds = liveSeconds, LiveWaitSeconds = liveWait, WallLimitSeconds = wallLimit, QuitWhenDone = quit, WestAi = westAi, ScreenshotSeconds = screenshots, Speed = speed,
        };
    }

    /// <summary>.NET Framework's <c>Path.IsPathRooted</c> throws on a quote, <c>&lt;</c>, <c>&gt;</c> or <c>|</c>; such a value is no folder.</summary>
    private static bool IsAbsolute(string folder)
    {
        try
        {
            return Path.IsPathRooted(folder);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static double Seconds(string key, string value, double fallback, List<string> warnings)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0.0 && !double.IsInfinity(seconds))
            return seconds;

        warnings.Add(key + "='" + value + "' is not a positive number; using " + FordsAutorunPlan.Num(fallback));
        return fallback;
    }

    private static int Speed(string key, string value, int fallback, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed) && FordsGameSpeed.IsLevel(speed))
            return speed;

        warnings.Add(key + "='" + value + "' is not 1, 2, 4 or 10; using " + fallback);
        return fallback;
    }

    private static List<double> SecondsList(string key, string value, List<string> warnings)
    {
        var seconds = new List<double>();
        foreach (var part in value.Split(','))
        {
            if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0.0 && !double.IsInfinity(number))
                seconds.Add(number);
            else
                warnings.Add(key + " ignored '" + part + "': not a positive number");
        }

        seconds.Sort();
        return seconds;
    }

    private static bool Flag(string key, string value, bool fallback, List<string> warnings)
    {
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        warnings.Add(key + "='" + value + "' is not 0 or 1; using " + (fallback ? 1 : 0));
        return fallback;
    }
}
