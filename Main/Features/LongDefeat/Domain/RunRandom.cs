namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The run's only source of randomness: SplitMix64 over a seed, so the unattended bot and the leaderboard can replay every roll. Integer
/// arithmetic only, no floating point in a draw. <see cref="For"/> derives an independent generator per purpose, so one stream
/// never shifts another when a feature draws more or fewer numbers.
/// </summary>
internal sealed class RunRandom
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const ulong MixA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixB = 0x94D049BB133111EBUL;

    /// <summary>Stream numbers: affixes (1, wave, 0), boon offers (2, pick, draw), the bot's class (3, 0, 0).</summary>
    public const int AffixStream = 1;
    public const int OfferStream = 2;
    public const int ClassStream = 3;

    private ulong _state;

    private RunRandom(ulong state) => _state = state;

    /// <summary>The generator of one purpose: it starts at <c>seed ^ (stream * Gamma) ^ (a * MixA) ^ (b * MixB)</c>, every product unchecked in 64 bits.</summary>
    public static RunRandom For(ulong seed, int stream, int a, int b) =>
        new(seed ^ unchecked((ulong)stream * Gamma) ^ unchecked((ulong)a * MixA) ^ unchecked((ulong)b * MixB));

    public ulong Next()
    {
        unchecked
        {
            _state += Gamma;
            var z = _state;
            z = (z ^ (z >> 30)) * MixA;
            z = (z ^ (z >> 27)) * MixB;
            return z ^ (z >> 31);
        }
    }

    /// <summary>One draw mapped to 0 to <c>n - 1</c> as <c>Next() % n</c>. An <paramref name="n"/> of 0 or less gives 0 and still consumes a draw, so the sequence stays aligned.</summary>
    public int Below(int n)
    {
        var draw = Next();
        return n <= 0 ? 0 : (int)(draw % (ulong)n);
    }
}
