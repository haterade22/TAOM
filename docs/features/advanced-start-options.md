# Advanced Start Options (ASO) adaptation

**Status:** shipped at the v1.5.0 engine bump (2026-08-19), in-game verification owed.
**Code:** [`Main/Features/AdvancedStartOptions/TaomStartOptionsProvider.cs`](../../Main/Features/AdvancedStartOptions/TaomStartOptionsProvider.cs)
**Related:** [`v1.5.0-impact.md`](../migration/v1.5.0-impact.md) · [`rca-v150-engine-bump-2026-08-19.md`](../reviews/rca-v150-engine-bump-2026-08-19.md)

## What ASO is

Bannerlord v1.5.0 added a pre-campaign options screen: a **campaign scenario** (Default, United
Empire, Invasion, Last Stand, 2 Faction War), a **player start** (Default, King, Vassal, Mercenary,
Trader, Outlaw, Beggar), stacking **modifiers** (Bandit Surge, Civil Unrest, Recruit Shortage, Swift
Travel), and a shareable **seed**. It runs from the main menu before the campaign exists, so it does
not interact with TAOM's character-creation stage sequence at all (verified: `InitializeCharacterCreationStages`
is byte-identical between v1.4.8 and v1.5.0).

## Why TAOM needs to adapt it

**ASO's faction pickers are hardcoded, not data-driven.** `SandBoxStartOptionsProvider.GetCultureItems()`
returns a literal list of the eight vanilla StringIds. TAOM keeps those eight ids and renames the
kingdoms in place via `spkingdoms.xslt`, then adds fourteen more in `taom_spkingdoms.xml`. Left
alone, the menu therefore offered "Western Empire" and dropped the player into Gondor, and TAOM's
fourteen LOTR kingdoms were invisible.

| ASO showed | Actually is |
|---|---|
| Northern Empire (`empire`) | Dunland |
| Western Empire (`empire_w`) | Gondor |
| Southern Empire (`empire_s`) | Mordor |
| Sturgia / Aserai / Vlandia / Battania / Khuzait | Dale / Harad / Rohan / Khand / Rhun |

## What TAOM does

`TaomStartOptionsProvider` uses the engine's own extension point rather than a Harmony patch.
`AdvancedStartOptionsManager.Initialize()` reflects over every active game assembly with
`BindingFlags.Static | Public | NonPublic` and binds any method carrying `[StartOptionsProvider]`
whose signature is `void (AdvancedStartOptions)`.

### 1. The United Empire scenario is removed

Two independent reasons, either sufficient:

- It builds a kingdom from a hardcoded `TextObject` literally named **"Calradian Empire"** with
  StringId `calradian_empire`, merging whichever factions hold the three imperial ids. In TAOM that
  is Gondor, Mordor and Dunland in one realm.
- **Two of its three unifier choices are broken.** Both `HandleKingdomCleanup` and `ResolveKingdom`
  branch on `Culture.StringId == "empire"`. In vanilla all three imperial kingdoms satisfy that; in
  TAOM only Dunland does, because `spkingdoms.xslt` gives `empire_w` the `gondor` culture and
  `empire_s` the `mordor` culture. Picking Gondor or Mordor as unifier therefore skips both the
  deactivation and the redirect, leaving the player ruling an empty, fief-less shell beside the real
  merged kingdom.

### 2. TAOM's fourteen kingdoms are added to every faction picker

`KingdomId`, `LastStandKingdomId`, `InvasionScenarioFactionId`, `TwoFactionWarFaction1Id`,
`TwoFactionWarFaction2Id`.

Small kingdoms are added too. `GiveStartingFiefs` degrades rather than throwing: it falls through to
`FindFallbackStartingTown`, which guards every step with `if (list.Count > 0)`. **Lindon** and
**Goblin-town** own one town and no castle, so a Vassal start there takes the fallback instead of
receiving a castle. That is a gameplay quirk, not a crash.

### 3. The menu is re-localised

49 overrides in `taom_module_strings.xml`. Two id families are needed and both matter:

- `str_campaign_starting_options_item_name.<id>` is the list entry.
- `str_advanced_start_value_name.<id>` is the interpolation used inside scenario descriptions such as
  `{InvasionScenarioFactionId}`.

Scenario text that names Calradia directly is retitled to Middle-earth.

## Gotchas

**The item-condition delegate answers "is this DISABLED", not "is it enabled".** Vanilla's
always-available helper is named `GetNeverDisabledItem` and returns **`false`**. Returning `true`
from a condition greys the item out. This is the single easiest thing to invert here.

**`RemoveItem` exists but is easy to miss.** `ListAdvancedStartOption` exposes `GetItems()` returning
an `IReadOnlyList`, which reads as immutable, but `AddItem` and `RemoveItem` both mutate the private
backing list. `AddItem` overwrites an existing entry with the same identifier rather than duplicating.

**Adding a picker item without a string leaves a raw StringId on screen.** ASO looks each item up by
id through `GetListItemName`, so a new kingdom needs both string rows or the menu shows `erebor`.

## Interactions with the rest of TAOM

- **Player start applies at character-creation phase 8.** TAOM's phase-9 handlers
  (`SpecialResourcesBehavior`, `PlayerPossessionBehavior`, `StartupResourcesBehavior`) deliberately run
  after it. Verified: across CampaignSystem, SandBox and StoryMode the only indices any subscriber
  uses are 1 and 8, so 9 is both reachable and last.
- **King / Vassal / Trader / Beggar starts overwrite equipment and gold.** TAOM's career starting
  equipment survives only on Default, Mercenary and Outlaw. The startup-gold re-apply is explicitly
  gated on the default start so ASO's own values are not clobbered.
- **Civil Unrest** works by swinging vanilla's loyalty thresholds, which `TaomSettlementLoyaltyModel`
  overrides. See [`v1.5.0-impact.md`](../migration/v1.5.0-impact.md) for the high-rebellion pair that
  keeps the modifier meaningful.
- **Recruit Shortage** and **Swift Travel** both land, because `TaomVolunteerModel` and
  `TaomPartySpeedModel` call `base.` first.

## Not supported

**War Sails / NavalDLC.** Its start-options provider is what registers the Nord Invasion scenario and
the Fleet Admiral / Merchant Venturer starts, and `OnNordInvasionScenarioSelected` dereferences a
`"nord"` kingdom TAOM does not have. `Main/_Module/SubModule.xml` declares an `<IncompatibleModules>`
block, so the pairing is refused rather than left to chance.

## Owed

- In-game: open the ASO screen and confirm the menu reads Dunland / Gondor / Mordor, the fourteen
  TAOM kingdoms appear in the pickers, and United Empire is absent.
- A translator run for the 49 new string rows (they exist in all 12 languages as English fallback).
- Decide whether Lindon and Goblin-town should be excluded from the Vassal start rather than falling
  back, once the fallback has been seen in play.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
