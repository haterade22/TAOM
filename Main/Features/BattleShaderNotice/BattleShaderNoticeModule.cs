using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BattleShaderNotice.Hooks;

namespace TAOM.Features.BattleShaderNotice;

/// <summary>
/// Battle Shader Notice (docs/features/battle-shader-notice.md): the startup shader notice's window and a loading-screen
/// line during a battle load that compiles many shaders. Patch155 applies at GameInit: both targets first run when a
/// mission loads, after the game has initialized. No settings, no save data.
/// </summary>
internal sealed class BattleShaderNoticeModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch155_BattleShaderNotice";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "BattleShaderNotice";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IBattleShaderNoticeRunner, BattleShaderNoticeRunner>(Reuse.Singleton);
        registrator.Register<IBattleShaderNoticeTextSource, BattleShaderNoticeTextSource>(Reuse.Singleton);
        registrator.Register<IShaderCompileQueueAdapter, ShaderCompileQueueAdapter>(Reuse.Singleton);
        registrator.Register<BattleShaderNoticeService>(Reuse.Singleton);
        registrator.RegisterMapping<IScreenShaderNotice, BattleShaderNoticeService>();
    }

    public override void InitializeStatics(IResolver resolver)
    {
        var service = resolver.Resolve<BattleShaderNoticeService>();
        var logger = resolver.Resolve<IModLogger>();
        MissionState_OnTick_BattleShaderNotice_Patch.Initialize(service, logger);
        LoadingWindowViewModel_Update_BattleShaderLine_Patch.Initialize(service, logger);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
