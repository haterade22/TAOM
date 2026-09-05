# Uncapturable Heroes

## Overview

Sauron and the nine Nazgûl can never be taken prisoner. When a battle they lose would normally put
them in someone's dungeon, they slip away as fugitives instead: they still lose the field, still
lose their army, and still take days to rebuild, but nobody ever ransoms the Witch-king. A second
seam covers the non-battle case, a hero captured because the fief he was resting in changed hands.

**This does not make them immortal.** A hero who dies in the battle still dies. That is a deliberate
scope decision, not an oversight, and it is restated at the bottom of this doc.

## Why This Exists

- **Vanilla behavior:** after a battle, `MapEvent.CaptureDefeatedPartyMembers` takes any defeated
  lord prisoner subject to a per-winner chance roll. Nothing in the engine exempts anybody:
  `Hero.CanBecomePrisoner()` returns `true` unconditionally for every AI hero.
- **TAOM requirement:** the mod's top-tier antagonists are not people you put in a cell and trade
  back for denars.
- **Without this feature:** Sauron sits in a Gondorian dungeon with a ransom price, which is both an
  immersion break and a soft win condition the campaign was never balanced for.

## Architecture

### Design challenge

Two problems, one easy and one that looks easy and is not.

**The identity problem.** There is no attribute that picks these ten heroes out. All of them are
`occupation="Lord"`, and eight of the ten are `culture="Culture.mordor"`, exactly like every
ordinary Mordor lord. Race gets you one of them:

| Hero | Race attribute | Where defined |
|---|---|---|
| `lord_1_17` Sauron | `race="sauron"`, the only one in the mod | `lords.xslt:1060` |
| `lord_1_15` (Witch-King), `lord_1_155`, `lord_1_16`, `lord_1_28`, `lord_1_38`, `lord_1_48` (Khamûl) | **none**, so vanilla race 0 (human) | `lords.xslt` templates at `:888`, `:942`, `:996`, `:1594`, `:2086`, `:3550` |
| `lord_1_48_1`, `lord_1_48_2`, `lord_1_48_3` | `race="uruk"` | `characters/lords.xml:24974`, `:25025`, `:25076` |

So a race list frees six of the Nine, and adding `uruk` to catch the other three would protect every
uruk lord in the game. **The compiled `nazgul_nine` hero set is the only axis that covers all nine**,
and a future refactor that "simplifies" the config to a race list would silently free six wraiths
with no error and a config that still parses. `ShippedUncapturableHeroesConfigTests` fails if that
set is ever dropped.

Note `lords.xslt` emits attributes as `<xsl:attribute name="race">`, never as a literal `race="..."`,
so a naive grep for `race=` in that file reports zero. Two comments elsewhere in the repo state this
distribution incorrectly; see "Known doc drift" below.

**The seam problem.** The obvious hook, `CampaignEvents.CanHeroBecomePrisonerEvent`, is a dead end.
`Hero.cs:2010-2012` reads:

```csharp
if (this != MainHero)
{
    return true;                 // dispatcher never reached
}
bool result = true;
CampaignEventDispatcher.Instance.CanHeroBecomePrisoner(this, ref result);
```

The event fires only for the player. Subscribing to it does exactly nothing for an AI lord. The
method itself has to be patched.

### Solution approach

**The escape needs no implementation.** `MapEvent.cs:1983` gates capture on `CanBecomePrisoner()`,
and when that gate fails the hero is still in the defeated member roster, so the fall-through at
`MapEvent.cs:2004-2008` fires `MakeHeroFugitiveAction.Apply(hero)`. Suppressing the capture *is*
granting the escape, with correct party teardown, settlement exit and event dispatch, all written by
the engine. We write the veto only.

That premise is the single point of failure for the whole feature, so it is pinned by an IL test
(`UncapturableHeroesBindingTests.MapEvent_StillFallsThroughToTheFugitiveAction`) rather than by
hope. If TaleWorlds ever restructures that method, the veto would still apply and the escape would
stop happening, leaving a defeated hero neither captured nor free, with no exception and no log line.

**Be honest about how far that test goes.** It asserts the three engine calls still exist in that
method and that the fugitive call still comes after the gate in IL order. It does NOT verify the
control-flow relationship the feature actually rests on: that the gate's false branch leaves the
roster entry intact and reaches the fall-through. A refactor that kept all three calls but removed
the hero from the roster on the false branch, or moved the fugitive call inside the capture branch,
would still pass. Verifying that needs real IL branch analysis or a live-campaign integration test,
and neither exists yet. Read `MapEvent.cs` by hand on any engine bump; a green test means the premise
has not obviously been deleted, not that it still holds.

