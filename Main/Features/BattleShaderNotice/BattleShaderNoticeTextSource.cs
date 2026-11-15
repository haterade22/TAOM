using TAOM.Features.ShaderCompileNotice;
using TaleWorlds.Localization;

namespace TAOM.Features.BattleShaderNotice;

/// <summary>
/// Engine boundary: resolves the battle notice's texts on the game thread, before the window thread starts. The
/// window thread only fills the <see cref="ShaderCompileNoticeTexts.CountToken"/> placeholder and never touches the
/// engine's text manager.
/// </summary>
internal sealed class BattleShaderNoticeTextSource : IBattleShaderNoticeTextSource
{
    public BattleShaderNoticeTexts Resolve()
    {
        var title = new TextObject("{=taom_battle_shader_notice_title}Preparing this scene: {COUNT} shaders compiled");
        title.SetTextVariable("COUNT", ShaderCompileNoticeTexts.CountToken);
        string titleText = title.ToString();
        string detail = new TextObject(
            "{=taom_battle_shader_notice_detail}The game is compiling shaders for this scene. It is not frozen. This can take a minute or two.")
            .ToString();
        string line = new TextObject(
            "{=taom_battle_shader_loading_line}Compiling shaders for this scene. This can take a minute or two; the game is not frozen.")
            .ToString();

        // Both title slots carry the count-only title: a battle has no remembered total.
        return new BattleShaderNoticeTexts(new ShaderCompileNoticeTexts(titleText, titleText, detail), line);
    }
}
