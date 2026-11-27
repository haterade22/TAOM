using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.LoreGreetings.Hooks;

namespace TAOM.Features.LoreGreetings;

/// <summary>
/// Lore Greetings (docs/features/lore-greetings.md): people who know each other in the books greet each other by lore. Two patches
/// in Patch177, applied at GameInit, over one service. No behavior, model or save data, nothing in <c>IoC.cs</c> or
/// <c>SubModule.cs</c>.
/// </summary>
internal sealed class LoreGreetingsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch177_LoreGreetings";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "LoreGreetings";

    public override void RegisterServices(IRegistrator registrator) => LoreGreetingsIoC.RegisterLoreGreetingsFeature(registrator);

    public override void InitializeStatics(IResolver resolver)
    {
        var service = resolver.Resolve<LoreGreetingsService>();
        Patch177_LoreGreeting.Initialize(service);
        Patch177_LoreMeeting.Initialize(service);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