```
uncapturable_heroes_config.json
            |
   UncapturableHeroesConfigProvider  ──  UncapturableHeroesSettingsProvider (MCM over JSON)
            |                                        |
   UncapturableRegistry (identity)  ────────  UncapturableHeroService (policy)
            |                                    /            \
   INazgulRegistry, IRaceManager      IHeroCaptivityAdapter   IInquiryAdapter
                                              |
                            Hero_CanBecomePrisoner_Patch (battle)
                            TakePrisonerAction_Apply_Patch (everything else)
```

### The two seams

**Seam 1, postfix on `Hero.CanBecomePrisoner()`.** Guards in order: never flip `false` to `true`;
defer for `MainHero`; defer when the service is null; **defer when `DeathMark != None`**; then set
`__result = false` and announce.

That fourth guard is worth its line. `MapEvent.cs:1977` already excluded `DiedInBattle` and
`DiedInLabor`, but a hero carrying some other mark (`Murdered`, say) passes that gate and then fails
the `DeathMark == None` condition on the fugitive fall-through. Denying his capture would leave him
stranded in a defeated roster, neither captured nor escaped, and would make the escape message a lie.

**Seam 2, prefix on `TakePrisonerAction.Apply(PartyBase, Hero)`.** Covers the routes that never
consult the gate. In practice that is `PrisonerCaptureCampaignBehavior.cs:67`: a hero standing in a
settlement that changes hands, or whose host faction declares war.

It targets the **public** `Apply`, not the private `ApplyInternal`. The only caller `Apply` misses is
`ApplyByTakenFromPartyScreen`, and that is already unreachable for a hero, because
`PlayerEncounter.DoCaptureHeroes` (`PlayerEncounter.cs:1611`) does
`RosterToReceiveLootPrisoners.RemoveIf(e => e.Character.IsHero)` before the loot screen ever sees it.
A public target is also a binding `HarmonyPatchBindingTests` can hold across an engine bump.

Two of its guards carry real weight. The `IsPrisoner` one: `MakeHeroFugitiveAction` touches the
party, the settlement and the hero state but **never removes anyone from a captor's `PrisonRoster`**,
so vetoing a re-capture of an already-held hero would leave him `Fugitive` and still listed as
somebody's prisoner.

The `DeathMark != None` one **mirrors the postfix's guard 4 and has to stay in step with it.** A kill
applied while the hero is inside a map event does not kill: it stages a mark and returns
(`KillCharacterAction.cs:46-49`). `MapEvent.cs:1977` admits every mark except `DiedInBattle` and
`DiedInLabor`, so a `Murdered` hero reaches the capture gate, the postfix deliberately defers to
vanilla there, vanilla answers `true`, and `MapEvent.cs:1993` calls straight into this prefix.
Without the guard the two seams would contradict each other on the same hero in the same battle: one
decides to leave him to vanilla, the other frees him a moment later. Found by the Codex pass as its
only P1.

Ordering inside the service matters too. On the direct-capture path the hero is **already a fugitive**
by the time the announcement runs, so nothing after the mutation may throw: an escaping exception
unwinds into the prefix's catch, which returns `true`, and vanilla then captures a hero the world was
just told escaped. The whole announce body including the config read sits inside a guard for that
reason (the config provider is a `Lazy<T>`, and a faulted `Lazy<T>` rethrows forever).

**Both hooks fail open, and that direction is not arbitrary.**
`PatchShield.ShieldFinalizerWithResult` swallows `Missing*`/`TypeLoad` exceptions, after which the
patched method returns `default(bool)`. For the postfix that is `false`, which would make *every hero
in the game* uncapturable. Each body carries its own try/catch that leaves the vanilla answer alone.

## Configuration

`Main/_Module/ModuleData/uncapturable_heroes/uncapturable_heroes_config.json`

```json
{
  "enabled": true,
  "heroSets": ["nazgul_nine"],
  "heroIds": ["lord_1_17"],
  "uncapturableRaces": ["sauron"],
  "excludeHeroIds": [],
  "announceEscape": true
}
```

The shipped file carries a `_comment_*` note on every key; the table below is the contract.

