# RCA: Player Switcher (#514) deep review, 2026-08-27

Feature: Player Switcher, branch `feat/player-switcher`, commits `a9f3b0b5..ac6593cf`.
Review: five parallel Claude agents (standards, 1.4.8 API compatibility, cross-system data flow,
efficiency and lifecycle, completeness) plus an independent Codex adversarial pass.

## Top line

Three of the five agents returned clean: standards 8/8 with no violations, API compatibility 22/22
verified against the installed 1.4.8 DLLs with zero incompatibilities, and completeness with every
service tested and every registry row accurate. The two that found things found **four** genuine
defects, none blocking, all fixed in this session.

The interesting result is not the count. It is that **the two most dangerous claims in the feature
were both correct**, and both were correct for reasons no one had actually verified until the review
went and looked:

- The DryIoc `RegisterMapping` really does give the session reader and writer the same singleton.
  The standards agent proved it by reading DryIoc's own source rather than by reasoning about the
  API's name: `RegisterMapping` fetches the already-registered `Factory` **object** and re-registers
  that same object under the second interface. Had it instead been a resolve-and-forward, the
  picker would have written to one instance and the handover read another, the feature would have
  been a silent no-op in game, and all 77 unit tests would still have passed.
- The Harmony apply timing is safe, and it is safe for a different reason than the sibling bug #299.
  #299 was a Save/Load hero preview rendering on the *cold main menu*, before any game-init callback.
  Character creation only begins after a `Game` exists, and `Patch9_RaceFilter` already patches the
  sibling `FaceGenVM` in that same flow at this exact timing.

Both were verified rather than assumed. That is the whole value of the gate.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `BodyGeneratorPreviewSink.ApplyPreview` sets `IsPreviewActive(true)` inside its `try`, but the body-properties parse-failure `return` and the outer `catch` both exit without clearing it. A failed preview leaves `Patch9_RaceFilter` suppressed for the rest of that face-generator visit, so the culture race filter silently stops applying to a player building their own face | State/lifecycle | The flag was reasoned about as "set on preview, cleared on restore", and `RestoreDefault` does clear it in a `finally`. The failure paths of the *setter* side were never enumerated. Classic latch asymmetry: the closer was written carefully, the opener was not | Rule already exists and did not fire: `.claude/rules/harmony-patches.md` "Latches & Toggle Gates" says a latch opened in one place and closed in another needs a closer on **every** opener path. Extend its scope note: it currently reads as being about Harmony patch latches, and this latch lives in a Hook class, so it did not pattern-match |
| 2 | MED | Seven of fifteen localization keys are dead: `taom_ps_title`, `_hint`, `_clear`, `_selected`, `_switched`, `_failed`, `_unavailable`. Zero C# references. The player gets **no feedback at all** when a switch is blocked or fails; they simply end up playing their created character with no explanation, while the exact string for that case ships translated in twelve languages | Data flow / spec compliance | The implementation plan specified a completion toast as handover step 10 and the strings file was written from the plan, but the toast was never implemented and nothing checked the strings file back against its consumers. The localization gate only checks the reverse direction (every key has a row in every language), never "every declared key is used" | Add a test asserting every `taom_ps_*` key in the strings XML is referenced from C# or a prefab. The generic version of this is worth having repo-wide, since the existing localization suite is entirely one-directional |
| 3 | P3 | `Patch77_BodyGeneratorView_OnFinalize` is a plain `[HarmonyPostfix]`. If vanilla's own `BodyGeneratorView.OnFinalize()` throws, the postfix never runs, the four static fields are never cleared and `ui_clan` is never unloaded | Harmony / lifecycle | The teardown was designed around "does this method get called", which the efficiency agent confirmed generously by decompiling `GauntletBodyGeneratorScreen.OnDeactivate` (it does, on every navigation direction including quit-to-menu). Nobody asked the second question: "does the postfix run if the original throws" | A postfix that performs cleanup is the wrong shape; `[HarmonyFinalizer]` runs even when the original throws. Worth a lessons entry, because "cleanup in a postfix" reads as correct and is not |
| 4 | P3 | `PlayerSwitcherVM` does not override `OnFinalize()`, so the three `MBBindingList<HeroPickItemVM>` collections never have `OnFinalize()` called on their items, and vanilla `ClanPartyMemberItemVM.OnFinalize()` (which nulls `HeroViewModel._hero`) never runs for picker rows | Lifecycle | The teardown calls `ViewModel?.OnFinalize()` and that looked complete. `TaleWorlds.Library.ViewModel.OnFinalize()` is an empty virtual, so the call reached a no-op stub and did nothing visible | When a VM owns child VMs, the parent's `OnFinalize` must cascade. Bounded here because `ReleaseMovie` already detached the widget tree, but it breaks the pattern vanilla itself follows |

