using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The recorded lines of the mission heroes (#1569): per hero id an ordered list of lines, each a list of takes, and each take the full path of one OGG file.
/// <see cref="None"/> has no hero.
/// </summary>
internal sealed class HeroLinePlan
{
    public static HeroLinePlan None { get; } = new(new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>());

    private readonly IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> _lines;

    public HeroLinePlan(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> lines) => _lines = lines;

    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> Lines => _lines;

    /// <summary>
    /// The take to play at a hero's <paramref name="spawn"/>th spawn of the mission (1-based): line <c>(spawn - 1) % lines</c>, so the lines come in order and then start again,
    /// and take <c>(salt + spawn) % takes</c>. Null for a hero with no lines and for a spawn below 1.
    /// </summary>
    public string? Pick(string heroId, int spawn, ulong salt)
    {
        if (spawn < 1 || !_lines.TryGetValue(heroId, out var lines) || lines.Count == 0)
            return null;

        var takes = lines[(spawn - 1) % lines.Count];
        return takes.Count == 0 ? null : takes[(int)((salt + (ulong)spawn) % (ulong)takes.Count)];
    }
}
