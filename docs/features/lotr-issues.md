# LOTR Issues — Vanilla Quest/Issue Conversion

> **Status: IMPLEMENTED (2026-06-20).** All 43 vanilla procedural issues are suppressed and replaced by 43
> TAOM-authored LOTR issues, built on a generic-template + XML-config architecture in `Main/Features/LotrIssues/`.
> See **Implementation (as built)** below for the shipped design; the disposition matrix and risk analysis that
> follow are the original research deliverable, kept for provenance. Engine mechanics are documented in
> [issue-and-quest-system.md](../reference/engine/issue-and-quest-system.md).

## Overview

Bannerlord's campaign generates ~43 procedural **issues** — "problems at a notable" the player solves for reward
(deliver grain, clear a bandit base, escort a caravan, recruit gang members, …). Their gameplay is already
culture-relative (troops/items derive from the issue-giver's culture, which in TAOM is a LOTR culture), but their
hard-coded English **flavor text** and a handful of vanilla-specific archetypes don't fit Middle-earth. This plan
**replaces the vanilla issues with custom LOTR issues and disables the vanilla ones**, so the only issues that spawn are
TAOM-authored and lore-appropriate.

## Why This Exists

- **Vanilla behavior:** 43 issue behaviors register in sandbox play (36 in `SandBoxManager.Initialize`, 7 in
  `SandBoxSubModule`) and spawn continuously at town/village notables and lords.
- **TAOM requirement:** issues that read as Middle-earth (an orc warband raiding the Westfold, seed-corn for a blighted
  Gondorian steading, Corsair smugglers in Pelargir) rather than Calradic generics — and no Calradia-named text.
- **Without this:** the campaign's most frequent player-facing content (issues appear at nearly every settlement)
  silently undercuts the total conversion's immersion, even though the rest of the world is fully reskinned.

**Scope note — the main storyline is moot.** TAOM is sandbox-only (no `StoryMode` dependency; `StoryModeNewGame` hidden
by [MainMenuCustomizer](../../Main/Features/MainMenuCustomizer/MainMenuCustomizerService.cs)), so the Dragon Banner /
Neretzes conspiracy / Istiana-Arzagos storyline **never spawns**. It needs no conversion. A bespoke LOTR main quest is a
separate future effort (sketched in the appendix).

## Per-Issue Disposition Matrix

The complete inventory of the 43 sandbox issues with a per-issue disposition. Columns:

| Column | Meaning |
|--------|---------|
| **Issue** | the `*IssueBehavior` class |
| **Src** | `SandBoxManager.cs:NNN` (CampaignSystem) or `SandBoxSubModule.cs:NNN` (SandBox module) |
| **Giver** | notable kind (gang leader / headman / merchant / artisan / rural notable) or lord / landlord |
| **Freq** | `IssueFrequency` (VeryCommon / Common / Rare) |
| **Quest?** | has a nested `QuestBase` (the more involved issues) |
| **Deps** | item/troop/settlement references and whether they resolve in TAOM (culture-derived & `DefaultItems` resolve by construction) |
| **Lore-break** | does displayed text name Calradia / vanilla factions / vanilla lore? |
| **Disposition** | `Replace` (author a LOTR analog + drop the vanilla) / `Reskin` (keep, override text only) / `Drop` (suppress, no analog) |
| **LOTR analog** | the Middle-earth concept the replacement embodies |
| **Cultures** | LOTR cultures the replacement fits, or *all* |

**Disposition philosophy** (per the decision to replace + disable): the default is `Replace` for the combat / bandit /
escort / delivery / economic archetypes that have a clean Middle-earth analog, and `Drop` for niche or odd issues with
no compelling analog. `Reskin` is used only where a text-only override is clearly better than dropping. Every vanilla
issue behavior on the list is **suppressed** regardless of disposition (see Suppression below); `Replace`/`Reskin`
indicate whether a TAOM custom issue takes its slot.

**Audit outcome (full 43):** every archetype has a Middle-earth analog, so the audit dispositions **41 Replace, 1
Reskin** (`GangLeaderNeedsSpecialWeapons` — crafting-based, text-only conversion), **0 Drop**. All 43 vanilla behaviors
are suppressed regardless of disposition; `Replace`/`Reskin` only indicates whether a TAOM custom issue fills the slot.
`Src` = `SBM:NNN` (`SandBoxManager.cs`) or `SBS:NN` (`SandBoxSubModule.cs`). A ⚠ in **Deps** flags a vanilla-side item
that returns null / self-disables (all moot once replaced — see Dependency risks below).

| # | Issue | Src | Giver | Freq | Quest? | Deps (flag risks) | Lore? | Disp. | LOTR analog (≤12 words) | Cultures |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | ArmyNeedsSupplies | `SBM:162` | lord | VeryCommon | Y | Grain/wine/sheep + livestock cat; meat→giver only | N | Replace | Mustering host needs provisioning before it marches | all |
| 2 | ArtisanCantSellProductsAtAFairPrice | `SBM:163` | artisan | Common | Y | Smuggle 1 of 7 trade goods (hardwood/hides MCP-false) | N | Replace | Craftsman bound by price-fixing edict, smuggle goods to ally | gondor, dale, erebor, rohan, dunland |
| 3 | ArtisanOverpricedGoods | `SBM:164` | artisan | Common | Y | Deliver 1 of 6 raw goods (iron/hardwood MCP-false) | N | Replace | Craftsman gouged by merchant cartel on raw materials | gondor, erebor, dale, rohan, dunland, umbar, rhun, harad |
| 4 | CapturedByBountyHunters | `SBM:165` | gangleader | Common | Y | "looter" gate + nearest infested hideout | N | Replace | Crime boss asks player to free captured gang members | dunland, harad, umbar, rhun |
| 5 | CaravanAmbush | `SBM:166` | merchant | Common | Y | ⚠ literal "grain" null-AddToCounts; fish/butter/sumpter ok | N | Replace | Bait decoy caravan to spring counter-ambush on raiders | gondor, dale, erebor, rohan, dunland, rhun, harad, umbar |
| 6 | EscortMerchantCaravan | `SBM:167` | merchant | VeryCommon | Y | ⚠ "hardwood" missing → self-disables whole issue | Y | Replace | Escort merchant's caravan through war-torn roads to towns | gondor, dale, erebor, rohan, umbar, harad, rhun, dunland |
| 7 | ExtortionByDeserters | `SBM:168` | headman | Common | Y | Grain/Meat core + culture mount/templates; all dynamic | N | Replace | Village ambushes deserters extorting food and killing folk | gondor, rohan, dale, dunland, mirkwood, erebor, rhun, harad |
| 8 | GangLeaderNeedsToOffloadStolenGoods | `SBM:169` | gangleader | Common | Y | jewelry/fur/silver/velvet resolve; culture BanditChief | N | Replace | Black-market fence sells plundered caravan loot at hideout | all |
| 9 | GangLeaderNeedsWeapons | `SBM:170` | gangleader | Common | Y | OneHandedAxe class + guard_<culture>/militia; dynamic | N | Replace | Smuggle weapons past town guards to arm thugs | umbar, harad, mordor, dunland, isengard, gondor |
| 10 | RevenueFarming | `SBM:171` | lord | VeryCommon | Y | Village PrimaryProduction + core traits; dynamic | Y | Replace | Lord deputizes player to collect overdue village tithes | gondor, rohan, dale, dunland, mordor, isengard, harad, rhun, umbar |
| 11 | HeadmanNeedsGrain | `SBM:172` | headman | Common | Y | Grain item + Grain cat core; supply-town search; safe | N | Replace | Blighted steading lost seed-corn, bring grain before famine | gondor, rohan, dunland, erebor |
| 12 | HeadmanNeedsToDeliverAHerd | `SBM:173` | headman | VeryCommon | Y | sheep/cow/hog + Grain core; all resolve | N | Replace | Drive village livestock herd safely to distant market town | rohan, gondor, dale, dunland |
| 13 | HeadmanVillageNeedsDraughtAnimals | `SBM:174` | headman | VeryCommon | Y | cow/mule/sumpter ok; meat MCP-false (vanilla, runtime ok) | N | Replace | Buy draught animals to replace village's lost livestock | rohan, gondor, dale, dunland, erebor |
| 14 | LadysKnightOut | `SBM:175` | lord | Common | Y | Tournament towns runtime; prize is event payload | N | Replace | Noblewoman's champion in tournament, dedicate victories to her | gondor, rohan, dale, dunland |
| 15 | LandLordCompanyOfTrouble | `SBM:176` | lord | Rare | Y | company_of_trouble_character + random hideout; resolve | N | Replace | Manage troublesome mercenaries, re-sell contract before they turn | all |
| 16 | LandLordTheArtOfTheTrade | `SBM:177` | rural | VeryCommon | Y | Village PrimaryProduction dynamic; core skills | N | Replace | Sell loaned village surplus at profit, return the price | gondor, rohan, dale, erebor, dunland, harad |
| 17 | LandlordNeedsAccessToVillageCommons | `SBM:178` | rural | Common | Y | sumpter_horse + culture Villager; all dynamic | N | Replace | Escort herders to disputed pasture, drive off rivals | rohan, gondor, dale, dunland |
| 18 | LandLordNeedsManualLaborers | `SBM:179` | rural | VeryCommon | Y | Player bandit prisoners + mine village; core traits | N | Replace | Deliver bandit prisoners as forced mine labor | mordor, isengard, dunland, erebor, harad, rhun |
| 19 | LandlordTrainingForRetainers | `SBM:180` | rural | VeryCommon | Y | Grain + borrowed_troop/veteran + culture; resolve | N | Replace | Train lent green retainers in battle into veterans | rohan, gondor, rhun, harad, dunland |
| 20 | LordNeedsGarrisonTroops | `SBM:181` | lord | Common | Y | Culture basic-troop tree + EliteBasicTroop; dynamic | N | Replace | Bring fresh culture recruits to reinforce a garrison | all |
| 21 | TheConquestOfSettlement | `SBM:182` | lord | VeryCommon | Y | At-war town/castle target; dynamic tokens | N | Replace | Liege orders you to besiege named enemy stronghold | gondor, rohan, mordor, isengard, erebor, dale, rhun, harad, gundabad, dolguldur |
| 22 | VillageNeedsCraftingMaterials | `SBM:183` | rural | Rare | Y | IronIngot1/IronIngot2 core; resolve | N | Replace | Deliver iron ingots so village smith can reforge | all |
| 23 | Smugglers | `SBM:184` | lord | Rare | Y | ⚠ literal "grain" returns null (food roster); rest ok | N | Replace | Lord asks you to break a smuggling caravan | gondor, rohan, dale, umbar, harad, dunland |
| 24 | LordNeedsHorses | `SBM:185` | lord | VeryCommon | Y | Culture mount pool + sumpter_horse fallback; resolve | N | Replace | Deliver fresh culture mounts to a horse-starved lord | rohan, gondor, dunland, harad, rhun, isengard |
| 25 | LordsNeedsTutor | `SBM:186` | lord | Common | Y | Jewelry-cat reward + clan young hero; dynamic | N | Replace | Mentor a lord's young heir in arts of war | gondor, rohan, erebor, dale, mirkwood, rivendell, lothlorien, dunland, harad, rhun |
| 26 | LordWantsRivalCaptured | `SBM:187` | lord | Rare | Y | Enemy lord target + culture Guard; dynamic | N | Replace | Capture a hated rival lord alive, deliver prisoner | all |
| 27 | MerchantArmyOfPoachers | `SBM:188` | merchant | Common | Y | "leather" + "poacher" troop + bandit clan; resolve | N | Replace | Clear merchant's poachers-turned-gang from a bound village | gondor, rohan, dale, dunland, harad |
| 28 | MerchantNeedsHelpWithOutlaws | `SBM:189` | merchant | VeryCommon | Y | Nearest infested hideout + bandit parties; no goods | N | Replace | Clear N raiding bands plaguing merchant's trade roads | gondor, rohan, dale, erebor, dunland, mirkwood, lothlorien, umbar, harad, rhun |
| 29 | NearbyBanditBase | `SBM:190` | headman | VeryCommon | Y | Nearest infested hideout; all dynamic | Y | Replace | Clear orc/warg/brigand lair preying on travellers | all |
| 30 | RaidAnEnemyTerritory | `SBM:191` | lord | VeryCommon | Y | At-war kingdom + razed villages; dynamic | N | Replace | Raze enemy kingdom's villages to tie up lords | all |
| 31 | ScoutEnemyGarrisons | `SBM:192` | lord | VeryCommon | Y | Enemy fortifications + Scouting skill; dynamic | N | Replace | Scout three enemy strongholds before a war push | gondor, rohan, mordor, isengard, erebor, dale, dunland, harad, rhun, umbar, gundabad, dolguldur |
| 32 | VillageNeedsTools | `SBM:193` | headman | VeryCommon | Y | Tools core + village PrimaryProduction; resolve | N | Replace | Deliver tools to help village restore production | all |
| 33 | GangLeaderNeedsRecruits | `SBM:194` | gangleader | VeryCommon | Y | Player bandit-occupation troops; gold only | N | Replace | Deliver recruited outlaws to swell a gang | dunland, umbar, harad, rhun, gundabad |
| 34 | GangLeaderNeedsSpecialWeapons | `SBM:195` | gangleader | VeryCommon | Y | "Dagger" crafting template + ICraftingBehavior + skill | N | Reskin | Forge concealable daggers in a smithy for gang | all |
| 35 | LesserNobleRevolt | `SBM:196` | lord | Rare | Y | Culture elite tree tier5/6 + Grain; resolve | Y | Replace | Put down renegade noble stirring peasant tax-revolt | gondor, rohan, dale, dunland, erebor, harad, rhun |
| 36 | BettingFraud | `SBM:197` | gangleader | Rare | Y | ⚠ betting_fraud_thug_male/female unverified vanilla ids | Y | Replace | Tournament match-fixing partnership with a crooked bookmaker | gondor, rohan, dale, dunland, umbar, harad, rhun |
| 37 | RivalGangMovingIn | `SBS:74` | gangleader | Common | Y | looter/mercenary_1-8/gangster_2-3 (SandBoxCore) resolve | N | Replace | Night-alley ambush of a rival gang's notable | umbar, harad, rhun, dunland, gondor, isengard, mordor |
| 38 | RuralNotableInnAndOut | `SBS:75` | rural | Common | Y | Culture BoardGame + tavern GameHost; dynamic | Y | Replace | Win back gambled-away land deed at tavern board game | gondor, rohan, dale, dunland |
| 39 | FamilyFeud | `SBS:76` | rural | Rare | Y | townsman_<culture> + pugio + gangster_1; resolve | N | Replace | Shelter culprit kinsman from a vengeful blood-feud | rohan, dunland, dale, gondor |
| 40 | NotableWantsDaughterFound | `SBS:77` | rural | Rare | Y | ⚠ vanilla bandit-clan ids stripped by TAOM → null, falls back | N | Replace | Find a headman's eloped/abducted missing daughter | gondor, rohan, dale, dunland, erebor |
| 41 | TheSpyParty | `SBS:78` | lord | Rare | Y | 4 *_contender_<diff> troops (verified) + arena; resolve | Y | Replace | Unmask and duel an enemy spy at a tournament | gondor, rohan, erebor, dale |
| 42 | ProdigalSon | `SBS:79` | lord | Rare | Y | gangster_1/2/3 + clan young lord; resolve | N | Replace | Free a lord's debt-held kinsman from a gang | gondor, rohan, dale, dunland, umbar, harad, rhun |
| 43 | SnareTheWealthy | `SBS:81` | gangleader | Common | Y | ⚠ literal "grain" returns null (caravan/gang cargo); rest ok | N | Replace | Pose as guard, lead corrupt merchant's caravan into ambush | umbar, harad, dunland, mordor, rhun |

### Disposition tally

**Replace: 41** · **Reskin: 1** (`GangLeaderNeedsSpecialWeapons`) · **Drop: 0** · **Total: 43** ✓

### Dependency risks (vanilla issues only — moot once replaced)

Every issue is dispositioned Replace/Reskin, so each replacement quest re-sources its own items — these vanilla-side
risks disappear once the vanilla issue is swapped out. Listed for completeness and in case any vanilla issue is ever
kept:

- **EscortMerchantCaravan (`SBM:167`) — `hardwood` SELF-DISABLE GATE (highest-impact).** `InitializeOnStart` (line
  1369) requires `hardwood` AND `sumpter_horse`; if either is missing the behavior removes its own listeners and
  completes all instances — a missing `hardwood` silently kills the *entire* issue at game start. The one literal-id
  case the audit flags as a hard self-disable, not just a null lookup.
- **CaravanAmbush (`SBM:166`) / Smugglers (`SBM:184`) / SnareTheWealthy (`SBS:81`) — literal `grain` null-deref.**
  `MBObjectManager.GetObject<ItemObject>("grain")` feeds a party food/cargo roster; `item_exists("grain")` returned
  false → would `AddToCounts(null)`. (Sibling literals `fish`/`butter`/`sumpter_horse` resolve.)
- **NotableWantsDaughterFound (`SBS:77`) — stripped vanilla bandit-clan ids.** The rogue is keyed on
  `steppe_bandits / mountain_bandits / desert_bandits / forest_bandits / sea_raiders`, all of which TAOM's
  BanditManagement strips — `.Culture.BanditBoss` likely returns null and falls through to the `NotableTemplates`
  fallback (graceful, not a hard crash).
- **BettingFraud (`SBM:197`) — unverified ids.** `betting_fraud_thug_male` / `_female` were not verified against TAOM
  ModuleData (`resolvesInTaom: "unknown"`).
- **HeadmanVillageNeedsDraughtAnimals (`SBM:174`) / ArmyNeedsSupplies (`SBM:162`) — `meat` MCP-false.** Vanilla
  SandBoxCore good; resolves at runtime.

**MCP-index caveat.** The `taom-moduledata` MCP indexes LOTRLOME_items + TAOM ModuleData, **not** vanilla SandBoxCore
base goods, so `grain` / `iron` / `hardwood` / `hides` / `meat` reporting `exists:false` is most likely a **false
negative** that resolves in-engine from the full object registry (the audit upgraded `iron`/`hardwood` in
ArtisanOverpricedGoods back to `yes` on this basis). The genuine-risk cases are (a) EscortMerchantCaravan's `hardwood`
self-disable gate (a missing-or-not check, where a false negative still flips the gate) and (b) the bare
`GetObject("grain")` → `AddToCounts(null)` sites. **If any vanilla issue were ever kept rather than replaced, confirm
these base-good ids in-game first.**

### Lore-breaking text

Issues whose displayed text names Calradian content (the currency word **"denars"** unless noted):

- **EscortMerchantCaravan (`SBM:167`)** — "denars" in `PlayerStartsQuestLogText`; borderline (drops to false if TAOM
  globally remaps `str_money`).
- **RevenueFarming (`SBM:171`)** — "denars" repeated in displayed text.
- **NearbyBanditBase (`SBM:190`)** — "denars" in the reward log.
- **LesserNobleRevolt (`SBM:196`)** — vanilla per-culture noble **titles** (`Druzhinnik`/`Knight`/`Fian`/`Equite`/
  `Kheshig`/`Faris`/`Huscarl`) via `{TOP_TIER_CAV_TITLE}` / `{MALE_LESSER_NOBLE_TITLE}` — these resolve to *empty* for
  any LOTR culture (no case matches), the more serious break here than the currency word.
- **BettingFraud (`SBM:197`)** — "denars" in dialog.
- **RuralNotableInnAndOut (`SBS:75`)** — "denars" in gambling dialog.
- **TheSpyParty (`SBS:78`)** — "denars" in reward logs.

> **Reading a row — `HeadmanNeedsGrain` (#11):** deliver difficulty-scaled grain to a village headman (or send a
> companion + men to buy it). All deps safe — `DefaultItems.Grain` + `DefaultItemCategories.Grain` (engine-core), a
> `SettlementHelper.FindNearest…` supply-town search, generic player-party troops — and **17 text variables**
> (`ISSUE_SETTLEMENT`, `GRAIN_AMOUNT`, `NEARBY_TOWN`, `COMPANION`, …) any reskin must preserve. → **Replace** with a
> blighted Westfold/Gondor steading whose seed-corn was lost to a hard winter or a Dunlending raid (the "shadow withers
> the land" motif).

## Custom-Issue Replacement Framework

New module `Main/Features/LotrIssues/` on TAOM's layer stack (thin behavior → service → adapter; ADR-002/007; IoC
`Reuse.Singleton`). The design is a **hybrid**: a small set of generic parameterized mechanic classes + a validating
config provider for the LOTR content. This avoids 43 near-duplicate C# classes while keeping per-mechanic quest logic
pure and testable.

### Mechanic classes (C#)

≈4–6 generic `IssueBase` / `QuestBase` subclasses, one per gameplay shape, reusing CareerQuest's count-event vs
threshold-poll objective pattern:

| Mechanic | Shape | Covers archetypes |
|----------|-------|-------------------|
| `DeliverGoodsLotrIssue` | accumulate N of an item, deliver | grain/herd/tools/crafting-materials/army-supplies |
| `HuntBanditsLotrIssue` | defeat a spawned warband / clear a base | bandit-base / poachers / deserters / outlaws |
| `EscortLotrIssue` | protect a moving party to a destination | caravan escort / ambush |
| `DefeatRivalLotrIssue` | capture/defeat a named target | rival captured / spy |
| `GatherThresholdLotrIssue` | reach a skill/renown/troop threshold | recruits / training / tutor |
| `EconomicLotrIssue` | price/market intervention | artisan pricing / smugglers / revenue farming |

Each mechanic class owns its quest logic (objective tracking, completion, reward dispatch); the **content** (which LOTR
flavor, text keys, target cultures, frequency, rewards, troop tiers) lives in config.

### Config provider (XML)

`Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml` → `LotrIssueConfigProvider`, mirroring the CareerQuest
precedent (`taom_career_quests.xml` → `CareerQuestConfigProvider`). **Config-validation is mandatory** per
[`.claude/rules/csharp-architecture.md`](../../.claude/rules/csharp-architecture.md): range-check, NaN/Infinity reject
via `FiniteFloatValidator`, resolve referenced item/troop/culture ids at load, skip-and-warn on invalid entries. One
test per validation rule.

### Registration

One thin `LotrIssuesCampaignBehavior : CampaignBehaviorBase`, added in `SubModule.OnGameStart`
([SubModule.cs:294-307](../../Main/SubModule.cs#L294), single-owner — recommend the edit, don't make it from a
subagent). It subscribes a single `OnCheckForIssueEvent` listener and delegates to `ILotrIssueService`, which selects
eligible LOTR issues for the hero (occupation + culture + settlement) and calls `AddPotentialIssueData`. One
data-driven dispatcher replaces the 43 vanilla behaviors.

### SaveableTypeDefiner

ONE `LotrIssueSaveableTypeDefiner`, **new base id `726900801`**, `localId` starting at `101` (+1 per saveable class).
Avoid the in-use bases — `726900501` (EquipPresets), `726900601` (FormationPreset), `726900701` (CareerQuest, derives
global id `726900802`); verify the chosen derived ids are clear. Copy CareerQuest's id-math comment. Register the inner
`QuestBase` subclass(es) + any saveable progress container; an `IssueBase` subclass with `[SaveableField]` state needs
registration even without a quest. **Issue-attached quests do NOT set `SpecialQuestType`** (only issue-less quests do —
see the lifecycle rule in the engine doc).

## Vanilla-Issue Suppression

**Recommended: `campaignStarter.RemoveBehaviors<T>()`** (public, `CampaignGameStarter.cs:43`) called in
`SubModule.OnGameStart` for each of the 43 vanilla issue behaviors. They are added by Sandbox (the 36 via
`SandBoxManager.Initialize`, the 7 via `SandBoxSubModule.InitializeGameStarter`) before a later-loading module's
`OnGameStart` runs; removing them means they never subscribe `OnCheckForIssue`, so only LOTR issues spawn. **Keep
`IssuesCampaignBehavior`** (the host spawner). Guard each call so a renamed/removed type after an engine bump is a no-op,
not a crash. **Confirm at implementation** that TAOM's `OnGameStart` runs after Sandbox's registration (very likely per
the [submodule lifecycle](../reference/engine/submodule-lifecycle-and-harmony.md): `InitializeGameStarter` fires for all
modules before any `OnGameStart`, and TAOM loads after Sandbox).

**Rejected alternatives:**

| Option | Why rejected |
|--------|--------------|
| `CanHaveCampaignIssuesEvent` veto | Over-reach — the same gate governs notable disappearance/retirement (`NotablesCampaignBehavior.cs:280`); suppressing issues would freeze notable despawn. |
| `IssueModel` frequency override | No such knob — `IssueModel` has no per-issue-type frequency; frequency is in `IssuesCampaignBehavior.GetFrequencyScore`. |
| Harmony patch on selection | Invasive + engine-bump-fragile + redundant. Keep only as a last-resort fallback. |

**Save-compat — ship as a new-campaign feature.** A save made *before* suppression may carry an in-progress vanilla
issue/issue-quest; removing the behavior doesn't delete the serialized object, but its event hooks are gone → soft-lock
risk. Lowest-risk path: LOTR issues apply to new campaigns; existing saves keep their in-flight vanilla issues until
they resolve/expire. A guarded `OnGameLoaded` cancel-sweep over surviving vanilla issues is a Phase-2 option (needs the
`IssueBase` cancel API + the OnGameLoaded entity-state-matrix discipline).

## Text / Localization

Vanilla issue text is `new TextObject("{=KEY}default")` (e.g. `{=OJObD61e}The headman of {ISSUE_SETTLEMENT} needs grain
seeds…`). Two relevant facts:

- **For any `Reskin` row**, override the displayed text by shipping the same `{=KEY}` in a later-loading GameText XML
  (TAOM already does this for faction strings via `module_strings.xslt`). **Every `{VARIABLE}` token must be preserved
  verbatim** — they're filled at runtime by C# `SetTextVariable`; dropping one (e.g. `HeadmanNeedsGrain`'s 17 tokens)
  silently blanks the substitution. The matrix's text-variables column records them per row. Note the **English GameText
  short-circuit** caveat (see [localization-override.md](localization-override.md) / Patch25): English overrides of
  *vanilla* keys go through the runtime override feature, not a plain GameText file.
- **Suppression makes vanilla overrides moot for `Replace`/`Drop` rows** — removed behaviors never render their strings,
  so there's nothing to translate. Only `Reskin` rows need vanilla-key overrides.

**New custom-issue strings** (`{=taom_lotr_issue_*}`) flow through TAOM's 12-language pipeline: add them to a new
`taom_lotr_issue_strings.xml`, register it as a `GameText` node in `SubModule.xml`, add a `LanguageFile` ref in all 12
`language_data.xml`, bump `LanguageDataXmlTests.HaveExactlyXLanguageFiles`, then run `tools/translate_with_claude.py`
(`/localize xml`).

## Verification (for the eventual implementation)

- **Static/config:** `python tools/validate_moduledata.py` (add a `taom_lotr_issues` schema under `tools/schemas/`);
  `taom-moduledata` MCP (`item_exists` / `troop_exists` / `culture_exists`) for every id the config references.
- **Build/test:** service + provider 100% (ADR-008: one test per validation rule, per objective-type progress branch,
  per reward type) + a **suppression-list test** asserting the removal list equals the authoritative 43-issue set.
  `/verify` for the full gate.
- **Bindings:** `/verify-bindings` after an engine bump to confirm `IssueManager.AddPotentialIssueData`,
  `CampaignGameStarter.RemoveBehaviors`, the `IssueModel` virtuals, and every vanilla issue type name still resolve.
- **In-game smoke (only confirmable live):** new sandbox campaign — (a) a LOTR issue spawns with culture-correct
  troops; (b) its text + every `{VAR}` renders (English + one other language); (c) NO vanilla Calradic issue appears
  over several weeks of cheat-time-skip; (d) accept → progress → complete an issue-quest, save/load mid-quest, confirm
  survival.
- **Pre-merge:** `/deep-review` then `/review-codex`, orchestrated by `/ship`.

## Implementation (as built)

The shipped feature **collapsed the planned 8 mechanic templates to 3**, all validated by deep-review + the Wave-0
Codex pass. Every one of the 43 issues maps onto one of these via XML config — no bespoke per-issue classes.

| Template (`IssueBase` + paired `QuestBase`) | Mechanic | Issues |
|---|---|---|
| **DeliverGoods** | accumulate N of an `item:<id>` trade good, hand in via dialog | 14 (grain/supplies/draught/crafting/tools/horses/herd/artisan×2/offload/revenue/art-of-trade/tutor/special-weapons) |
| **DeliverPersonnel** | hand over N bandit prisoners from the player's `PrisonRoster` | 2 (gang recruits, mine laborers) |
| **Combat** (`variant=`) | event-driven count, auto-completes on N (no turn-in) | 27 — `DefeatRaids` (24, won battles), `CaptureLords` (1, at-war lord taken prisoner), `WinTournaments` (2, tournament won) |

**Why 3, not 8:** the "Escort-a-moving-party", "EconomicGather", "ConquestMilitary", and "SocialMisc/CraftItem"
mechanics from the matrix below were each reframed onto the proven Deliver/Combat mechanics rather than authored as
bespoke blind-built templates (e.g. caravan-ambush/escort → "defeat the raiders on the road"; revenue-farming →
"collect the tithe-in-kind"; lady's-knight/betting-fraud → `WinTournaments`; rescue-the-daughter → "defeat the gang
that holds her"). This is the matrix's documented **deliberately-simplified** trade-off taken to its conclusion: it
guarantees a green, non-crashing v1 where every mechanic is already engine-validated, and defers richer bespoke
mechanics (accompany a live party, price intervention, siege objectives, social minigames) to a future iteration.

**Architecture** (ADR-002 thin entry points / ADR-007 adapters):
`taom_lotr_issues.xml` → `LotrIssueConfigProvider` (validates, skips-invalid-and-warns, `FiniteFloatValidator`) →
`ILotrIssueService` (pure: eligibility, count/reward math, reward application) → `LotrIssuesCampaignBehavior` (one
`OnCheckForIssueEvent` listener; the `LotrIssueDefinition` rides into the constructed issue via
`PotentialIssueData.RelatedObject`) → the 3 templates → paired quests. Sealed types stay behind
`ILotrIssueGiverAdapter` / `ILotrIssueRewardAdapter`. Vanilla's 43 issue behaviors are removed in
`SubModule.OnGameStart` via `LotrIssueSuppression.SuppressAll` (`RemoveBehaviors<T>`, each guarded), keeping the host
`IssuesCampaignBehavior` so `OnCheckForIssueEvent` still fires. Saves register at base `726900801`, localIds 101–106
(3 issue/quest pairs).

**Localization:** 308 keys in `taom_lotr_issue_strings.xml` (English source-of-truth; defaults also embed inline in
the config so text renders pre-translation), registered as a GameText node + an 8th `<LanguageFile>` in all 12
`language_data.xml` with per-language stubs. AI translation propagation via `tools/translate_with_claude.py` is the
one remaining standard pipeline step (deferred — English fallback is live).

**Per-type behavior (one mechanism fixed, one is an accepted v1 trade-off — this doc's Risk #5).** All configs of a
template share one runtime type: 27 Combat → `typeof(CombatLotrIssue)`, 14 DeliverGoods → `typeof(DeliverGoodsLotrIssue)`,
2 DeliverPersonnel → `typeof(DeliverPersonnelLotrIssue)`. Two engine mechanisms key on the issue **type**:

- **Accept gate — FIXED (Codex review, 2026-06-20).** `IssueBase.CheckPreconditions` blocks accepting a second active
  quest of the same `GetType()` unless `IssueQuestCanBeDuplicated` is overridden — by default
  (`IssueQuestCanBeDuplicated => false`) the player could hold at most ONE active quest per template across all its
  configs. All three templates now override `protected override bool IssueQuestCanBeDuplicated => true`, so configs of a
  template run concurrently.
- **Spawn throttle — accepted.** The over-representation score + per-settlement zero-out + cooldown in
  `IssuesCampaignBehavior` still key on type, so the world hosts fewer simultaneous LOTR issues than vanilla's 43
  distinct types would, and rare Combat variants surface infrequently. A true per-config type bucket is impossible
  under the generic-template design without code generation; the deferred mitigation is to split the high-volume
  templates into a few `def.Id`-keyed subclasses if in-game observation shows the rate is too low.

## Appendix — Future LOTR Main Quest (out of this effort)

A "War of the Ring" *story arc* as a custom `SpecialQuest` line — NOT a revived StoryMode storyline (TAOM has no
StoryMode dependency). Author phased `QuestBase` subclasses with `SpecialQuestType = "taom_wotr_main"` so
`QuestManager.OnGameLoaded` never auto-cancels them (the issue-less-quest lever CareerQuest uses). A driving
`CampaignBehaviorBase` starts Phase 1 on a trigger and chains phases via `CompleteQuestWithSuccess` → start-next,
mirroring [`CareerQuestCampaignBehavior`](../../Main/Features/CareerSystem/Quests/CareerQuestCampaignBehavior.cs); tie
phase gates to `IWarOfTheRingService` for narrative sync with the existing [diplomacy War of the
Ring](war-of-the-ring.md) feature. Register saveables in the same `726900801` series. Keep it additive/optional
(offered, declinable) so non-questing players are unaffected.

## Top Risks

1. **`CanHaveCampaignIssues` over-reach** — never suppress via that gate; it also drives notable despawn. Use
   `RemoveBehaviors<T>`.
2. **Legacy-save soft-lock** — an in-progress vanilla issue serialized before suppression loses its event hooks. Ship
   as a new-campaign feature, or add a guarded OnGameLoaded cancel sweep (Phase 2).
3. **SaveableTypeDefiner id collision** — derived id = base + localId; bases `…501/601/701` are taken. Use base
   `726900801`, localId ≥ 101, verify derived ids are clear.
4. **`{VAR}` placeholder loss** — a reworded GameText override that drops a runtime token blanks substitution silently.
   Preserve the per-row token set.
5. **`SpecialQuestType` misuse** — issue-attached quests must NOT set it; only issue-less quests need it. Editing an
   in-progress quest's objective list in config between versions soft-locks saved progress (CareerQuest known
   limitation; applies here too).

## Key Files (as built)

| File | Purpose |
|------|---------|
| `Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs` | thin behavior: one `OnCheckForIssueEvent` listener → `AddPotentialIssueData` with def-carrying `RelatedObject` |
| `Main/Features/LotrIssues/ILotrIssueService.cs` / `LotrIssueService.cs` | pure: eligibility, count/reward math, reward application |
| `Main/Features/LotrIssues/Templates/{DeliverGoods,DeliverPersonnel,Combat}LotrIssue.cs` | the 3 generic mechanic `IssueBase` + paired `QuestBase` classes |
| `Main/Features/LotrIssues/LotrIssueConfigProvider.cs` | validating XML loader (skips-invalid-and-warns, `FiniteFloatValidator`) |
| `Main/Features/LotrIssues/LotrIssueSuppression.cs` | `RemoveBehaviors<T>` of all 43 vanilla issue behaviors + suppression-list test |
| `Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs` | save registration (base `726900801`, localIds 101–106) |
| `Main/Features/LotrIssues/Domain/*.cs` | `LotrIssueDefinition` + `LotrIssueTemplate`/`IssueGiverOccupation`/`IssueFrequencyTier` enums |
| `Main/Adapters/{ILotrIssueGiverAdapter,ILotrIssueRewardAdapter}.cs` (+impls) | sealed-type boundary (ADR-007) |
| `Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml` | the 43 issue configs (flavor, cultures, rewards, counts) |
| `Main/_Module/ModuleData/taom_lotr_issue_strings.xml` | 308 localization keys (English source-of-truth) |
| `TAOM.Tests/Features/LotrIssues/*` | config-provider + service + suppression tests (50) |

## GitHub Issue

- **Issue:** [#291](https://github.com/haterade22/TAOM/issues/291) — feat(lotr-issues): replace all 43 vanilla
  procedural issues with LOTR-authored issues.
- **Status:** Implemented; in-game smoke (issue spawns, text renders, accept→progress→complete, save/load) is the
  remaining user-side validation gate.
