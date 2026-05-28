using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Binding-verification gate for TAOM's <b>auxiliary</b> reflection sites — the private/internal
/// engine fields, properties, and methods that TAOM resolves by string name at runtime (via
/// <c>AccessTools.Field/Method/Property</c> or <c>Type.GetField/GetMethod</c>) OUTSIDE of a Harmony
/// patch's target resolution. Those targets are not compiler-checked: a rename/move/removal in a
/// Bannerlord update makes the lookup return null, and the feature silently degrades at runtime
/// (the reflecting code logs-and-survives, so there is no crash to investigate).
///
/// Patch <c>TargetMethod()</c> / attribute targets are NOT duplicated here — they are already covered
/// by <see cref="HarmonyPatchBindingTests"/>. This suite covers the residue: private state accessed
/// from patch bodies, adapters, and services.
///
/// Each row is hand-catalogued (string reflection cannot be auto-discovered from IL) and documented
/// in docs/reference/taleworlds-api-snapshot/reflection-sites.md. Runtime-dynamic sites that resolve
/// a type from a live instance (<c>instance.GetType()</c>) are intentionally excluded — they cannot
/// be verified without a running game; they are listed in the catalogue under "not offline-verifiable".
/// </summary>
[TestClass]
public class ReflectionSiteBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    // fullName, simpleName, member, kind (Field|Property|Method|Member), sourceSite
    [DataTestMethod]
    [TestCategory("BindingVerification")]
    // --- EquipPresets / inventory (InventoryScreenAdapter.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM", "SPInventoryVM", "_currentCharacter", "Field", "InventoryScreenAdapter.cs:29")]
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM", "SPInventoryVM", "_inventoryLogic", "Field", "InventoryScreenAdapter.cs:32")]
    // --- CompanionTactics formation-preset overlay (OOBOverlayService.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler", "MissionGauntletOrderOfBattleUIHandler", "_isActive", "Field", "OOBOverlayService.cs:57")]
    [DataRow("TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler", "MissionGauntletOrderOfBattleUIHandler", "_dataSource", "Field", "OOBOverlayService.cs:58")]
    // --- CompanionTactics role tooltips (RoleTooltipDecorator.cs) + manual patch (SubModule.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM", "PartyCharacterVM", "TypeIconData", "Property", "RoleTooltipDecorator.cs:40")]
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM", "OrderOfBattleHeroItemVM", "_cachedTooltipProperties", "Field", "RoleTooltipDecorator.cs:41")]
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM", "OrderOfBattleHeroItemVM", "GetCaptainTooltip", "Method", "SubModule.cs:503 (manual patch)")]
    // --- CharacterCreation race selector (FaceGenRaceSelectorRebuilder.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM", "FaceGenVM", "_selectedRace", "Field", "FaceGenRaceSelectorRebuilder.cs:208")]
    // --- SelectorVM<T> backing fields (FaceGen + CustomBattles commander selector) ---
    [DataRow("TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1", "SelectorVM`1", "_selectedIndex", "Field", "FaceGenRaceSelectorRebuilder.cs:211")]
    [DataRow("TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1", "SelectorVM`1", "_selectedItem", "Field", "FaceGenRaceSelectorRebuilder.cs:212")]
    [DataRow("TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1", "SelectorVM`1", "_onChange", "Field", "FaceGenRaceSelectorRebuilder.cs:213 / CommanderSelectorRebuilder.cs:21")]
    // --- CustomBattles faction injection (CustomBattleSideVM_Constructor_Patch.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.CustomBattle.CustomBattleSideVM", "CustomBattleSideVM", "OnCultureSelection", "Method", "CustomBattleSideVM_Constructor_Patch.cs:23")]
    // --- AdvancedCombat custom attacks (CustomAttacksUtils.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.Mission", "Mission", "RegisterBlow", "Method", "CustomAttacksUtils.cs:55")]
    // --- BannerColorPersistence banner-paste (BannerEditorView_OnTick_Patch.cs) ---
    [DataRow("SandBox.GauntletUI.BannerEditor.BannerEditorView", "BannerEditorView", "RefreshShieldAndCharacter", "Method", "BannerEditorView_OnTick_Patch.cs:21")]
    // --- SpecialResources transactional spend (PartyScreenLogic_AddCommand_Patch.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.Party.PartyScreenLogic+PartyCommand", "PartyCommand", "TotalNumber", "Member", "PartyScreenLogic_AddCommand_Patch.cs:71")]
    // --- EditorCacheRebuild path cache (PersistentPathCache.cs) ---
    [DataRow("TaleWorlds.Engine.PathReuseCache", "PathReuseCache", "_store", "Field", "PersistentPathCache.cs:149")]
    // --- EditorCacheRebuild distance-cache reflection web (NavigationCacheAdapter.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "_settlementToSettlementDistanceWithLandRatio", "Field", "NavigationCacheAdapter.cs:71")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "_fortificationNeighbors", "Field", "NavigationCacheAdapter.cs:73")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "_navigationType", "Property", "NavigationCacheAdapter.cs:76")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetAllRegisteredSettlements", "Method", "NavigationCacheAdapter.cs:79")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetUpdatedSettlementsForNeighborDetection", "Method", "NavigationCacheAdapter.cs:81")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "AddClosestEntrancePairBase", "Method", "NavigationCacheAdapter.cs:83")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "AddNeighbor", "Method", "NavigationCacheAdapter.cs:85")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "CheckBeingNeighbor", "Method", "NavigationCacheAdapter.cs:88")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetCacheElement", "Method", "NavigationCacheAdapter.cs:91")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetRealDistanceAndLandRatioBetweenSettlements", "Method", "NavigationCacheAdapter.cs:94")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "SetSettlementToSettlementDistanceWithLandRatio", "Method", "NavigationCacheAdapter.cs:97")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GenerateClosestSettlementToFaceCache", "Method", "NavigationCacheAdapter.cs:104")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "Serialize", "Method", "NavigationCacheAdapter.cs:110")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "Deserialize", "Method", "NavigationCacheAdapter.cs:113")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCacheElement`1", "NavigationCacheElement`1", "Sort", "Method", "NavigationCacheAdapter.cs:101")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.SandBoxNavigationCache", "SandBoxNavigationCache", "GetSceneXmlCrcValues", "Method", "NavigationCacheAdapter.cs:107")]
    public void ReflectionSite_ResolvesAgainstInstalledEngine(string fullName, string simpleName, string member, string kind, string source)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = ResolveType(fullName, simpleName);
        Assert.IsNotNull(type,
            $"Type '{fullName}' not found in any loaded engine assembly. The reflection site at {source} " +
            "would resolve null at runtime — the engine type was likely renamed or moved in this Bannerlord version.");

        Assert.IsTrue(HasMember(type, member, kind),
            $"{kind} '{member}' not found on {type.FullName}. The reflection site at {source} would resolve null " +
            "at runtime (silent feature degradation, no crash) — the member was likely renamed or removed in this Bannerlord version.");
    }

    // --- helpers ---

    private static Type ResolveType(string fullName, string simpleName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var a in assemblies)
        {
            var t = a.GetType(fullName, throwOnError: false);
            if (t != null) return t;
        }

        // Fallback: simple-name search (resilient to a namespace move that the full-name lookup misses).
        foreach (var a in assemblies)
        {
            Type[] types;
            try { types = a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x != null).ToArray(); }
            var hit = types.FirstOrDefault(x => x.Name == simpleName);
            if (hit != null) return hit;
        }
        return null;
    }

    private static bool HasMember(Type type, string member, string kind)
    {
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic |
                               BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            if ((kind == "Field" || kind == "Member") && t.GetField(member, F) != null) return true;
            if ((kind == "Property" || kind == "Member") && t.GetProperty(member, F) != null) return true;
            if ((kind == "Method" || kind == "Member") && t.GetMethods(F).Any(m => m.Name == member)) return true;
        }
        return false;
    }
}
