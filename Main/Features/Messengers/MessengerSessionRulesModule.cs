using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.Messengers.Hooks;

namespace TAOM.Features.Messengers;

/// <summary>
/// Messenger session rules (#898, docs/features/messengers.md): hides the lord lines a messenger conversation cannot
/// honour. One postfix patch class over six vanilla conditions; no model, behavior, setting or save data.
/// </summary>
internal sealed class MessengerSessionRulesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch117_MessengerSessionRules";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "MessengerSessionRules";

    public override void InitializeStatics(IResolver resolver) =>
        MessengerSessionRulesPatch.Initialize(resolver.Resolve<IMessengerConversationState>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
