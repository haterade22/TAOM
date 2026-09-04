using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// On-demand dump of the engine's input gates, for the "no tooltips anywhere" class of report.
///
/// Written for a specific failure that static reading got wrong once already: the campaign-map
/// resource bar showed no hover tooltips and Alt showed no party nameplates, both at once. Those two
/// ride different gates (focus-gated keyboard vs hit-test-gated mouse), so the only cheap way to tell
/// a focus fault from a mouse fault from a widget fault is to read the live state at the moment the
/// symptom is on screen.
///
/// Appends rather than overwrites: the useful artefact is several dumps from one session (before the
/// symptom, after it) in one file, which is what identifies the action that caused it.
/// </summary>
public static class InputStateDumpCheats
{
    private const string DumpFileName = "taom_input_state.log";

    private const string Usage =
        "Format is \"taom.print_input_state [label]\".\n"
        + "Appends the live focus/input state of every screen layer to Logs\\" + DumpFileName + "\n"
        + "and prints the verdict. Run it once with tooltips working and again once they are dead;\n"
        + "the optional label is written into the entry so the two are easy to tell apart.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_input_state", "taom")]
    public static string PrintInputState(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, Usage, args =>
        {
            var label = args.Count > 0 ? string.Join(" ", args) : "(no label)";
            var snapshot = Capture();
            var verdicts = InputStateDiagnosis.Build(snapshot);

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine("================================================================");
            sb.AppendLine($"taom.print_input_state | {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} | {label}");
            sb.AppendLine($"TopScreen    : {Or(snapshot.TopScreenTypeName, "(none)")}");
            sb.AppendLine($"FocusedLayer : {Or(snapshot.FocusedLayerName, "(null)")}");
            sb.AppendLine($"Layers       : {snapshot.Layers.Count}");
            sb.AppendLine();

            foreach (var verdict in verdicts)
                sb.AppendLine(verdict);

            sb.AppendLine();
            // "focusable" is IsFocusLayer, which means "may hold focus", not "holds it". The layer
            // that actually holds it is marked at the end of its row, and mislabelling this column
            // invites reading a row as focused when it is merely eligible.
            sb.AppendLine("name                           type                           ord  act focusable hit keys mouse  mask");
            foreach (var layer in snapshot.Layers)
            {
                sb.Append(Pad(layer.Name, 30)).Append(' ')
                  .Append(Pad(layer.TypeName, 30)).Append(' ')
                  .Append(layer.Order.ToString().PadLeft(4)).Append(' ')
                  .Append(Flag(layer.IsActive)).Append("   ")
                  .Append(Flag(layer.IsFocusCandidate)).Append("         ")
                  .Append(Flag(layer.IsHitThisFrame)).Append("   ")
                  .Append(Flag(layer.KeysAllowed)).Append("    ")
                  .Append(Flag(layer.MouseButtonAllowed)).Append("     ")
                  .Append(layer.InputUsageMask)
                  .Append(layer.Name == snapshot.FocusedLayerName ? "   <== FOCUSED" : "")
                  .AppendLine();
            }

            sb.AppendLine();

            Directory.CreateDirectory("Logs");
            var path = Path.Combine("Logs", DumpFileName);
            File.AppendAllText(path, sb.ToString());

            return string.Join("\n", verdicts)
                 + $"\nAppended full layer table to {Path.GetFullPath(path)}";
        });

    /// <summary>
    /// Reads the engine statics into a plain snapshot. <c>SortedLayers</c> is the right source rather
    /// than the top screen's own layers: its getter appends the global layers, and the campaign-map
    /// resource bar lives on one of those, so a top-screen-only walk would miss the very layer the
    /// resource-bar half of this diagnostic is about.
    /// </summary>
    private static InputStateSnapshot Capture()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = ScreenManager.TopScreen?.GetType().Name ?? "",
            FocusedLayerName = ScreenManager.FocusedLayer?.Name,
        };

        foreach (var layer in ScreenManager.SortedLayers ?? new List<ScreenLayer>())
        {
            if (layer == null) continue;

            snapshot.Layers.Add(new LayerInputState
            {
                Name = layer.Name ?? "-",
                TypeName = layer.GetType().Name,
                Order = layer.InputRestrictions?.Order ?? 0,
                IsActive = layer.IsActive,
                IsFocusCandidate = layer.IsFocusLayer,
                IsHitThisFrame = layer.IsHitThisFrame,
                KeysAllowed = layer.Input?.IsKeysAllowed ?? false,
                MouseButtonAllowed = layer.Input?.IsMouseButtonAllowed ?? false,
                InputUsageMask = layer.InputRestrictions?.InputUsageMask.ToString() ?? "-",
            });
        }

        return snapshot;
    }

    private static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string Flag(bool value) => value ? "Y" : ".";

    private static string Pad(string value, int width)
    {
        var text = value ?? "-";
        return text.Length > width ? text.Substring(0, width) : text.PadRight(width);
    }
}
