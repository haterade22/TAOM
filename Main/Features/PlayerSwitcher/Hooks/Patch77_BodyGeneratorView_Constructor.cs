using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher.UI;

namespace TAOM.Features.PlayerSwitcher.Hooks;

/// <summary>
/// Attaches the hero picker to the character creation face generator.
/// </summary>
/// <remarks>
/// Binds the constructor by ARITY, never by a hand-written Type[]. The predecessor mod pinned a
/// 12-type array and 1.4.8 added a 13th parameter (FaceGenHistory), so that attribute now matches
/// nothing and throws at PatchCategory time, which bricks startup. AccessTools.Constructor with no
/// type array is not a substitute either: Harmony normalises a null parameter array to
/// Type.EmptyTypes and looks for a parameterless constructor, which does not exist.
///
/// The ActiveState guard is what keeps the picker off the barber screen and the multiplayer face
/// generator, both of which construct the same view.
/// </remarks>
[HarmonyPatchCategory("Patch77_PlayerSwitcher")]
[HarmonyPatch]
public static class Patch77_BodyGeneratorView_Constructor
{
    private const string SpriteCategory = "ui_clan";

    private static IModLogger? _logger;

    /// <summary>Live picker state. One face generator exists at a time, so a single slot suffices.</summary>
    internal static BodyGeneratorView? HostView;
    internal static GauntletMovieIdentifier? Movie;
    internal static PlayerSwitcherVM? ViewModel;
    internal static bool WeLoadedSpriteCategory;

    public static void Initialize(IModLogger logger) => _logger = logger;

    /// <summary>Only bind while the engine still declares exactly one constructor.</summary>
    public static bool Prepare() => typeof(BodyGeneratorView).GetConstructors().Length == 1;

    public static IEnumerable<MethodBase> TargetMethods()
    {
        var ctors = typeof(BodyGeneratorView).GetConstructors();
        if (ctors.Length == 1)
            yield return ctors[0];
    }

    [HarmonyPostfix]
    public static void Postfix(BodyGeneratorView __instance)
    {
        try
        {
            Attach(__instance);
        }
        catch (Exception ex)
        {
            // A broken picker must never stop a player creating a character.
            _logger?.LogError($"Player Switcher: could not attach the picker panel: {ex}");
        }
    }

    private static void Attach(BodyGeneratorView view)
    {
        var policy = IoC.Resolve<IPlayerSwitchPolicyProvider>();
        if (!policy.Current.Enabled)
            return;

        // Both the barber and the multiplayer face generator build this same view.
        if (!(Game.Current?.GameStateManager?.ActiveState is CharacterCreationState state))
            return;

        var session = IoC.Resolve<IPlayerSwitchSessionWriter>();

        // Clear-on-construct IS the selection lifecycle. Because the view is rebuilt every time the
        // player enters the face generator stage, this alone handles going back to the culture
        // stage and returning, and removes any need to patch ExecuteDone, ExecuteCancel or
        // ResetFaceToDefault.
        session.Clear();

        var identity = IoC.Resolve<TAOM.Adapters.IPlayerIdentityAdapter>();
        if (!identity.CanReassignPlayerClan)
        {
            // The probe failed, so the handover could not complete. Never show a panel that
            // promises something the campaign cannot deliver, and say so once rather than
            // leaving the player wondering where the feature went.
            policy.DisableForSession("the player clan pointer is not reachable on this engine build");
            IoC.Resolve<TAOM.Adapters.IInquiryAdapter>().ShowMessage(
                "taom_ps_unavailable",
                "The player switcher could not start and is disabled for this session.",
                null, null);
            return;
        }

        var cultureId = state.CharacterCreationManager?.CharacterCreationContent?.SelectedCulture?.StringId;
        if (string.IsNullOrEmpty(cultureId))
            return;

        var picks = IoC.Resolve<IHeroPickerService>().BuildPickList(cultureId, policy.Current);
        if (picks.IsEmpty)
            return;

        var logger = _logger ?? IoC.Resolve<IModLogger>();
        var sink = new BodyGeneratorPreviewSink(view, session, logger);
        var vm = new PlayerSwitcherVM(picks, session, sink, logger);

        LoadSpriteCategory(logger);

        HostView = view;
        ViewModel = vm;
        Movie = view.GauntletLayer.LoadMovie("PreBuildCharacterSelection", vm);

        logger.LogInfo($"Player Switcher: picker attached for culture '{cultureId}' with {picks.TotalCount} lords");
    }

    /// <summary>
    /// The picker reuses vanilla's clan sprites. SpriteCategory carries an IsLoaded bool and no
    /// reference count, so an unconditional Unload on teardown would pull the sheet out from under
    /// any other consumer. Remember whether WE were the ones who loaded it.
    /// </summary>
    private static void LoadSpriteCategory(IModLogger logger)
    {
        var category = UIResourceManager.GetSpriteCategory(SpriteCategory);
        if (category == null)
        {
            logger.LogWarning($"Player Switcher: sprite category '{SpriteCategory}' not found; rows may render unstyled");
            return;
        }

        WeLoadedSpriteCategory = !category.IsLoaded;
        if (WeLoadedSpriteCategory)
            UIResourceManager.LoadSpriteCategory(SpriteCategory);
    }
}
