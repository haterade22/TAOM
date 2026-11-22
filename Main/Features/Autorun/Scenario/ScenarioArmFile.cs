using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TAOM.Core.Validation;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>The parser of the arm file's <c>key=value</c> lines; <see cref="ScenarioPlan.Resolve(Func{string, string?}, Func{string, bool}, Func{string, string})"/> reads the file and calls it.</summary>
internal static class ScenarioArmFile
{
    internal static ScenarioPlan Parse(string path, string text)
    {
        var warnings = new List<string>();
        string? kind = null, outFolder = null;
        string scenario = "-", mission = "-";
        var culture = ScenarioPlan.DefaultCulture;
        var days = ScenarioPlan.DefaultDays;
        var speed = ScenarioPlan.DefaultSpeed;
        double stall = ScenarioPlan.DefaultStallSeconds, wallLimit = ScenarioPlan.DefaultWallLimitSeconds, missionLimit = ScenarioPlan.DefaultMissionLimitSeconds;
        var quit = false;
        var commands = new List<string>();
        var after = new List<string>();
        string offerEvent = "-", choice = "-";

        foreach (var raw in text.TrimStart('﻿').Split('\n'))
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
                case "kind": kind = value; break;
                case "out": outFolder = value; break;
                case "scenario": scenario = value.Length == 0 ? "-" : value; break;
                case "mission": mission = value.Length == 0 ? "-" : value; break;
                case "mission_limit_seconds": missionLimit = Seconds(key, value, missionLimit, warnings); break;
                case "culture": culture = value.Length == 0 ? culture : value; break;
                case "days": days = Days(key, value, days, warnings); break;
                case "speed": speed = Speed(key, value, speed, warnings); break;
                case "stall_seconds": stall = Seconds(key, value, stall, warnings); break;
                case "wall_limit_seconds": wallLimit = Seconds(key, value, wallLimit, warnings); break;
                case "quit": quit = Flag(key, value, quit, warnings); break;
                case "command":
                    if (value.Length > 0) commands.Add(value);
                    break;
                case "after":
                    if (value.Length > 0) after.Add(value);
                    break;
                case "event": offerEvent = value.Length == 0 ? "-" : value; break;
                case "choice": choice = value.Length == 0 ? "-" : value.ToLowerInvariant(); break;
                default: warnings.Add("unknown key '" + key + "' ignored"); break;
            }
        }

        if (kind != ScenarioPlan.KindCampaignNew && kind != ScenarioPlan.KindStoryMission && kind != ScenarioPlan.KindStoryOffer)
            return ScenarioPlan.Unarmed("the arm file " + path + " needs kind=" + ScenarioPlan.KindCampaignNew + ", kind=" + ScenarioPlan.KindStoryMission + " or kind="
                + ScenarioPlan.KindStoryOffer + " (got '" + (kind ?? "") + "')");

        if (string.IsNullOrEmpty(outFolder) || !IsAbsolute(outFolder!))
            return ScenarioPlan.Unarmed("the arm file " + path + " needs an absolute out=<folder> for the done file");

        var story = kind == ScenarioPlan.KindStoryMission;
        if (story && mission == "-")
            return ScenarioPlan.Unarmed("the arm file " + path + " needs mission=<story mission id> for kind=" + ScenarioPlan.KindStoryMission);

        var offer = kind == ScenarioPlan.KindStoryOffer;
        if (offer && offerEvent == "-")
            return ScenarioPlan.Unarmed("the arm file " + path + " needs event=<id> for kind=" + ScenarioPlan.KindStoryOffer);
        if (offer && choice != ScenarioPlan.ChoiceDefend && choice != ScenarioPlan.ChoiceAttack && choice != ScenarioPlan.ChoiceFate)
            return ScenarioPlan.Unarmed("the arm file " + path + " needs choice=" + ScenarioPlan.ChoiceDefend + ", choice=" + ScenarioPlan.ChoiceAttack + " or choice="
                + ScenarioPlan.ChoiceFate + " for kind=" + ScenarioPlan.KindStoryOffer + " (got '" + choice + "')");

        return new ScenarioPlan
        {
            Armed = true, Warnings = warnings, ArmPath = path, Kind = kind, OutFolder = outFolder!, Scenario = scenario, Culture = culture,
            Days = days, Speed = speed, StallSeconds = stall, WallLimitSeconds = wallLimit, Quit = quit,
            MissionId = story ? mission : "-", MissionLimitSeconds = missionLimit, Commands = commands,
            EventId = offer ? offerEvent : "-", Choice = offer ? choice : "-", AfterCommands = offer ? after : new List<string>(),
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

    private static int Days(string key, string value, int fallback, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days >= 1 && days <= ScenarioPlan.MaxDays)
            return days;

        warnings.Add(key + "='" + value + "' is not a whole number from 1 to " + ScenarioPlan.MaxDays + "; using " + fallback);
        return fallback;
    }

    private static float Speed(string key, string value, float fallback, List<string> warnings)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed)
            && FiniteFloatValidator.IsFiniteInRange(speed, 0f, ScenarioPlan.MaxSpeed) && speed > 0f)
            return speed;

        warnings.Add(key + "='" + value + "' is not a number above 0 up to " + ScenarioPlan.Num(ScenarioPlan.MaxSpeed) + "; using " + ScenarioPlan.Num(fallback));
        return fallback;
    }

    private static double Seconds(string key, string value, double fallback, List<string> warnings)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && FiniteFloatValidator.IsFinite(seconds) && seconds > 0.0)
            return seconds;

        warnings.Add(key + "='" + value + "' is not a positive number; using " + ScenarioPlan.Num(fallback));
        return fallback;
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
