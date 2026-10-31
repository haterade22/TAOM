using System.Collections.Generic;
using TAOM.Features.DevConsole;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The bodies of the two Fords of War console commands, apart from the attributed entry points so they run in a test with a
/// catalog, a prompt and a session instead of the engine. Replies are plain English: console output is not localized
/// (docs/features/dev-console.md).
/// </summary>
internal static class FordsOfWarCommands
{
    /// <summary>The most gold one <c>taom.fords_gold</c> call adds; the purse is capped at MaxGold anyway.</summary>
    internal const int MaxGrant = 99999;

    /// <summary>Starts a match without the prompts: <c>[west] [east] [aiProfile] [scene]</c>, each optional.</summary>
    internal static string Start(IReadOnlyList<string> args, FordsConfigCatalog catalog, FordsSetupPrompt prompt)
    {
        if (!FordsSetupChoices.TryResolve(catalog, Arg(args, 0), Arg(args, 1), Arg(args, 2), Arg(args, 3), out var setup, out var error))
            return error;

        if (!prompt.Start(setup))
            return "Fords of War could not start (no TAOM.Client, or the load failed); the log and the main menu notice say why.";

        return $"Starting Fords of War: {setup.WestFaction} against {setup.EastFaction} on {setup.SceneName}, AI profile {setup.AiProfile}, {setup.LaneCount} lanes.";
    }

    /// <summary>Adds gold to the human commander during a match.</summary>
    internal static string Gold(IReadOnlyList<string> args, FordsSession session)
    {
        if (!session.IsActive)
            return "No Fords of War match is running.";

        if (!DevConsoleArgs.TryParseCount(args.Count > 0 ? args[0] : "", 1, MaxGrant, out var amount, out var error))
            return error;

        var added = session.AddGold(amount);
        var balance = session.GetView()?.Gold ?? 0;
        return added == amount
            ? $"Added {added} gold. You now hold {balance}."
            : $"Added {added} gold (the purse is capped). You now hold {balance}.";
    }

    private static string? Arg(IReadOnlyList<string> args, int index) => index < args.Count ? args[index] : null;
}