## Root-cause pattern: openers are reviewed less carefully than closers

Findings 1, 3 and 4 are the same shape three times. Each is a resource or flag whose **release**
path was written thoughtfully and whose **failure or cascade** path was not:

1. `IsPreviewActive` has a careful `finally` in `RestoreDefault` and no `finally` in `ApplyPreview`.
3. The teardown handles the case where teardown runs, not the case where it is skipped.
4. The teardown disposes the parent it holds a reference to, not the children the parent holds.

The common error is reasoning about the path you are writing rather than enumerating the paths that
reach the same state. In all three cases the author had the right idea and applied it to one side of
a pair. The generalisable prompt is: **for every acquire, list every exit from the acquiring scope;
for every cleanup, ask what happens when it does not run; for every dispose, ask what the disposed
object itself owns.**

This is a near-relative of the existing "Latches & Toggle Gates" rule, which already encodes
exactly point one for Harmony patches. It did not fire because its stated scope is patches and this
latch is in a Hook. Scope gap, not a missing rule, which is the same failure mode recorded three
times over for the NaN-gate class in `csharp-architecture.md`. The consistent lesson across both:
**when a rule catches a bug in one file category, ask immediately which other categories the same
mechanism appears in, and widen the scope then rather than after the next instance.**

## Why each agent missed what it missed

- **Standards (PASS):** correctly out of scope. None of the four are ADR violations. Notably it did
  the DryIoc and apply-timing verifications properly instead of asserting them, which is the harder
  half of its job.
- **API compatibility (PASS):** correctly out of scope; every signature genuinely checks out. It did
  catch a real nuance nobody had noticed, that `ItemRoster` has no `Add(ItemRoster)` overload and
  the call binds through `IEnumerable<ItemRosterElement>`. Harmless, but it is the kind of detail
  that becomes a bug the day someone assumes symmetry with `TroopRoster.Add(TroopRoster)`.
- **Data flow:** found 1 and 2. This remains the highest-value agent, consistent with the project's
  own record that every HIGH Codex finding has been a data-flow gap.
- **Efficiency:** found 3 and 4, and, more usefully, **refuted** the larger version of finding 3 by
  decompiling the screen teardown rather than accepting the worry as stated. A review that only
  confirms is worth less than one that also narrows.
- **Completeness:** correctly reported complete. Finding 2 is arguably in its territory (a declared
  string with no consumer), but its checklist asks whether localization is *present*, never whether
  it is *reachable*. That is the scope gap the preventive action above closes.

## Lessons to append

To `docs/reviews/lessons/state-lifecycle-save.md`:

### Enumerate every exit from the scope that sets a latch, not just the one that clears it

**Why missed:** `ApplyPreview` set a suppression flag at the top of a `try` and returned early on a
parse failure and swallowed in a broad `catch`, neither of which cleared it. The matching
`RestoreDefault` had a correct `finally`, which made the pair look symmetrical at a glance.
**Prevent:** a flag that gates OTHER code's behaviour is acquired in a `try`/`finally` or cleared on
every early return. Reviewing the closer is not reviewing the latch.
**Source:** `docs/reviews/rca-player-switcher-2026-08-27.md` finding 1. Sibling of
`.claude/rules/harmony-patches.md` "Latches & Toggle Gates", whose scope note said Harmony patches
and so did not fire for a Hook class.

### Cleanup belongs in a Harmony finalizer, not a postfix

