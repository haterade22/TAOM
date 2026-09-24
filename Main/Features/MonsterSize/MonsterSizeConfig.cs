namespace TAOM.Features.MonsterSize;

/// <summary>
/// A mount's size is authored on its Monster (Mike, 2026-09-23, #646, docs/features/monster-size.md). The engine has
/// no size attribute on &lt;Monster&gt; (v1.5.3 Monster.Deserialize reads none): it sizes a mount only from the ridden
/// item's body_length (Mission.BuildAgent: SetInitialAgentScale(0.01f * HorseComponent.BodyLength)). So TAOM reads
/// its own attribute off the Monster and writes it into every Horse item naming that Monster at game init; the
/// agent scale, the rider's camera and TAOM's reach all follow that one number.
/// </summary>
public static class MonsterSizeConfig
{
    /// <summary>TAOM's attribute on &lt;Monster&gt;, in body_length units (100 = the authored size). The engine's
    /// Monsters.xsd does not declare it, so the engine prints one "not declared" validation line per sized Monster at
    /// load and loads the file anyway (MBObjectManager.ValidationEventHandler only prints).</summary>
    public const string AttributeName = "taom_body_length";

    /// <summary>The body_length a sized Monster's Horse items keep. The engine's Items.xsd requires body_length on
    /// &lt;Horse&gt;, so the item cannot drop it; it holds this neutral value, TAOM overrides it with the Monster's size at
    /// game init, and a failed pass shows the mount at 1.0x rather than hiding behind a second copy of the size.</summary>
    public const int ItemPlaceholderBodyLength = 100;

    /// <summary>Accepted range, inclusive: 0.1x to 10x. The mumakil, TAOM's largest mount, is 300.</summary>
    public const int MinBodyLength = 10;
    public const int MaxBodyLength = 1000;
}
