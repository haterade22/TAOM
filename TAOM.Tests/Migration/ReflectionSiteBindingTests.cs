using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

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
    [DataRow("TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler", "MissionGauntletOrderOfBattleUIHandler", "_isActive", "Field", "OOBOverlayService.cs:64")]
    [DataRow("TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler", "MissionGauntletOrderOfBattleUIHandler", "_dataSource", "Field", "OOBOverlayService.cs:65")]
    // --- CompanionTactics role tooltips (RoleTooltipDecorator.cs) + manual patch (ManualPatchApplicator.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM", "PartyCharacterVM", "TypeIconData", "Property", "RoleTooltipDecorator.cs:40")]
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM", "OrderOfBattleHeroItemVM", "_cachedTooltipProperties", "Field", "RoleTooltipDecorator.cs:41")]
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM", "OrderOfBattleHeroItemVM", "GetCaptainTooltip", "Method", "ManualPatchApplicator.cs:24 (manual patch)")]
    // --- CharacterCreation race selector (FaceGenRaceSelectorRebuilder.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM", "FaceGenVM", "_selectedRace", "Field", "FaceGenRaceSelectorRebuilder.cs:208")]
    // --- SelectorVM<T> backing fields (FaceGen + CustomBattles commander selector) ---
    [DataRow("TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1", "SelectorVM`1", "_selectedIndex", "Field", "FaceGenRaceSelectorRebuilder.cs:211")]
    [DataRow("TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1", "SelectorVM`1", "_selectedItem", "Field", "FaceGenRaceSelectorRebuilder.cs:212")]
    [DataRow("TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1", "SelectorVM`1", "_onChange", "Field", "FaceGenRaceSelectorRebuilder.cs:213 / CommanderSelectorRebuilder.cs:21")]
    // --- CustomBattles faction injection (CustomBattleSideVM_Constructor_Patch.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.CustomBattle.CustomBattleSideVM", "CustomBattleSideVM", "OnCultureSelection", "Method", "CustomBattleSideVM_Constructor_Patch.cs:23")]
    // --- AdvancedCombat custom attacks (CustomAttacksUtils.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.Mission", "Mission", "RegisterBlow", "Method", "CustomAttacksUtils.cs:60")]
    // --- MissionPerf tick profiler: the private wait it times at its call site (MissionTickProfilerInstaller.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.Mission", "Mission", "WaitTickCompletion", "Method", "MissionTickProfilerInstaller.cs:31,54")]
    // --- MissionPerf attribution: the protected internal script tick it binds and swaps (MissionAttributionInstaller.cs) ---
    [DataRow("TaleWorlds.Engine.ScriptComponentBehavior", "ScriptComponentBehavior", "OnTick", "Method", "MissionAttributionInstaller.cs:35,66")]
    // --- MapPerf map profiler: the TickEvent listener walk (TickEventListenerWalker.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1", "MbEvent`1", "_nonSerializedListenerList", "Field", "TickEventListenerWalker.cs:47")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "Next", "Field", "TickEventListenerWalker.cs:51")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "<Action>k__BackingField", "Field", "TickEventListenerWalker.cs:54")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "<Owner>k__BackingField", "Field", "TickEventListenerWalker.cs:57")]
    // --- ShaderPrecompilation 1.4.7 headless-battle deployment-NRE guard (ShaderPrecompilePlayerAgentGuard.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.Mission", "Mission", "_initialPlayerAgent", "Field", "ShaderPrecompilePlayerAgentGuard.cs:44")]
    // --- BannerColorPersistence banner-paste (BannerEditorView_OnTick_Patch.cs) ---
    [DataRow("SandBox.GauntletUI.BannerEditor.BannerEditorView", "BannerEditorView", "RefreshShieldAndCharacter", "Method", "BannerEditorView_OnTick_Patch.cs:22")]
    // --- SpecialResources transactional spend (PartyScreenLogic_AddCommand_Patch.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.Party.PartyScreenLogic+PartyCommand", "PartyCommand", "TotalNumber", "Member", "PartyScreenLogic_AddCommand_Patch.cs:72")]
    // --- SpecialResources encyclopedia troop badge (EncyclopediaUnitBadgeMixin.cs, #590) ---
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items.EncyclopediaUnitVM", "EncyclopediaUnitVM", "_character", "Field", "EncyclopediaUnitBadgeMixin.cs:32")]
    // --- EditorCacheRebuild distance-cache reflection web (NavigationCacheAdapter.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "_settlementToSettlementDistanceWithLandRatio", "Field", "NavigationCacheAdapter.cs:71")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "_fortificationNeighbors", "Field", "NavigationCacheAdapter.cs:73")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "_navigationType", "Property", "NavigationCacheAdapter.cs:76")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetAllRegisteredSettlements", "Method", "NavigationCacheAdapter.cs:79")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetUpdatedSettlementsForNeighborDetection", "Method", "NavigationCacheAdapter.cs:81")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "AddClosestEntrancePairBase", "Method", "NavigationCacheAdapter.cs:83")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "AddNeighbor", "Method", "NavigationCacheAdapter.cs:85")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "CheckBeingNeighbor", "Method", "NavigationCacheAdapter.cs:374")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetCacheElement", "Method", "NavigationCacheAdapter.cs:388")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GetRealDistanceAndLandRatioBetweenSettlements", "Method", "NavigationCacheAdapter.cs:404")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "SetSettlementToSettlementDistanceWithLandRatio", "Method", "NavigationCacheAdapter.cs:420")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "GenerateClosestSettlementToFaceCache", "Method", "NavigationCacheAdapter.cs:104")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "Serialize", "Method", "NavigationCacheAdapter.cs:110")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1", "NavigationCache`1", "Deserialize", "Method", "NavigationCacheAdapter.cs:113")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCacheElement`1", "NavigationCacheElement`1", "Sort", "Method", "NavigationCacheAdapter.cs:101")]
    [DataRow("TaleWorlds.CampaignSystem.Map.DistanceCache.SandBoxNavigationCache", "SandBoxNavigationCache", "GetSceneXmlCrcValues", "Method", "NavigationCacheAdapter.cs:107")]
    // --- SettlementGuards patch-body reflection (patch TARGETS are auto-covered by HarmonyPatchBindingTests) ---
    [DataRow("SandBox.CampaignBehaviors.GuardsCampaignBehavior", "GuardsCampaignBehavior", "PrepareGuardAgentDataFromGarrison", "Method", "GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:32")]
    [DataRow("SandBox.CampaignBehaviors.GuardsCampaignBehavior", "GuardsCampaignBehavior", "_garrisonTroops", "Field", "GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.cs:33")]
    // --- Player Switcher (#514). The single load-bearing reflection site of the feature.
    // Campaign.PlayerDefaultFaction is internal, Clan.PlayerClan is a computed getter over it, and
    // ChangePlayerCharacterAction never updates it. Without this write the player clan pointer stays
    // on the abandoned character-creation clan, CharacterDeveloperVM throws enumerating its Heroes,
    // and KillCharacterAction's victim.Clan != Clan.PlayerClan guard stops that clan being destroyed.
    [DataRow("TaleWorlds.CampaignSystem.Campaign", "Campaign", "PlayerDefaultFaction", "Property", "PlayerIdentityAdapter.cs:35")]
    // --- Diplomacy Patch80 kingdom-vote deadlock (#547, #550): the members KingdomVoteDeadlockBinding
    // caches once in Initialize. Patch80KingdomVoteDeadlockBindingTests pins their shapes; these pin
    // existence. ExecuteDone is protected and is what seam D runs on a pre-concluded election.
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.KingdomDecisionsVM", "KingdomDecisionsVM", "_examinedDecisionsSinceInit", "Field", "KingdomVoteDeadlockBinding.cs")]
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.KingdomDecisionsVM", "KingdomDecisionsVM", "_shouldCheckForDecision", "Property", "KingdomVoteDeadlockBinding.cs")]
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes.DecisionItemBaseVM", "DecisionItemBaseVM", "_decision", "Field", "KingdomVoteDeadlockBinding.cs")]
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes.DecisionItemBaseVM", "DecisionItemBaseVM", "_onDecisionOver", "Field", "KingdomVoteDeadlockBinding.cs")]
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes.DecisionItemBaseVM", "DecisionItemBaseVM", "ExecuteDone", "Method", "KingdomVoteDeadlockBinding.cs")]
    // --- BanditManagement hideout boss fight (#564): the three private fields Patch86_HideoutAmbushBossFight
    // injects as ____name parameters. HarmonyFieldInjectionNamingTests pins the underscore count; these pin
    // the members, and Patch86HideoutBossFightBindingTests pins their types.
    [DataRow("SandBox.Missions.MissionLogics.Hideout.HideoutAmbushMissionController", "HideoutAmbushMissionController", "_allEnemyTroops", "Field", "Patch86_HideoutAmbushBossFight.cs")]
    [DataRow("SandBox.Missions.MissionLogics.Hideout.HideoutAmbushMissionController", "HideoutAmbushMissionController", "_overriddenHideoutBossAgentOrigin", "Field", "Patch86_HideoutAmbushBossFight.cs")]
    [DataRow("SandBox.Missions.MissionLogics.Hideout.HideoutAmbushMissionController", "HideoutAmbushMissionController", "_allEnemyTroopTypesCache", "Field", "Patch86_HideoutAmbushBossFight.cs")]
    // --- MonsterSize (#646): the size pass writes the private BodyLength setter, then recomputes the item's cached
    // Effectiveness. A missing setter leaves every sized mount at its item's placeholder 1.0x.
    [DataRow("TaleWorlds.Core.HorseComponent", "HorseComponent", "set_BodyLength", "Method", "MonsterSizeCatalogAdapter.cs:23")]
    [DataRow("TaleWorlds.Core.ItemObject", "ItemObject", "CalculateEffectiveness", "Method", "MonsterSizeCatalogAdapter.cs:28")]
    [DataRow("TaleWorlds.Core.ItemObject", "ItemObject", "set_Effectiveness", "Method", "MonsterSizeCatalogAdapter.cs:30")]
    // --- Creature Bandits route A (#692): the weapon-state probe. Missing: route A is skipped for every creature.
    [DataRow("TaleWorlds.MountAndBlade.Agent", "Agent", "_primaryWieldedItemIndexPointer", "Field", "CreatureRouteAUnmount.cs:22")]
    // --- ArmourAcquisition: the gate marks heavy, elite, lord and named pieces NotMerchandise at every game init
    // through the private setter. A missing setter turns gating off for the game (logged as an error).
    [DataRow("TaleWorlds.Core.ItemObject", "ItemObject", "set_NotMerchandise", "Method", "ArmourItemCatalogAdapter.cs:20")]
    // --- FactionUI (#704): Kysaro's front end. Each missing member silently degrades one thing: the themed
    // fonts' glyphs are not drawn, vanilla retries menu mode every frame on the main menu, the face
    // generator skips its fade-in, the backstory Random button presses nothing.
    [DataRow("TaleWorlds.TwoDimension.SpriteCategory", "SpriteCategory", "set_IsLoaded", "Method", "FrontEndResourceAdapter.cs:27")]
    [DataRow("TaleWorlds.MountAndBlade.MBMusicManager", "MBMusicManager", "set_CurrentMode", "Method", "MenuMusicAdapter.cs:12")]
    [DataRow("TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM", "FaceGenVM", "_faceGeneratorScreen", "Field", "CharacterCreationWidgets.cs:44")]
    [DataRow("TaleWorlds.GauntletUI.BaseTypes.ButtonWidget", "ButtonWidget", "HandleClick", "Method", "NarrativeRandomButton.cs:22")]
    // --- LoadTimeStamps (plan 040): the [Lifecycle] listener swap walks MbEvent's private listener records and
    // sets their private Action. Missing: per-handler timing is off for the session, dispatch totals only.
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1", "MbEvent`1", "_nonSerializedListenerList", "Field", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "Next", "Field", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "Action", "Property", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "set_Action", "Method", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "Owner", "Property", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`2", "MbEvent`2", "_nonSerializedListenerList", "Field", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2", "EventHandlerRec`2", "Next", "Field", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2", "EventHandlerRec`2", "Action", "Property", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2", "EventHandlerRec`2", "set_Action", "Method", "CampaignListenerAdapter.cs")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2", "EventHandlerRec`2", "Owner", "Property", "CampaignListenerAdapter.cs")]
    // --- XmlMerge (plan 042): the fast path loads each file through the engine's private loader. A miss turns the fast
    // path off for the session (logged at start); every module XML merge then runs the engine's own code.
    [DataRow("TaleWorlds.ObjectSystem.MBObjectManager", "MBObjectManager", "CreateDocumentFromXmlFile", "Method", "XmlMergeEngineAdapter.cs:53")]
    // --- TournamentRewards (Patch96): the prize the player picks at Join is written through the private setter.
    // Missing: a warning per Join and the advertised prize stands, though the dialog promised the pick.
    [DataRow("TaleWorlds.CampaignSystem.TournamentGames.TournamentGame", "TournamentGame", "set_Prize", "Method", "TournamentJoinAdapter.cs:18")]
    // --- CreatureSiegeRole: a siege tower's navmesh id start is a protected field, read through AccessTools.FieldRefAccess.
    // A miss binds nothing (fail-soft): every tower then reads as having no navmesh (start 0) and the role rules skip it with a named warning.
    [DataRow("TaleWorlds.MountAndBlade.MissionObject", "MissionObject", "DynamicNavmeshIdStart", "Field", "CreatureSiegeMissionAdapter.cs:227")]
    // --- NameplateCull (Patch104): the three private fields the adapter binds with AccessTools.FieldRefAccess.
    // A miss makes Initialize return a reason, the cull stays off and the vanilla nameplate update runs (one WARNING line).
    [DataRow("SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM", "SettlementNameplatesVM", "_mapCamera", "Field", "NameplateCullAdapter.cs:49")]
    [DataRow("SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM", "SettlementNameplateVM", "_bindIsVisibleOnMap", "Field", "NameplateCullAdapter.cs:50")]
    [DataRow("SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM", "SettlementNameplateVM", "_worldPos", "Field", "NameplateCullAdapter.cs:51")]
    // --- MissionStartGuard (Patch103): the six start calls the transpiler swaps, found by AccessTools.Method at install.
    // A miss makes AccessTools.Method return null; Rewrite's fit check rejects the null target, so Mission.AfterStart stays vanilla.
    [DataRow("TaleWorlds.MountAndBlade.MBSubModuleBase", "MBSubModuleBase", "OnBeforeMissionBehaviorInitialize", "Method", "MissionStartGuardSwaps.cs:23")]
    [DataRow("TaleWorlds.MountAndBlade.MissionBehavior", "MissionBehavior", "OnBehaviorInitialize", "Method", "MissionStartGuardSwaps.cs:24")]
    [DataRow("TaleWorlds.MountAndBlade.MBSubModuleBase", "MBSubModuleBase", "OnMissionBehaviorInitialize", "Method", "MissionStartGuardSwaps.cs:25")]
    [DataRow("TaleWorlds.MountAndBlade.MissionBehavior", "MissionBehavior", "EarlyStart", "Method", "MissionStartGuardSwaps.cs:26")]
    [DataRow("TaleWorlds.MountAndBlade.MissionBehavior", "MissionBehavior", "AfterStart", "Method", "MissionStartGuardSwaps.cs:27")]
    [DataRow("TaleWorlds.MountAndBlade.MissionObject", "MissionObject", "AfterMissionStart", "Method", "MissionStartGuardSwaps.cs:28")]
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

    /// <summary>
    /// A "File.cs:N" or "File.cs:N,M" label is only a pointer, and it drifts when code above the site
    /// changes. It shows only in a failure message, so nothing else catches the drift. This reads the
    /// labels off the DataRows above and checks each labelled line still names its member (a property
    /// accessor by its property name). Comments are blanked first, so a doc comment that mentions the
    /// member cannot satisfy a label. No game needed: it reads source files only, so it carries no
    /// BindingVerification category and runs on any machine with the repo.
    ///
    /// Convention: label the line that spells the member (its string literal, its nameof, or the name
    /// filter inside the lookup), not the first line of a multi-line call and not a declaration. A label
    /// with no line number only has to name a file that exists under Main/.
    ///
    /// Limit: RepoPaths.StripComments is not string-aware, so a "//" or "/*" inside a string literal blanks
    /// the code after it. If this test reports a line that does spell its member, look for such a string on
    /// that line or above it, and put the lookup on a line of its own.
    /// </summary>
    [TestMethod]
    public void EveryLineLabel_PointsAtALineThatNamesItsMember()
    {
        var labelPattern = new Regex(@"(?<file>[\w.]+\.cs)(?::(?<lines>\d+(?:,\d+)*))?");
        var failures = new List<string>();
        var checkedLabels = 0;

        foreach (var (member, source) in CatalogueRows())
        {
            var needles = new List<string> { member };
            if (member.StartsWith("get_") || member.StartsWith("set_")) needles.Add(member.Substring(4));

            foreach (Match label in labelPattern.Matches(source))
            {
                var fileName = label.Groups["file"].Value;
                var files = SourceFilesNamed(fileName);
                if (files.Count == 0)
                {
                    failures.Add($"{fileName}: no such file under Main/ (member {member})");
                    continue;
                }
                if (!label.Groups["lines"].Success) continue;

                var fileLines = files
                    .Select(f => RepoPaths.StripComments(File.ReadAllText(f)).Split('\n'))
                    .ToList();
                foreach (var n in label.Groups["lines"].Value.Split(',').Select(int.Parse))
                {
                    checkedLabels++;
                    var ok = fileLines.Any(l => n >= 1 && n <= l.Length && needles.Any(x => l[n - 1].Contains(x)));
                    if (ok) continue;

                    var now = fileLines.SelectMany(l => Enumerable.Range(1, l.Length)
                            .Where(i => needles.Any(x => l[i - 1].Contains(x))))
                        .Distinct().OrderBy(i => i);
                    failures.Add($"{fileName}:{n} -> {member} now appears (outside comments) at line(s): "
                        + (now.Any() ? string.Join(", ", now) : "none"));
                }
            }
        }

        Assert.IsTrue(checkedLabels > 0, "No line labels were found; the label pattern or the DataRow layout changed.");
        Assert.AreEqual(0, failures.Count,
            $"{failures.Count} stale line label(s). Repoint each DataRow and its reflection-sites.md row to the "
            + "line that spells the member in the reflection lookup:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Every DataRow's member and source label must be repeated in a row of reflection-sites.md, so the
    /// catalogue cannot lose a site the test pins.
    /// </summary>
    [TestMethod]
    public void EveryDataRow_HasAMatchingRowInTheCatalogue()
    {
        var doc = RepoPaths.ReadSource("docs/reference/taleworlds-api-snapshot/reflection-sites.md")
            .Split('\n');
        var tokenPattern = new Regex(@"[\w.]+\.cs(?::\d+(?:,\d+)*)?");
        var failures = new List<string>();

        foreach (var (member, source) in CatalogueRows())
        {
            var tokens = tokenPattern.Matches(source).Cast<Match>().Select(m => m.Value).ToList();
            var cell = $"| `{member}` |";
            var found = doc.Any(l => l.Contains(cell) && tokens.All(t => HasExactToken(l, t)));
            if (!found)
                failures.Add($"{member} ({source}): no catalogue row with that member and label");
        }

        Assert.AreEqual(0, failures.Count,
            $"{failures.Count} DataRow(s) missing from reflection-sites.md Category B:\n" + string.Join("\n", failures));
    }

    // A label token matches only whole: "File.cs:23" must not match "File.cs:230" or "File.cs:23,30", and
    // "File.cs" must not match "OtherFile.cs" (Codex pass 2, 2026-10-09).
    private static bool HasExactToken(string line, string token) =>
        Regex.IsMatch(line, @"(?<![\w.])" + Regex.Escape(token) + @"(?![\w:,])");

    [TestMethod]
    public void HasExactToken_LongerLineNumberOrLongerFileName_DoesNotMatch()
    {
        Assert.IsTrue(HasExactToken("| `MonsterSizeCatalogAdapter.cs:23` | x |", "MonsterSizeCatalogAdapter.cs:23"));
        Assert.IsFalse(HasExactToken("| `MonsterSizeCatalogAdapter.cs:230` | x |", "MonsterSizeCatalogAdapter.cs:23"));
        Assert.IsFalse(HasExactToken("| `X.cs:35,66,70` | x |", "X.cs:35,66"));
        Assert.IsFalse(HasExactToken("| `OtherAdapter.cs` | x |", "Adapter.cs"));
        Assert.IsFalse(HasExactToken("| `Adapter.cs:12` | x |", "Adapter.cs"));
    }

    private static IEnumerable<(string Member, string Source)> CatalogueRows()
    {
        var method = typeof(ReflectionSiteBindingTests).GetMethod(nameof(ReflectionSite_ResolvesAgainstInstalledEngine));
        return method.GetCustomAttributes<DataRowAttribute>()
            .Select(r => ((string)r.Data[2], (string)r.Data[4]))
            .ToList();
    }

    private static List<string> SourceFilesNamed(string fileName)
    {
        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(RepoPaths.RepoPath("Main"), fileName, SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
            .ToList();
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
