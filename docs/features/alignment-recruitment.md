# Alignment-Gated Recruitment

## Overview

A recruiter (player or AI lord) cannot recruit volunteer troops from a settlement controlled by an enemy-aligned kingdom. A Free-aligned lord (Gondor, Rohan, the Elves, Dwarves, Dale) is barred from recruiting in an Evil-controlled settlement (Mordor, Isengard, Dol Guldur, Gundabad, Dunland, Rhûn, the orc/goblin factions), and — by default — the reverse. Neutral factions (Umbar, Shaghana, Abanissa, Khand) recruit and are recruited from freely.

## Why This Exists

LOTR's factions have a hard moral axis the base game's recruitment doesn't model: the men of Gondor will not march under the banner of Mordor, and Sauron's orcs will not serve a free lord. Vanilla lets any lord recruit any settlement's volunteers (gated only by relation, gold, and tier). This feature ties recruitment to the existing Free/Evil/Neutral alignment so a player's (and the AI's) army composition respects who they serve.

## Architecture

### Design challenge
The engine's recruiter-aware gate is **per-notable**, not per-troop. The only method that sees both the recruiter and the recruitment source is `VolunteerModel.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero, useValueAsRelation)`, which returns a recruitable-slot *index cap* (it never sees the individual volunteer `CharacterObject`). Both the player recruit UI (`RecruitVolunteerTroopVM.CanBeRecruited` via `HeroHelper.HeroCanRecruitFromHero`) and AI lords (`RecruitmentCampaignBehavior.RecruitVolunteersFromNotable`) clamp to this cap.

### Solution
Override `MaximumIndexHeroCanRecruitFromHero` in TAOM's existing `TaomVolunteerModel`. Returning **`-1`** is the engine's own "recruit nothing from this notable" signal (it already returns -1 for negative relation / being at war), so a single override blocks both the player UI and AI in one place — no Harmony patch, no `OnTroopRecruited` undo.

Alignment is keyed by **kingdom StringId**, reusing the existing `IAlignmentService.GetKingdomSide` (the same lookup the Execution and Diplomacy features use), backed by `execution/alignment.json`. Both sides are resolved to a kingdom StringId:
- **Recruiter** ← `buyerHero.Clan.Kingdom.StringId` (the kingdom the recruiter serves).
- **Source** ← `sellerHero.CurrentSettlement.MapFaction.StringId` (the kingdom controlling the recruitment settlement).

Keying on kingdom (not culture) is required because TAOM maps several LOTR factions onto vanilla culture slots that share a culture but differ in alignment — Gondor (`empire_w`, free) and Mordor (`empire_s`, evil) are the canonical example. This is the same `MapFaction.StringId`-not-`Culture.StringId` disambiguation `TaomTargetScoreModel` documents.

```
TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero   ← boundary: extract kingdom StringIds, -1 or base
        │ delegates the decision to
IRecruitmentAlignmentService.IsRecruitmentBlocked        ← pure: alignment predicate (no TaleWorlds types)
        │ uses
IAlignmentService.GetKingdomSide  +  IRecruitmentAlignmentSettingsProvider (MCM over JSON)
```

### Block predicate
`recruiterSide`, `sourceSide` ∈ {Free, Evil, Neutral} from `GetKingdomSide`.
- **Symmetric** (default): block ⇔ both sides non-Neutral AND different.
- **GoodRejectsEvil**: block ⇔ `recruiterSide == Free && sourceSide == Evil` (Evil recruiters unrestricted).
- Neutral on either side never blocks; disabled never blocks; if "Apply To AI Lords" is off, AI recruiters never block; if "Apply To Player" is off, the player never blocks. The player and AI gates are independent — you can keep AI gated while exempting yourself, or the reverse.

Note: the service deliberately does **not** call `IAlignmentService.AreEnemyAlignments`, whose Neutral semantics are inverted for this purpose (it treats Neutral as an enemy of everyone).

## Configuration

| Source | Field | Default | Meaning |
|--------|-------|---------|---------|
| `recruitment_alignment/recruitment_alignment_config.json` | `enabled` | `true` | Master toggle. |
| | `mode` | `"Symmetric"` | `"Symmetric"` or `"GoodRejectsEvil"`. Unknown value → reverts to Symmetric with a warning. |
| | `applyToAi` | `true` | When false, AI lords recruit unrestricted (the player is still gated if `applyToPlayer`). |
| | `applyToPlayer` | `true` | When false, the player recruits unrestricted (AI lords are still gated if `applyToAi`). |
| MCM "World/Recruitment Alignment" | Enable Recruitment Alignment Block / Only Good Rejects Evil / Apply To Player / Apply To AI Lords | as above | MCM overrides JSON at runtime (`Reuse.Singleton` — JSON edits need a process restart; MCM is live). The player and AI toggles are independent; the master toggle off disables the feature for everyone. |

Alignment data itself lives in `execution/alignment.json` (shared with Execution + Diplomacy) — 22 kingdom StringIds, no changes needed for this feature.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/AlignmentRecruitment/IRecruitmentAlignmentService.cs` / `RecruitmentAlignmentService.cs` | Pure block predicate. |
| `Main/Features/AlignmentRecruitment/RecruitmentAlignmentConfig.cs` | JSON DTO + derived `GoodRejectsEvilOnly`. |
| `Main/Features/AlignmentRecruitment/RecruitmentAlignmentConfigProvider.cs` | Loads + validates the JSON (mode whitelist, fallback-to-default). |
| `Main/Features/AlignmentRecruitment/RecruitmentAlignmentSettingsProvider.cs` | Merges MCM over JSON defaults. |
| `Main/Features/AlignmentRecruitment/RecruitmentAlignmentIoC.cs` | `Reuse.Singleton` registrations (wired from `Main/IoC.cs`). |
| `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs` | The `MaximumIndexHeroCanRecruitFromHero` override (added to the existing model). |
| `Main/SubModule.cs` | Threads `IRecruitmentAlignmentService` into the `TaomVolunteerModel` construction. |
| `Main/Features/TaomSettings.cs` | The 3 MCM knobs (group "World/Recruitment Alignment", GroupOrder 36). |
| `Main/_Module/ModuleData/recruitment_alignment/recruitment_alignment_config.json` | Default config. |

## Dependencies

- `IAlignmentService` (Execution feature) + `execution/alignment.json`.
- The vanilla `VolunteerModel` recruitment chokepoint (player UI + AI both honor `MaximumIndexHeroCanRecruitFromHero`).

## Tests

`TAOM.Tests/Features/AlignmentRecruitment/`:
- `RecruitmentAlignmentServiceTests` — one case per (recruiterSide × sourceSide) cell under both modes, plus master-toggle, the player-vs-AI × applyToAi branches, and null-id-resolves-Neutral. (20 cases)
- `RecruitmentAlignmentConfigProviderTests` — valid/case-insensitive/unknown `mode`, missing file, malformed JSON, empty object, caching. (8 cases)

The `TaomVolunteerModel` override is a thin boundary (GameModel) and is validated in-game, not unit-tested.

## How-To

**MCM is authoritative in-game; JSON is the compiled default.** `RecruitmentAlignmentSettingsProvider` reads `TaomSettings.Instance?.X ?? jsonDefault`, and `TaomSettings.Instance` is non-null whenever MCM is loaded (i.e. always, in a normal game). So the MCM toggles win at runtime; the JSON file only supplies the value used in unit tests and during the early-startup window before MCM initializes. This matches every other TAOM settings provider (e.g. `CastleRecruitmentSettingsProvider`). **To change behavior in a running game, use the MCM panel** — editing the JSON alone will not take effect because the hardcoded MCM defaults shadow it.

**Restrict to "good rejects evil" only** — toggle "Only Good Rejects Evil" in MCM (the JSON `"mode": "GoodRejectsEvil"` sets the compiled default for tests / pre-MCM startup). Evil lords may then recruit anywhere.

**Disable for yourself only** — toggle off "Apply To Player" in MCM (JSON `"applyToPlayer": false`). You recruit anyone; AI lords stay gated. To disable the whole feature for everyone instead, toggle off the master "Enable Recruitment Alignment Block".

**Disable for AI** — toggle off "Apply To AI Lords" in MCM (JSON `"applyToAi": false` is the compiled default).

**Add/retune a faction's alignment** — edit `execution/alignment.json` (shared with Execution/Diplomacy); keys are kingdom StringIds (`empire_w`, `empire_s`, `vlandia`, `erebor`, …). (This file is read directly by `AlignmentService`, not shadowed by MCM.)

## Notes / Edge Cases

- An evil-conquered-but-not-yet-culture-converted settlement counts as the conqueror's alignment immediately (its `MapFaction` is already the new owner), even if it still offers the old culture's troops for a while.
- An independent / clanless early-game player has no kingdom → resolves Neutral → never blocked until they join or found a kingdom.
- `isPlayer` is `buyerHero == Hero.MainHero` (matching vanilla's own MainHero special-casing in this model). A player-clan party led by a **companion** (not the main hero) is therefore treated as an AI recruiter and follows the "Apply To AI Lords" toggle — intentional, since the engine routes such parties through the AI recruitment path with `mobileParty.LeaderHero`.
- Recruiter alignment uses `buyerHero.Clan.Kingdom.StringId`. This is equivalent to the engine's own `Hero.MapFaction` (= `Clan.Kingdom ?? Clan`) for alignment purposes: a hero serving a kingdom (vassal or mercenary — mercenary service sets `Clan.Kingdom`) resolves to that kingdom; a clanless/independent hero resolves to Neutral either way.
- Player UX: blocked notables show greyed-out volunteers (the same visual vanilla uses for negative relation). A custom "won't serve you" tooltip is a possible follow-up (would need a `RecruitVolunteerTroopVM` UI postfix).
- Garrison auto-recruit and AI map-recruit are inherently same-kingdom and never trigger the gate.

## Changelog

- 2026-06-17 — Initial feature: alignment-gated recruitment via a single `TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero` `-1` override (no Harmony); kingdom-StringId alignment through `IAlignmentService` / `execution/alignment.json`; Symmetric/GoodRejectsEvil modes + independent player/AI MCM toggles ("World/Recruitment Alignment", GroupOrder 36) + JSON config; 34 unit tests. Issue #286.
