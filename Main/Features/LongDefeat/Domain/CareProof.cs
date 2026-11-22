namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// When a field care heal can be proved (#1143). The <c>field-care</c> check compares the defenders' mean health after the heal with the mean before plus the share of what was missing, within
/// <see cref="Tolerance"/>, so a heal that raises the mean by no more than <see cref="ProvableRise"/> proves nothing: a write that did nothing would pass too, and the check skips it. The
/// unattended bot therefore buys care only when the heal will show, or every such run would owe a pass the game cannot write.
/// </summary>
internal static class CareProof
{
    /// <summary>The mean may differ from the sum by this share of the limit: <c>Agent.Health</c>'s setter ceils (E23), so a soldier gains up to one whole point, about 1% of a 100 health limit and half that on average.</summary>
    internal const float Tolerance = 0.01f;

    /// <summary>A heal must raise the mean by more than this (twice <see cref="Tolerance"/>) for a pass to mean anything.</summary>
    internal const float ProvableRise = 2f * Tolerance;

    /// <summary>
    /// The rise the bot wants before it buys: a quarter over <see cref="ProvableRise"/>, because the heal lands a tick after the buy and a pack bought on that tick joins at full health first,
    /// which raises the mean a little.
    /// </summary>
    internal const float BotRise = 1.25f * ProvableRise;

    /// <summary>
    /// Whether the bot should buy care now: the mean was read (0 to 1; -1 is "nobody read" and a NaN is no reading) and the share of what is missing clears <see cref="BotRise"/>.
    /// </summary>
    public static bool WorthBuying(float share, float meanHealth) => meanHealth >= 0f && meanHealth <= 1f && share * (1f - meanHealth) > BotRise;

    /// <summary>
    /// The mean the heal will find after <paramref name="joined"/> soldiers at full health join the <paramref name="standing"/> soldiers the mean was read over. A reading that is none (below 0, or a NaN)
    /// stays as it is, and so does a mean with nobody joining.
    /// </summary>
    public static float AfterJoin(float meanHealth, int standing, int joined) =>
        meanHealth >= 0f && standing > 0 && joined > 0 ? (meanHealth * standing + joined) / (standing + joined) : meanHealth;
}
