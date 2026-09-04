using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        "Format is \"taom.print_hud_layout [maxNodes] [layerNameFilter]\".\n"
        + "Writes the widget tree (type, Id, on-screen rectangle, visibility) of every layer,\n"
        + "GLOBAL layers included, to Logs\\" + DumpFileName + " and prints a summary. Run it\n"
        + "while the UI you are measuring is on screen (e.g. mid-battle for the combat HUD).\n"
        + "The filter is a case-insensitive substring of the layer name: \"taom.print_hud_layout\n"
        + "Tooltip\" dumps just the tooltip layer, which matters because the layers most worth\n"
        + "reading (Tooltip, MapBar) sort last and a whole-stack dump can truncate before them.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_hud_layout", "taom")]
    public static string DumpHudLayout(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, Usage, args =>
        {
            // A first argument that PARSES as a number is a node cap, even when it is out of range.
            // Falling through to "treat it as a layer name" would silently turn a fat-fingered "0"
            // or "-5" into a filter that matches nothing, and report that as a clean empty dump.
            var maxNodes = DefaultMaxNodes;
            var filterFrom = 0;
            if (args.Count > 0 && int.TryParse(args[0], out var parsed))
            {
                if (parsed <= 0)
                    return $"maxNodes must be a positive integer; got '{args[0]}'.\n{Usage}";

                maxNodes = parsed;
                filterFrom = 1;
            }
            var filter = args.Count > filterFrom ? string.Join(" ", args.Skip(filterFrom)) : null;

            // SortedLayers rather than TopScreen.Layers: its getter appends the GLOBAL layers, and
            // the two most diagnostic ones (Tooltip, MapBar) are global. A top-screen-only walk
            // cannot see them at all, which is what made an earlier tooltip investigation blind.
            var layers = ScreenManager.SortedLayers ?? new List<ScreenLayer>();
            if (layers.Count == 0) return "No layers, nothing to dump.";

            // Refuse before writing when the filter matches nothing, rather than overwriting a
            // useful previous dump with an empty one and reporting success.
            if (filter != null && !layers.Any(l => l?.Name != null
                && l.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return $"No layer name contains '{filter}'. Present: "
                     + string.Join(", ", layers.Where(l => l != null).Select(l => l.Name ?? "-"))
                     + $"\nNothing was written, so {DumpFileName} still holds the previous dump.";
            }

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine($"HUD layout dump. Top screen: {ScreenManager.TopScreen?.GetType().Name ?? "(none)"}, "
                        + $"layers: {layers.Count}{(filter == null ? "" : $", filter: '{filter}'")}");

            var nodes = 0;
            var truncated = false;
            var collapsedRoots = 0;
            var matched = 0;

            foreach (var layer in layers)
            {
                if (layer == null) continue;
                if (filter != null && (layer.Name == null
                    || layer.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;

                matched++;

                if (!(layer is GauntletLayer gauntletLayer))
                {
                    sb.AppendLine($"-- layer '{layer.Name}' ({layer.GetType().Name}) -- not a GauntletLayer, no widget tree.");
                    continue;
                }

                // A null context or root is REPORTED, never skipped. "This layer exists but has no
                // widget tree" is a finding in its own right: it is what an unloaded movie looks
                // like, and silently skipping it hides the answer.
                var root = gauntletLayer.UIContext?.Root;
                if (root == null)
                {
                    sb.AppendLine($"-- layer '{layer.Name}' ({layer.GetType().Name}) -- NO WIDGET TREE "
                                + $"(UIContext {(gauntletLayer.UIContext == null ? "is null" : "present, Root is null")}). "
                                + "No movie is loaded on this layer, so it renders nothing.");
                    continue;
                }

                sb.AppendLine($"-- layer '{layer.Name}' ({layer.GetType().Name}) active={layer.IsActive} --");
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

            return $"Dumped {nodes} widgets from {matched} layer(s) to {Path.GetFullPath(path)}"
                 + (truncated ? $" (TRUNCATED at {maxNodes} — output is incomplete)" : "")
                 + (collapsedRoots > 0 ? $" ({collapsedRoots} layer(s) not yet measured: re-run once the UI is fully up)" : "")
                 + (matched == 0 ? " (no layer name matched the filter)" : "");
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
