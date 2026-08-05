using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// Issue #384 — on-demand widget-tree dump of the current top screen, with real on-screen
/// rectangles. Ported as a console command from the external reference module's always-on
/// diagnostic (TAOM-Career-UX-Upstream-2026-08-05), whose dumps found three UI bugs that
/// code reading did not. On-demand replaces the reference's kill-switch flag files and
/// unbounded logger; the collapsed-tree guard survives as a warning in the output.
/// </summary>
public static class HudLayoutDumpCheats
{
    private const int DefaultMaxNodes = 6000;
    private const string DumpFileName = "taom_hud_layout.log";

    private const string Usage =
        "Format is \"taom.print_hud_layout [maxNodes]\".\n"
        + "Writes the top screen's full widget tree (type, Id, on-screen rectangle, visibility)\n"
        + "to Logs\\" + DumpFileName + " and prints a summary. Run it while the UI you are\n"
        + "measuring is on screen (e.g. mid-battle for the combat HUD).";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_hud_layout", "taom")]
    public static string DumpHudLayout(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, Usage, args =>
        {
            var maxNodes = DefaultMaxNodes;
            if (args.Count > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
                maxNodes = parsed;

            var topScreen = ScreenManager.TopScreen;
            if (topScreen == null) return "No top screen — nothing to dump.";

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine($"HUD layout dump — screen: {topScreen.GetType().Name}, layers: {topScreen.Layers?.Count ?? 0}");

            var nodes = 0;
            var truncated = false;
            var collapsedRoots = 0;

            foreach (var layer in topScreen.Layers ?? (IReadOnlyList<ScreenLayer>)new List<ScreenLayer>())
            {
                if (!(layer is GauntletLayer gauntletLayer)) continue;
                var root = gauntletLayer.UIContext?.Root;
                if (root == null) continue;

                sb.AppendLine($"-- layer '{layer.Name}' ({layer.GetType().Name}) --");
                // Collapsed-tree guard (reference module finding): a not-yet-measured tree
                // dumps all-zero rectangles that read as truth. Warn instead of lying.
                if (root.Size.X < 1f && root.Size.Y < 1f)
                {
                    collapsedRoots++;
                    sb.AppendLine("   WARNING: root measures 0x0 — layout not yet measured; rectangles below are not meaningful.");
                }

                DumpWidget(root, 0, sb, ref nodes, maxNodes, ref truncated);
                if (truncated) break;
            }

            if (truncated)
                sb.AppendLine($"TRUNCATED at {maxNodes} nodes — pass a larger maxNodes to see the rest.");

            Directory.CreateDirectory("Logs");
            var path = Path.Combine("Logs", DumpFileName);
            File.WriteAllText(path, sb.ToString());

            return $"Dumped {nodes} widgets to {Path.GetFullPath(path)}"
                 + (truncated ? $" (TRUNCATED at {maxNodes} — output is incomplete)" : "")
                 + (collapsedRoots > 0 ? $" ({collapsedRoots} layer(s) not yet measured — re-run once the UI is fully up)" : "");
        });

    private static void DumpWidget(Widget widget, int depth, StringBuilder sb, ref int nodes, int maxNodes, ref bool truncated)
    {
        if (truncated) return;
        if (nodes >= maxNodes) { truncated = true; return; }

        nodes++;
        var id = string.IsNullOrEmpty(widget.Id) ? "-" : widget.Id;
        sb.Append(' ', depth * 2)
          .Append(widget.GetType().Name)
          .Append(" id=").Append(id)
          .Append(" x=").Append((int)widget.GlobalPosition.X)
          .Append(" y=").Append((int)widget.GlobalPosition.Y)
          .Append(" w=").Append((int)widget.Size.X)
          .Append(" h=").Append((int)widget.Size.Y)
          .Append(widget.IsVisible ? "" : " HIDDEN")
          .AppendLine();

        foreach (var child in widget.Children)
            DumpWidget(child, depth + 1, sb, ref nodes, maxNodes, ref truncated);
    }
}
