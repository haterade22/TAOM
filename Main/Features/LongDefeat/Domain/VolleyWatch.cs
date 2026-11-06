namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One volley use as the director fired it, kept for its <c>volley</c> check line (phase3-plan.md section 6): which wave and use, the source, the aim mode and who aimed it (<c>ByBot</c>; a person aims the
/// crosshair with his eye ray, the bot toward the automatic centre), how far the aimed point lay from the nearest attacker, the arrows asked for, the shots the aim planned, the attackers in reach, the distinct
/// attackers the shots name, the missiles the field added, the agents the fire spawned (0 for a missile), and the owner's troop and damage multiplier (D25).
/// </summary>
internal sealed record VolleyUse(int Wave, int Use, VolleySource Source, VolleyAimMode Mode, bool ByBot, float? RayGap, int Arrows, int Shots, int Candidates, int Distinct, int Added,
    int Spawned, string OwnerTroop, float Multiplier);

/// <summary>
/// The hits a use's arrows land while its window is open (the director opens it when the volley fires and closes it six seconds later, at a clear, or at the run's end). A foe hit counts in <see cref="Hits"/>
/// and, when the victim fell, in <see cref="Kills"/>; a shield block, a friendly hit and a friendly hit the engine cancelled are each counted apart. The engine's null affectors and wrong-side affectors are
/// counted for the reader (A24), and the first hit's numbers kept (A25). Not thread safe: the director feeds it on the main thread.
/// </summary>
internal sealed class VolleyWatch
{
    public VolleyWatch(VolleyUse use) => Use = use;

    public VolleyUse Use { get; }

    public int Hits { get; private set; }

    public int Kills { get; private set; }

    public int Blocked { get; private set; }

    public int Friendly { get; private set; }

    public int Cancelled { get; private set; }

    public int AffectorNull { get; private set; }

    public int AffectorSide { get; private set; }

    public float BaseSpeed { get; private set; }

    public float ImpactSpeed { get; private set; }

    /// <summary>Seconds this window has been open, set by the director as it ticks.</summary>
    public float Seconds { get; set; }

    public void Add(LdVolleyHit hit)
    {
        if (hit.AffectorNull)
            AffectorNull++;
        if (hit.AffectorSideWrong)
            AffectorSide++;
        if (Hits + Blocked + Friendly + Cancelled == 0 && hit.Kind != LdVolleyHitKind.Cancelled)
        {
            BaseSpeed = hit.StartingBaseSpeed;
            ImpactSpeed = hit.ImpactSpeed;
        }

        switch (hit.Kind)
        {
            case LdVolleyHitKind.Foe:
                Hits++;
                if (hit.Kill)
                    Kills++;
                break;
            case LdVolleyHitKind.Blocked:
                Blocked++;
                break;
            case LdVolleyHitKind.Friendly:
                Friendly++;
                break;
            case LdVolleyHitKind.Cancelled:
                Cancelled++;
                break;
        }
    }
}
