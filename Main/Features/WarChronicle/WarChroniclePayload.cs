using System.Collections.Generic;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle's save content, section by section, between the services and the JSON
/// (<see cref="WarChronicleSaveCodec"/>). Each section is read on its own: a section that fails to
/// parse comes back empty with a line in <see cref="Warnings"/>, the others unaffected.
/// </summary>
internal sealed class WarChroniclePayload
{
    public List<WarEffect> Effects { get; } = new List<WarEffect>();

    /// <summary>Empty when the save had no baselines section, or one that did not parse.</summary>
    public Dictionary<string, int> Baselines { get; } = new Dictionary<string, int>();

    public Dictionary<string, int> RallyTiers { get; } = new Dictionary<string, int>();

    /// <summary>
    /// The chronicle section, a JSON array this milestone does not interpret: the event states are
    /// reserved for a later one. Carried through a load and a save unchanged.
    /// </summary>
    public string ChronicleJson { get; set; } = WarChronicleSaveCodec.EmptyArray;

    /// <summary>What was skipped or reset while parsing, one line each, for the log.</summary>
    public List<string> Warnings { get; } = new List<string>();
}
