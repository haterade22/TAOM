namespace TAOM.Features.BattleShaderNotice;

/// <summary>The shader notice for a screen outside a mission (character creation), fed once per application tick.</summary>
public interface IScreenShaderNotice
{
    /// <summary>Shows the notice while <paramref name="screenOpen"/> and the engine has many shaders queued; closes it
    /// when the queue empties or the screen closes.</summary>
    void OnScreenTick(bool screenOpen);
}
