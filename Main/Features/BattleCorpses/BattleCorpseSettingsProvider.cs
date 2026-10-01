namespace TAOM.Features.BattleCorpses;

public class BattleCorpseSettingsProvider : IBattleCorpseSettingsProvider
{
    public bool IsCleanupEnabled => TaomSettings.Instance?.EnableBattleCorpseCleanup ?? true;

    public float FadeSeconds => TaomSettings.Instance?.BattleCorpseFadeSeconds ?? BattleCorpsePolicy.DefaultFadeSeconds;

    public int CorpseCap => TaomSettings.Instance?.BattleCorpseCap ?? BattleCorpsePolicy.DefaultCorpseCap;

    public bool IsAdviceEnabled => TaomSettings.Instance?.ShowBattleSettingsAdvice ?? true;
}