| Key | Meaning |
|---|---|
| `enabled` | Master switch, overridden at runtime by the MCM toggle |
| `heroSets` | Named lore groups. Only `nazgul_nine` is known, resolving to `NazgulRegistry`. Unknown names are skipped with a warning |
| `heroIds` | Individual StringIds. Sauron is listed here *and* under the race rule, so the feature survives a data change that drops his race attribute |
| `uncapturableRaces` | **The rule.** Any hero of a named FaceGen race, without being listed by id. Matches exactly one hero on shipped data |
| `excludeHeroIds` | **Evaluated first**, beats the rule and both include lists. The escape hatch for handing one hero back to vanilla capture |
| `announceEscape` | Whether to write the campaign message-feed line |

Resolution order is fixed: exclude, then `heroIds`, then `heroSets`, then the race rule. First match
wins.

**Reload scope: a full Bannerlord restart.** The provider is `Reuse.Singleton` wrapping a `Lazy<T>`,
so the file is read once per process. A new campaign or a save reload will not pick up an edit.

**MCM:** `World/Uncapturable Heroes` → "Sauron and the Nazgûl Cannot Be Captured", `GroupOrder = 48`.

## Behaviour matrix

| Route | Outcome |
|---|---|
| Battle capture gate, `DeathMark == None` | Denied; vanilla's fall-through makes him a fugitive. Announced if it was the player's own battle |
| Same, hero is the party leader | `RemovePartyLeader()` already ran at `MapEvent.cs:1979-1981`, so `MakeHeroFugitiveAction` takes the `MemberRoster.RemoveTroop` branch, not `DestroyPartyAction`. The leaderless party is torn down by `MapEventSide`, exactly as vanilla does when every capture chance fails |
| `DeathMark == DiedInBattle`/`DiedInLabor`, or `Occupation.Special` | `MapEvent.cs:1977` skips the whole block; the seam never runs. **He can still die** |
| `DeathMark` is anything else (`Murdered`, `Executed`) | **Both** seams defer and vanilla captures. The postfix's guard 4 and the prefix's matching guard have to agree here: the postfix leaving him to vanilla is only coherent if the prefix does not then free him at `MapEvent.cs:1993`. No stranded roster entry, no message |
| Battle ended by retreat | `MapEvent.cs:1957` early-returns; nothing runs |
| Party annihilated (`MapEventSide` raw `ChangeState(Fugitive)`) | Cannot fire for him: already a fugitive, no longer `LeaderHero`. Known blind spot; that path also does not raise `CharacterBecameFugitiveEvent` |
| Settlement changes hands, or war declared, with him inside | Seam 2 vetoes and frees him. The reverse-indexed loop in `HandleSettlementHeroes` makes the `LeaveSettlementAction` removal safe. **The main real non-battle trigger** |
| Post-battle loot screen | Structurally unreachable; `PlayerEncounter.cs:1611` already stripped every hero from the loot prisoner roster before the screen sees it |
| Talk-to-defeated-lord dialog, "You are my prisoner now." (`LordConversationsCampaignBehavior.cs:3072-3076` and `:3145-3149`) | **Reachable, and neither site is `IsPrisoner`-gated.** Seam 2 vetoes and he escapes, so the player picks the line and the hero leaves anyway. Both sites pass `Campaign.Current.MainParty.Party` as the captor, so the escape message always fires immediately afterwards and supplies the resolution. Left as is deliberately: assert-then-escape reads as intended for these ten, and gating the option would mean patching vanilla conversation conditions for a line that resolves itself |
| Failed release persuasion | `IsPrisoner`-guarded, so Seam 2 defers. No `PrisonRoster` corruption |
| `LordWantsRivalCapturedIssueBehavior` | Unreachable; already suppressed at `LotrIssueSuppression.cs:65` |
| Player-side routes (`PartyBase.cs:746`, `EncounterGameMenuBehavior`, `MapEvent.cs:2041`, `PrisonBreakCampaignBehavior`) | `MainHero` guards on both seams; untouched |
| Already a prisoner when the feature ships | Seam 2 defers; he stays held until ransomed or released normally. Documented gap, by decision |

### What happens after the escape

`MakeHeroFugitiveAction` leaves the hero with `PartyBelongedTo == null` and
`CurrentSettlement == null`. `HeroSpawnCampaignBehavior.OnHeroDailyTick` then gives roughly a
30%/day chance to teleport him into a suitable settlement (fugitives get +100 weight toward their
`HomeSettlement`) and flip him back to `Active`, after which `ConsiderSpawningLordParties` raises a
fresh party. He returns on his own in days, with no help from this feature.

## Localization

Two keys in `taom_module_strings.xml`. Two rather than one because the battle escape and the
settlement escape are different sentences a translator must be able to reorder independently.

