Plan 037: cut TAOM's per-party, per-frame and per-day costs on the campaign map without changing any
campaign decision: MCM reads moved off hot paths, cheap filters before expensive work, and one duplicate
engine query removed. Behaviour-preserving: AI choices, prices and outcomes stay identical.

FACTS (read-only audit 2026-10-02 at dffdf879; the writer re-reads each and quotes current code):
- Multipliers: 2,033 parties at +30 s on a new campaign rising to 3,042 in fast-forward; 4,770 to 4,879
  living heroes; 1,002 settlements (78 towns, 143 castles, 622 villages, 158 hideouts); FastForward 4x,
  Extra 8x, Ctrl+Space 16x scale per-frame campaign dt (TaomSettings.cs:427-437). The map ran 7 to 8 fps
  in fast-forward on the desktop (docs/migration/v1.5.2-impact.md:146-147).
- MCM read cost: TaomSettings.Instance is MCM GlobalSettings<T>.Instance: two ConcurrentDictionary
  operations plus a walk of the settings containers per read. The fix pattern is commit 7feca96b
  (BattleBalanceSettingsProvider caches the reference in its constructor and reads through it; live MCM
  edits still apply; an IL rule test pins it). Plan 031 applies it to the mission-side providers; this
  plan applies it to the campaign-side ones. Precondition per provider: never cache null (a provider
  resolved before MCM creates its settings needs a lazy cache).
CHANGES:
1. Caravan trade (Patch59, applied SubModule.cs:574-578): CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:40-41
   repeats AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty, which vanilla just
   ran (v1.5.3 TaleWorlds.CampaignSystem.cs:178114) for the same party and town: obtain vanilla's value
   instead (for example a prefix that captures the inputs and a postfix that reads what vanilla computed,
   or __state; read harmony-il.md lessons first) only if it is provably the identical value; otherwise
   cache the result per (party, town) for the duration of one destination search. CaravanTradeSettingsProvider.cs:21, 23
   read TaomSettings per call: cache.
2. Castle recruitment: Patch42_HourlyTickParty_Postfix.cs:60-64 reads IsEnabled and IsAiEnabled (two MCM
   lookups) before its party filter, for every party every hour: filter first. CastleAiToggle.cs:20-24
   reads both settings on every call from inside a transpiled vanilla loop: read through a cached
   provider.
3. Alignment desertion: AlignmentDesertionBehavior.cs:84-102 and AlignmentDesertionService.cs:26-51
   build a List and a DesertionTroopInfo per roster element before the owner and location gates, for
   every party and settlement every day: gate first; cache AlignmentDesertionSettingsProvider.cs:19.
4. Party speed (TaomPartySpeedModel.cs:70-80): the roster walk runs on every speed recompute for every
   moving party; run it only when the culture feat it serves can apply to that party (check the cheap
   condition first; verify which feat and condition in the code).
5. Per-frame settings reads on the map: RealmBordersSettingsProvider.cs:90-116 (about 12 reads per map
   frame from RealmBorderService.cs:241-299), FieldCommissionSettingsProvider.cs:50, :117-124 (a 7-read
   snapshot every frame from FieldCommissionBehavior.cs:51, :105-117), InventorySearch's per-frame MCM bool
   (InventorySearchCampaignBehavior.cs:40, :80-95), the PartyIconScaleConfig.GetScale read per icon build
   (PartyIconScaleConfig.cs:47), the TimeAcceleration mixin's per-frame read (TimeAccelerationMixin.cs:16,
   :87-91): cache each provider's settings reference.
6. Refuge: RefugeCampaignBehavior.cs:152-211 allocates a List and walks InvolvedParties on every world
   battle start and end even with no refuge: return early when the refuge book is empty.
7. Culture marketplace (CultureMarketplaceMaintenanceService.cs:26-77, TownRosterAdapter.cs:59-157):
   per town per day it resolves each routed item with MBObjectManager.GetObject and scans the whole town
   roster per item, and rebuilds two HashSets of the whole culture pool per call: build the id sets once
   per culture, resolve ItemObjects once, count the roster once per town tick. Same stock decisions.
TESTS: per change, a test that pins identical decisions (scores, stock picks, speeds, desertion results)
against the old path on representative fakes, plus the IL rule for every cached provider; the full suite
matches the baseline's failure set.
OUT OF SCOPE: diagnostics defaults (MapLoad heartbeat, AutoResolve battle log, EconomyDiagnostics:
FOR-MIKE, because changing an MCM default needs the rename trap handled and is the maintainer's call);
the XML merge (C5: measure first); PatchShield (plan 034); anything mission-side (plans 030 to 033).
STOP conditions to include: any change that alters an AI decision, a price or a stock result in a test;
a provider resolved before MCM exists whose lazy cache would change behaviour.
