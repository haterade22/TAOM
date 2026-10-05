using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CreatureSiegeRole.Hooks;

namespace TAOM.Features.CreatureSiegeRole;

/// <summary>
/// Creature Siege Role (docs/features/creature-siege-role.md): oversized creatures (cave and hill trolls) never crew a siege
/// engine or climb a ladder or tower, and instead break the castle gates, then hold the ground behind them (defenders: hold the
/// gate). Two settings-and-config providers, one mission behavior and no Harmony patch: the engine reaches the role through four
/// game models that call <see cref="CreatureSiegeHooks"/> (the two agent stat models for the detachment cost, the two damage
/// models for the gate blow), and the mission behavior moves the creatures. The hooks' logger is wired in
/// <see cref="InitializeStatics"/>. No save data: everything lives and ends inside one battle.
///
/// Provenance: a behavioural port of TOR_Core's TORMonsterSiegeLogic (GPL-3.0), reimplemented on TAOM's own seams with nothing
/// copied; see the TOR_Core row in docs/reference/provenance-register.md.
///
/// No interface on either provider: nothing fakes them and each has one consumer (ADR-002). The singleton providers cache for the
/// process, so a JSON edit needs a restart of the game, not a new battle.
/// </summary>
internal sealed class CreatureSiegeRoleModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Behaviors =
    {
        MissionBehaviorDecl.Of((_, resolver) => new CreatureSiegeRoleMissionBehavior(
            resolver.Resolve<CreatureSiegeRoleSettingsProvider>(),
            resolver.Resolve<CreatureSiegeRoleConfigProvider>(),
            resolver.Resolve<IRaceManager>(),
            resolver.Resolve<IModLogger>())),
    };

    public override string Id => "CreatureSiegeRole";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<CreatureSiegeRoleSettingsProvider>(Reuse.Singleton);
        registrator.Register<CreatureSiegeRoleConfigProvider>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        CreatureSiegeHooks.Logger = resolver.Resolve<IModLogger>();

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Behaviors;
}