- `taom_uncapturable_escapes_battle`: `{HERO} cannot be held. He slips away from the field before he can be taken.`
- `taom_uncapturable_escapes_capture`: `{HERO} cannot be held. He is gone before your men reach him.`

Delivered through `IInquiryAdapter.ShowMessage` into the campaign message feed. No `LogEntry`, so no
new save surface.

The message is player-filtered: Seam 1 uses `MapEvent.IsPlayerMapEvent` (not the static
`PlayerMapEvent`, which only means "the player is in some battle" and false-positives on concurrent
AI fights), and Seam 2 compares the captor's `MapFaction` to the player's. Escapes elsewhere in the
world are silent.

Wording note: vanilla also falls through to fugitive when the winner has no roster to receive
prisoners (`MapEvent.cs:1986-1990`), so the line says "he escaped", which is always true, rather than
"TAOM prevented a capture", which would not be.

## Co-op

Disposition **ReviewedSafe**, recorded in `CoopVetoClassificationTests`. The prefix does skip a
replicated campaign-state mutation, so what carries the condition matters: it is lore-fixed identity
from the compiled `NazgulRegistry` roster plus shipped hero and race data, so every peer computes the
same answer from the same files. Deferring to the host was rejected because the battle seam is a
postfix on a query and cannot be host-gated without making Sauron capturable in a fief but not in a
field battle.

## Tests

| File | Covers |
|---|---|
| `UncapturableRegistryTests` | Every resolution row, both wraith race shapes, exclude beating the rule, unknown set/race names skipped and warned, the unknown-race-id fallback trap, table built once |
| `UncapturableHeroServiceTests` | Toggle off never asks the registry; the escape happens before the announce gate; a failed escape returns false so vanilla capture proceeds; a throwing toast does not undo a completed escape |
| `UncapturableHeroesConfigProviderTests` | Missing file, malformed JSON, null lists reverted, empty lists passed through, and the `ObjectCreationHandling.Replace` append-merge regression |
| `ShippedUncapturableHeroesConfigTests` | The shipped file parses clean and still names the Nazgûl set; every wraith id still exists in the data; **Sauron still carries `race="sauron"`** and **still ships `occupation="Lord"`** (Occupation.Special would silently unhook Seam 1) |
| `UncapturableHeroesBindingTests` | Every engine member resolves, plus the IL premise test on `MapEvent.CaptureDefeatedPartyMembers` |
| `UncapturableHeroesWiringTests` | IoC registration order (must follow Enlistment, which owns the single `IInquiryAdapter` registration), patch statics, category application, both `ResetForUnload` calls, the MCM property |

## Deliberately out of scope

**Death is not blocked.** The Witch-king can be killed in a battle he could not be captured in.
"Uncapturable" is not "undying", and players will read it as such, so it is stated in the MCM hint
text as well as here. If a death block is ever added for the same cast, the un-defeatable question
has to be reopened first: at that point there would be no way to take these ten off the board at all.

**Heroes already in a dungeon** when the feature ships are not reconciled. The feature never
proactively frees anyone; auto-releasing on load would silently take a captive the player earned.
They stay subject to vanilla's own ransom, release and escape logic, which does include a generic
AI-prisoner escape: `PrisonerReleaseCampaignBehavior` listens on `DailyTickHeroEvent`, starts from a
4% daily chance and calls `EndCaptivityAction.ApplyByEscape`. So a Nazgûl imprisoned in an older save
will get out on his own eventually, just not because of this feature.

## Known doc drift (not fixed here)

`Main/_Module/ModuleData/dread_aura/dread_aura_config.json` (`_comment_heroSets`) and
`Main/Features/DreadAura/DreadRegistry.cs` (the axis-1 comment) both assert that "eight of the Nine
carry no race attribute and Khamûl is `race="orc"`". Verified false on 2026-08-26: **six** carry no
race attribute and three are `race="uruk"`; Khamûl (`lord_1_48`) carries **no** race attribute at all,
is `culture="Culture.dolguldur"`, and his "orc" is a `skill_template`
(`SkillSet.taom_north_orc_warrior_skills`).

DreadAura's behaviour is unaffected, since it finds the Nine through `heroSets` exactly as this
feature does; only the stated reasoning is wrong. It needs its own issue and commit, because anyone
re-deriving "which heroes can the race axis reach" from those comments gets the wrong answer.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/player-switcher.md](./player-switcher.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/modding/lords-and-heroes.md](../modding/lords-and-heroes.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