**Why missed:** the teardown was validated by confirming the target method is reliably invoked, which
it is. The question never asked was what happens when that method throws: a postfix does not run.
**Prevent:** any Harmony patch whose body releases a resource, clears static state or unloads an
asset uses `[HarmonyFinalizer]`. A postfix is for augmenting a result, not for guaranteeing cleanup.
**Source:** same RCA, finding 3.

To `docs/reviews/lessons/localization-ui.md`:

### Localization gates are one-directional; add the reverse check

**Why missed:** the suite proves every English key has a row in all twelve languages. Nothing proves
a declared key is ever rendered. Seven keys shipped translated and unused, including the three that
were supposed to tell the player their switch had failed.
**Prevent:** assert that every `{=key}` declared in a feature's strings XML is referenced from C# or
a prefab. A dead key is not merely wasted translation: it is usually the fossil of a feature step
that was specified and then never implemented, which is exactly what it was here.
**Source:** same RCA, finding 2.

## The Codex pass: five more, two of them P1

The five Claude agents returned four findings. Codex, given the same ten load-bearing claims and
told to attack them, returned nine and **refuted six of the ten claims**, including two the Claude
agents had marked clean. It was the most productive reviewer here, and the reason is worth
recording: it was the only one that opened the vanilla CONSUMERS of the engine calls rather than the
engine calls themselves.

| # | Sev | Bug | Why every Claude agent missed it |
|---|---|---|---|
| 5 | **P1** | `HeroPickItemVM` derived from vanilla `ClanPartyMemberItemVM`, whose constructor opens `IsLeader = hero == party.LeaderHero;` with no null guard. Wanderers have no party and ship enabled by default, so the first wanderer threw inside the panel build, the attach patch swallowed it, and **the entire picker silently never appeared** | The binding test asserted the base was *unsealed*. Nobody read its constructor BODY. Standards checked layering; API compatibility checked the type and signature exist. Both are true. The defect is one line inside it |
| 6 | **P1** | The handover caught every exception and reported "continuing as created character" even after `ChangePlayerCharacterAction` had already changed `Game.Current.PlayerTroop`. No engine transaction, no rollback | The failure test threw at the ENTRY to `ApplyPlayerCharacter`, before any mutation, so it proved the safe case and looked like it proved the general one. A test validates the scenario it constructs and nothing else |
| 7 | P2 | Vanilla `HeirSelectionCampaignBehavior` listens to the same player-character-changed events and copies the old party items and the old hero equipment into the new party. On adoption `AbsorbOriginalParty` then added the same roster again, **doubling every stack** | The design reasoned about what OUR code does at 1100. Nobody enumerated the other listeners on the events `ChangePlayerCharacterAction` fires. The data-flow agent traces TAOM-internal flow; this is vanilla-internal flow reacting to a TAOM call |
| 8 | P2 | StoryMode seeds the player clan with an adult elder brother, so `KillCharacterAction` promotes him rather than destroying the clan, leaving an orphan clan AND the leftover creation party alive | The design was reasoned against the SandBox startup clan, which is empty. Nobody asked whether that holds in every mode the feature registers for |
| 9 | P2 | The preview mutates the live `BodyGenerator`, and vanilla calls `SaveCurrentCharacter()` from `Done()` and `GoToIndex()`, persisting the previewed body into `CharacterObject.PlayerCharacter`. Previewing a lord then abandoning the selection left the player wearing that lord's face | Same shape as 7: we reasoned about what our preview writes, not what vanilla later saves from the object we wrote into |
| 10 | P2 | `RestoreDefault` cleared the suppression flag AFTER `SetBodyProperties`, so the one refresh that would have rebuilt the culture-filtered race selector was itself suppressed | The Claude data-flow agent found the two `ApplyPreview` leaks and stopped. Codex found the third, which is subtler: not a missing clear, but a correct clear in the wrong ORDER |

Codex also independently **confirmed** the two claims I most wanted verified, agreeing with the
Claude agents: the DryIoc mapping shares one singleton, and the apply timing is not the bug #299
class. Three reviewers agreeing on those by three different methods is the strongest signal here.

