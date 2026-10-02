using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Drift guards for Patch95_FactionUI (#704). Harmony binds prefix parameters by NAME, so each target is
/// pinned by its parameter names as well as its shape (lessons/harmony-il.md, "Harmony binds
/// prefix/postfix parameters by NAME"); the private music members and the enum values the music adapter
/// writes are pinned too, since a renumbered <c>MusicMode</c> would compile to a stale constant.
/// </summary>
[TestClass]
public class FactionUIBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static void AssertParameterNames(MethodBase? method, string description, params string[] expected)
    {
        Assert.IsNotNull(method, description + " did not resolve: Patch95 would not apply.");
        CollectionAssert.AreEqual(expected, method!.GetParameters().Select(p => p.Name).ToArray(),
            description + " parameter names changed: Harmony binds the prefix by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GauntletLayerLoadMovie_KeepsItsNames()
    {
        RequireGame();
        var layer = AccessTools.TypeByName("TaleWorlds.Engine.GauntletUI.GauntletLayer");
        var viewModel = AccessTools.TypeByName("TaleWorlds.Library.ViewModel");
        AssertParameterNames(AccessTools.Method(layer, "LoadMovie", new[] { typeof(string), viewModel }),
            "GauntletLayer.LoadMovie(string, ViewModel)", "movieName", "dataSource");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void UIResourceManagerRefresh_IsAPublicStaticMethodWithNoParameters()
    {
        RequireGame();
        var manager = AccessTools.TypeByName("TaleWorlds.Engine.GauntletUI.UIResourceManager");
        var refresh = AccessTools.Method(manager, "Refresh", Type.EmptyTypes);
        AssertParameterNames(refresh, "UIResourceManager.Refresh()");
        Assert.IsTrue(refresh!.IsStatic && refresh.IsPublic, "UIResourceManager.Refresh is no longer public static");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpriteCategoryIsLoaded_KeepsTheSetterTheFontRegistrationCalls()
    {
        // FrontEndResourceAdapter.RegisterFont marks its one-sheet category loaded through this private
        // setter; without it a runtime font's glyph sheet draws nothing, with no log line.
        RequireGame();
        var category = AccessTools.TypeByName("TaleWorlds.TwoDimension.SpriteCategory");
        Assert.IsNotNull(AccessTools.PropertySetter(category, "IsLoaded"), "SpriteCategory.IsLoaded has no setter left");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void VideoPlayerViewPlayVideo_KeepsItsNames()
    {
        RequireGame();
        var view = AccessTools.TypeByName("TaleWorlds.Engine.VideoPlayerView");
        AssertParameterNames(
            AccessTools.Method(view, "PlayVideo", new[] { typeof(string), typeof(string), typeof(float), typeof(bool) }),
            "VideoPlayerView.PlayVideo", "videoFileName", "soundFileName", "framerate", "looping");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void VideoPlaybackStateSetStartingParameters_KeepsItsNames()
    {
        RequireGame();
        var state = AccessTools.TypeByName("TaleWorlds.MountAndBlade.VideoPlaybackState");
        AssertParameterNames(AccessTools.Method(state, "SetStartingParameters"),
            "VideoPlaybackState.SetStartingParameters",
            "videoPath", "audioPath", "subtitleFileBasePath", "frameRate", "canUserSkip");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LoadingImageNameSetter_KeepsItsName()
    {
        RequireGame();
        var viewModel = AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.LoadingWindowViewModel");
        AssertParameterNames(AccessTools.PropertySetter(viewModel, "LoadingImageName"),
            "LoadingWindowViewModel.LoadingImageName setter", "value");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MusicManagerMenuModeMembers_Resolve()
    {
        RequireGame();
        var manager = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MBMusicManager");
        AssertParameterNames(AccessTools.Method(manager, "ActivateMenuMode", Type.EmptyTypes), "MBMusicManager.ActivateMenuMode");
        Assert.IsNotNull(AccessTools.PropertySetter(manager, "CurrentMode"), "MBMusicManager.CurrentMode has no setter to restore the mode with.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MusicModeValues_AreTheOnesTheAdapterWasCompiledAgainst()
    {
        RequireGame();
        var manager = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MBMusicManager");
        var modeType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MusicMode");
        Assert.AreEqual(modeType, AccessTools.Property(manager, "CurrentMode")?.PropertyType);
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(modeType, "Paused")));
        Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(modeType, "Menu")));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BodyGeneratorViewConstructor_KeepsItsThirteenNames()
    {
        RequireGame();
        var view = AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator.BodyGeneratorView");
        var constructors = view.GetConstructors();
        Assert.AreEqual(1, constructors.Length, "BodyGeneratorView has more than one constructor now: pick the right one");
        CollectionAssert.AreEqual(
            new[]
            {
                "affirmativeAction", "affirmativeActionText", "negativeAction", "negativeActionText", "character",
                "openedFromMultiplayer", "filter", "dressedEquipment", "getCurrentStageIndexAction",
                "getTotalStageCountAction", "getFurthestIndexAction", "goToIndexAction", "faceGenHistory",
            },
            constructors[0].GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InitCamera_KeepsItsNames()
    {
        RequireGame();
        var view = AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator.BodyGeneratorView");
        AssertParameterNames(AccessTools.Method(view, "InitCamera"), "BodyGeneratorView.InitCamera", "camera", "cameraPosition");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ReflectedCharacterCreationMembers_Resolve()
    {
        RequireGame();
        var faceGen = AccessTools.TypeByName("TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM");
        Assert.IsNotNull(AccessTools.Field(faceGen, "_faceGeneratorScreen"), "FaceGenVM._faceGeneratorScreen: the face generator fade-in finds its view through it");
        var view = AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator.BodyGeneratorView");
        Assert.IsNotNull(AccessTools.Method(view, "ReadyToRender"), "BodyGeneratorView.ReadyToRender");
        var manager = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager");
        Assert.IsNotNull(AccessTools.Method(manager, "GoToStage", new[] { typeof(int) }), "CharacterCreationManager.GoToStage(int): the stage buttons jump through it");
        // The same lookup NarrativeRandomButton makes: if HandleClick ever turned public, a lookup with
        // every binding flag would still pass while the button silently stopped working.
        Assert.IsNotNull(
            AccessTools.TypeByName("TaleWorlds.GauntletUI.BaseTypes.ButtonWidget")
                .GetMethod("HandleClick", BindingFlags.Instance | BindingFlags.NonPublic),
            "ButtonWidget.HandleClick is no longer a non-public instance method: the Random button presses options through it");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryPatch95Class_CarriesTheAppliedCategory()
    {
        var patchClasses = typeof(FactionUIPatchContext).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(FactionUIPatchContext).Namespace
                        && t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
            .ToList();

        Assert.AreEqual(8, patchClasses.Count, "Patch95 has eight patch classes: " + string.Join(", ", patchClasses.Select(t => t.Name)));
        foreach (var patch in patchClasses)
        {
            var category = patch.GetCustomAttribute<HarmonyPatchCategory>();
            Assert.AreEqual("Patch95_FactionUI", category?.info.category, patch.Name + " is outside the applied category.");
        }
    }

    // FrontEndMovieService.BeginLoad's vanilla half: a swap matches the movie name AND the data source's
    // type name, so an engine rename of either silently leaves that screen vanilla.
    private static readonly (string Movie, string ViewModel)[] VanillaScreens =
    {
        ("InitialScreen", "InitialMenuVM"),
        ("GameVersion", "GameVersionVM"),
        ("LoadingWindow", "LoadingWindowViewModel"),
        ("FaceGen", "FaceGenVM"),
        ("CharacterCreationNarrativeStage", "CharacterCreationNarrativeStageVM"),
        ("CharacterCreationReviewStage", "CharacterCreationReviewStageVM"),
        ("BannerEditor", "BannerEditorVM"),
        ("CharacterCreationClanNamingStage", "CharacterCreationClanNamingStageVM"),
        ("CharacterCreationOptionsStage", "CharacterCreationOptionsStageVM"),
    };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SwapTable_EveryVanillaViewModelName_IsStillAnEngineViewModel()
    {
        RequireGame();
        var viewModels = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name.StartsWith("TaleWorlds", StringComparison.Ordinal)
                        || a.GetName().Name.StartsWith("SandBox", StringComparison.Ordinal)
                        || a.GetName().Name.StartsWith("StoryMode", StringComparison.Ordinal))
            .SelectMany(LoadableTypes)
            .Where(t => IsViewModel(t))
            .Select(t => t.Name)
            .ToList();

        foreach (var (movie, viewModel) in VanillaScreens)
            Assert.IsTrue(viewModels.Contains(viewModel), $"{viewModel} (the data source of {movie}) is no engine view model any more: that screen would stay vanilla.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("LiveInstall")]
    public void SwapTable_EveryVanillaMovie_IsStillAPrefabInTheInstall()
    {
        RequireGame();
        var prefabs = new[] { "Native", "SandBox", "SandBoxCore", "StoryMode", "CustomBattle" }
            .Select(module => System.IO.Path.Combine(GameAssemblies.GameDir, "Modules", module, "GUI", "Prefabs"))
            .Where(System.IO.Directory.Exists)
            .SelectMany(dir => System.IO.Directory.GetFiles(dir, "*.xml", System.IO.SearchOption.AllDirectories))
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .ToList();

        foreach (var (movie, _) in VanillaScreens)
            Assert.IsTrue(prefabs.Contains(movie), $"No vanilla prefab named {movie}: the engine no longer loads that movie, so the themed swap never fires.");
    }

    private static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToArray()!;
        }
    }

    private static bool IsViewModel(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.FullName == "TaleWorlds.Library.ViewModel")
                return true;
        }
        return false;
    }

    [TestMethod]
    public void SubModule_AppliesInitializesTicksAndResetsPatch95()
    {
        // Each of these missing leaves the front end vanilla, or themed but leaking, with no error:
        // no category call applies nothing, no Initialize leaves every prefix a no-op, no tick never
        // releases an image, and no game-end reset carries one campaign's pick into the next.
        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(source, "TryPatchCategory(\"Patch95_FactionUI\")", "SubModule no longer applies Patch95.");
        StringAssert.Contains(source, "FactionUIPatchContext.Initialize(", "SubModule no longer initializes the patch context.");
        StringAssert.Contains(source, "FactionUIWidgetContext.Initialize(", "SubModule no longer initializes the widget context.");
        StringAssert.Contains(source, "_factionUiTicker?.Tick(dt)", "SubModule no longer ticks the front end.");
        StringAssert.Contains(source, "FactionScreenLauncher>()?.ResetForGameEnd()", "SubModule no longer resets the faction screen on game end.");
        // Without the 1060 handler a pick's name and skills never land, and the pick stays "active".
        StringAssert.Contains(source, "FactionPresetRegistrationBehavior(", "SubModule no longer adds the hero-preset finalize behavior.");
    }
}
