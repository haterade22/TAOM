using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.IncidentVoices.Hooks;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// Incident Voices (docs/features/incident-voices.md): the incidents the map shows speak of Middle-earth, in the player's
/// side's voice, and close for peoples they do not fit. One postfix on <c>Incident.CanIncidentBeInvoked</c> in Patch176, applied
/// at GameInit. No behavior, model or save data, nothing in <c>IoC.cs</c> or <c>SubModule.cs</c>.
/// </summary>
internal sealed class IncidentVoicesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch176_IncidentVoices";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "IncidentVoices";

    public override void RegisterServices(IRegistrator registrator) => IncidentVoicesIoC.RegisterIncidentVoicesFeature(registrator);

    public override void InitializeStatics(IResolver resolver) =>
        IncidentCanBeInvokedPatch.Initialize(resolver.Resolve<IncidentVoiceService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