### The pattern behind Codex's five: review the CONSUMER, not the call

Findings 7, 8 and 9 are one mistake made three times, and it is a different mistake from the
opener/closer asymmetry above. Each calls a vanilla API correctly, verifies the signature correctly,
and reasons correctly about what that call does. What was never asked is **what else in the engine
reacts to it**:

- `ChangePlayerCharacterAction.Apply` is not just a state change; it dispatches events that
  `HeirSelectionCampaignBehavior` acts on (7).
- `KillCharacterAction` is not just a removal; its behaviour depends on a clan composition that
  differs by game mode (8).
- Writing to the live `BodyGenerator` is not just a preview; vanilla later persists whatever sits
  there (9).

`.claude/rules/csharp-architecture.md` "GameModel Cross-Entity Propagation" already encodes exactly
this instinct, and it did not fire because its stated trigger is a `GameModel` override returning a
per-entity value. This feature has no GameModel. **That is the third scope gap of this review**,
after the latch rule and the localization gate, and it is the same lesson each time.

**Preventive action, generalising past GameModels:** when calling a vanilla ACTION that dispatches
campaign events, find the other listeners before assuming the call is self-contained.
`CampaignEventDispatcher` makes it mechanical: find the event, find its subscribers, read them. For
`ChangePlayerCharacterAction` the answer is `HeirSelectionCampaignBehavior`, and it moves inventory.

## Lessons to append (Codex additions)

To `docs/reviews/lessons/adapters-taleworlds-api.md`:

### A vanilla action dispatches events; enumerate the OTHER listeners before assuming it is self-contained

**Why missed:** the design reasoned carefully about what `ChangePlayerCharacterAction.Apply` does and
never asked what subscribes to the events it fires. `HeirSelectionCampaignBehavior` copies the old
party items and the old hero equipment into the new party, which duplicated every stack on one path
and silently carried startup gear onto the taken-over lord on the other.
**Prevent:** for any vanilla `*Action` call, locate the events it dispatches and read every
subscriber before treating the call as a leaf. This is the non-GameModel form of
`csharp-architecture.md` "GameModel Cross-Entity Propagation", whose trigger was too narrow to fire.
**Source:** `docs/reviews/rca-player-switcher-2026-08-27.md` finding 7.

### Subclassing a vanilla ViewModel inherits its unguarded constructor, not just its bindings

**Why missed:** `ClanPartyMemberItemVM` was chosen precisely so an engine change would break the
build. The binding test asserted it was unsealed. Its constructor dereferences the `MobileParty`
argument on its first line, and the feature's default-enabled wanderers have no party, so the whole
panel silently failed to appear.
**Prevent:** before deriving from an engine VM, read its CONSTRUCTOR BODY for unguarded
dereferences of arguments you may legitimately pass as null. Inheritance bought for compile-time
safety is worth nothing if the base cannot accept your data.
**Source:** same RCA, finding 5.

To `docs/reviews/lessons/state-lifecycle-save.md`:

### There is no rollback past ChangePlayerCharacterAction; report accordingly

**Why missed:** the catch-all reported every failure as "continuing as the created character", and
the test covering it threw before the first mutation, validating the one case where that message is
true.
**Prevent:** when a sequence contains an irreversible step, track whether it ran and report
post-commit failures distinctly. A failure message that misstates the player's identity is worse
than no message. Write the failure test to throw AFTER the irreversible step.
**Source:** same RCA, finding 6.

## Status

Nine of the ten findings are fixed in this session. The tenth, Codex's SUSPECTED P3 on hero states
(prisoners, fugitives, `NotSpawned`), is **deliberately deferred and recorded here**: taking over a
prisoner would begin the campaign in captivity, which is a real consequence, but Codex could not
establish reachability against shipped TAOM startup data and neither could I. It is written into the
feature doc's Owed list and is the first thing the in-game smoke should probe.

Suite 7,680 green. Two regression tests were added beyond the fixes: a container-level `AreSame` on
the session mapping, and a reverse localization check asserting every declared key is rendered.
