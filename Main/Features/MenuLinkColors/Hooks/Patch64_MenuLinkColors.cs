using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu;

namespace TAOM.Features.MenuLinkColors.Hooks;

/// <summary>
/// Patch64 — Prefix on <c>GameMenuVM.ContextText</c>'s setter, retinting the hyperlinks in the
/// game-menu body by faction.
///
/// Why this seam and not one closer to the text: <c>GameMenuManager.GetMenuText</c> and
/// <c>GameMenu.GetText()</c> both return the SAME cached <c>TextObject</c> reference every call,
/// and <c>GameMenuVM.IsMenuTextChanged()</c> compares by reference every frame. A postfix there
/// returning a new object would rebuild the menu text at 60 Hz forever; mutating the returned
/// object would corrupt the menu's stored template. The setter is reached only when
/// <c>_requireContextTextUpdate</c> is set — once per menu open or refresh.
///
/// The prefix runs BEFORE vanilla's <c>if (value != _contextText)</c> guard, so the field and the
/// comparison both see the rewritten string and no extra property-change notification fires.
///
/// Rewriting here rather than in <c>HyperlinkTexts</c> is what keeps the TAOM style names off
/// every other UI surface — the encyclopedia and quest log build their strings independently and
/// never pass through this VM, so their brushes are never asked for a style they lack.
/// </summary>
[HarmonyPatch(typeof(GameMenuVM), "ContextText", MethodType.Setter)]
[HarmonyPatchCategory("Patch64_MenuLinkColors")]
public static class Patch64_MenuLinkColors
{
    private static IMenuLinkStyleRewriter _rewriter;

    private static IMenuLinkStyleRewriter GetRewriter() =>
        _rewriter ??= TAOM.IoC.Resolve<IMenuLinkStyleRewriter>();

    [HarmonyPrefix]
    public static void Prefix(ref string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        try
        {
            value = GetRewriter()?.Rewrite(value) ?? value;
        }
        catch
        {
            // Purely cosmetic — a failure here must never cost the player the menu text itself.
        }
    }
}
