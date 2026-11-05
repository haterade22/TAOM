using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.TextureAdvice;

/// <summary>
/// One-time memory advice (docs/features/texture-advice.md): on a machine with about 16 GB or less at the highest Texture
/// Quality, one popup suggests one step lower. <see cref="ApplyPhase.MainMenu"/> runs once per process at the first main
/// menu, where <c>StallReportNotifier</c> already shows a popup. No engine type is touched before that step.
/// </summary>
internal sealed class TextureAdviceModule : TaomFeatureModule
{
    public override string Id => "TextureAdvice";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ITextureAdviceAdapter, TextureAdviceAdapter>(Reuse.Singleton);
        registrator.Register<ITextureAdviceService, TextureAdviceService>(Reuse.Singleton);
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.MainMenu)
            resolver.Resolve<ITextureAdviceService>().AdviseOnce();
    }
}
