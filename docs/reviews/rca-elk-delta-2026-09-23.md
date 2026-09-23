# RCA: great elk delta, the any-rider creature attacks and the antler charge (deep review, 2026-09-23)

## Top-line

`/deep-review` of everything done after Review 127 (the elk, 2026-09-22), all on Mike's instructions of
2026-09-23 and all uncommitted: the elk built at 2x with `charge_damage` 50 (#636); the antler charge as two typed
blows, 40 blunt and 20 pierce; and #643, the four elephant-like trees (elephant, mumakil, war ram, elk) attacking
under a player rider as the warg does. Seven lenses in two waves (Standards, Engine, Data flow, XML; then
Efficiency, Completeness, Design), all on the deep-reviewer definition.

**No CRITICAL or HIGH, and no engine incompatibility.** Engine verified 24 claims against the installed v1.5.3
DLLs and left 4 native links unverified. XML ran 7 gates with 0 failures.

Findings: **3 MEDIUM, 10 LOW**, all fixed or documented the same day. Four findings changed behaviour, so they went
to Mike in one question batch before anything was applied; his answers reshaped the change (below). Follow-ups:
the live-Armory test fixture (not filed, reason below) and the mount-parity audit's reskin gap (folded into #642).

## Mike's four decisions (asked once, applied with tests)

| # | Finding | Mike's answer | Applied as |
|---|---|---|---|
| D1 | Data flow MED + Design: the elephant-like blow was the CREATURE's. A hit's affector is read from `Blow.OwnerId` (v1.5.3 `Agent.cs:5465`), the kill's through native (`Agent.Die` to `IMBAgent.Die`, `:4694`, back into `Mission.OnAgentRemoved`, `Mission.cs:3006`; how native picks it is unread), and a mount agent has no Character (`Mission.cs:4611` builds it with null), so `BattleAgentLogic.OnAgentRemoved`, which reads `affectorAgent.Character` with no rider swap, credited the kill to no one. The warg and spider credit their rider | "The rider, like the warg" | The shared hit passes the rider, falling back to the creature only once the rider has gone (`WargAttackService`'s rule), for all four creatures |
| D2 | Design P1 + Efficiency: with armour ignored and the blunt part lethal, 40 blunt + 20 pierce lands exactly like one 60 blow, with two log lines and two hit reactions | "One 60 blunt blow" | `ElkConfig.AttackDamage` 60 / `AttackDamageType` Blunt; the profile carries the damage type; `ElkAntlerChargeTask`, `ComputeAntlerCharge` and `AntlerChargeDamage` deleted |
| D3 | XML MED: previews (inventory, party screen, map icon) scale a mount by the item's `scale_factor` (`CharacterTableau.cs:1104`, `MobilePartyVisual.cs:1135`), not `body_length`, so the 2x elk previews at 1x | "Battle only, document it" | `elk.md` Size section and a Known gap |
| D4 | Data flow LOW: the career's charge bonuses (the `MountChargeDamage` passive, Antler Crash's charge buff) reached the elk's body charge through `MountChargeDamage`, never the antler blow | "Scale the antler blows too" | New `ICareerAgentStatService.MountChargeMultiplier`, the product `ApplyMountStatModifiers` now also uses, read when the charge fires through the profile's `RiderMultiplier`; `ComputeInflictedDamage` gates it (a NaN, non-positive or past-10x value counts as 1). Elk only |

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | Every synthetic creature blow (warg, spider, elephant, mumakil, ram) wrote "Blunt" in the combat log. `CombatLogData`'s constructor sets `DamageType = Blunt` (`CombatLogData.cs:385`), `GetLogString` reads that field (`:202`), and vanilla overwrites it only inside `MissionCombatMechanicsHelper.GetAttackCollisionResults` (`:200`), which a hand-built blow never runs | A hand-built engine struct skips the vanilla code that fills it | A 2026-05-27 fix set the COLLISION data's type and a comment claimed the log reads it. Nobody opened `GetLogString` to see which field it reads, so the comment turned a guess into documentation for four months | `TakeDamage` sets `combatLogData.DamageType`; the comment now carries the evidence chain. Lesson "A struct you build for an engine call skips the vanilla code that fills it" (adapters-taleworlds-api) |
| F2 | MED | `elephant.md`'s tree diagram and Key files row still showed the removed `IsAiControlledDecorator` level and the player-ridden sleep | **Repeat offender, third time in five days**: the docs sweep did not grep the removed symbol | The #643 doc pass added one subsection per creature doc and searched for nothing else. The lesson exists (ram RCA F1, 2026-09-18; elk RCA F8, 2026-09-22); it was not consulted | Fixed. The lesson gains a third recurrence note and a mechanical backstop recommendation (below) |
| F3 | MED | The Phase 7 template in `creature-mount-authoring.md` still prescribed the AI-only gate, and `/new-creature-mount` makes that doc the first read, so the next creature would have shipped with it | Same as F2 | Same as F2 | Same as F2 |
| F4 | LOW | Four statements said a mount agent's Character is its rider: the Phase 7 template, `ElkMissionBehavior`, `WarRamMissionBehavior`, `MumakilMissionBehavior`. The engine builds mount agents with a null character (`Mission.cs:4611`) | A later-established engine fact never flows back to older statements | #610 recorded "a mount agent's own Character is null" in `TaomAgentStatCalculateModel` on 2026-09-17; the older template line and its three clones were never swept for the contradicted claim | All four corrected. Recurrence note on the lesson "Cloning a sibling clones its unverified claims" (data-content-cultures) |
| F5 | LOW | `elk.md`'s smoke step said a kill proves `CanKillEvenIfBlunt`, with no game mode named. Custom Battle registers `DefaultAgentDecideKilledOrUnconsciousModel` (`CustomGame.cs:102`), which kills every downed agent, so the step could not fail there | A smoke step that cannot fail | Written from the campaign model's behaviour without checking which kill model the mode the tester would pick registers | The step names a campaign battle, the MCM page and the literal `[BlowDiag]` format. Lesson "A smoke step that proves an engine decision names the game mode that makes it" (testing-qa) |
| F6 | LOW | The CHANGELOG's elk test counts (10, 26) matched no run | Counts written from memory | Written while the tests were being edited, before a run (`evidence-over-claims.md` §C trap 3) | Copied from runner output (11, 30). The rule exists; no new lesson |
| F7 | LOW | `TakeDamage`'s new `damageType` default was unpinned, and every caller but the elk relies on it | A shared default without a test | The new parameters were tested through their pure helper only | `TakeDamage_DamageTypeDefault_IsPierce` (reflection) |
| F8 | LOW | `ComposeWeaponFlags` was inserted between `ComposeBlowFlags`'s summary and its method, so the blow-flag summary documented the wrong member | Insertion above another member's doc comment | A slip in placement | Moved below `ComposeBlowFlags` |
| F9 | LOW | The `damageType` doc said the type changes the hit sound; for a weaponless record the sound lookup never reads it | Stated without reading `GetHitSound` | Same as F1: the log path was assumed, not traced | The doc names the combat log and the killed-or-wounded rule |
| F10 | LOW | `body_length` is also an auto-resolve power stat: a mount's `Effectiveness` folds in `body_length x weight x 0.025` (`ItemObject.cs:945`), which `CharacterObject.GetSimulationAttackPower` adds at 2.5x. The 2x body and charge 50 took the elk from 221.2 to 268.3 | A visual attribute with a second consumer | The size change was reviewed as a render and reach question | One paragraph in `elk.md`; no data change |
| F11 | LOW | #636's issue body still describes the pre-2x design | An issue body is a document too | Deltas went to comments on 2026-09-22; the 2026-09-23 ones had not been posted | A comment on #636 with the current design |
| F12 | LOW | Doc drift from the same pass: the feature-map row, `elk.md`'s issue section without #643, "fires" for "fire" in `elephant.md` and `mumakil.md`, the memory file's test count, no 2026-09-23 verification row in the ledger | Same as F2 | Same as F2 | All fixed; the ledger row records the gates re-run for this review |
| F13 | LOW | `tools/audit_mount_parity.py` covers neither horse-skeleton reskin (`MOUNTS` lists spider, warg, elephant, mumakil). The ram RCA of 2026-09-18 left "a ram section" as a follow-up that was never filed | A deferred follow-up with no owner | Recorded in an RCA line only; no issue, so nobody picked it up | Folded into #642 (collapse the ram and elk), where the two reskins' shared shape is designed |

**Convergence pass (one deep-reviewer on the applied changes): 0 CRITICAL, 0 HIGH, 0 MED, 3 LOW, all fixed.**

| # | Sev | Bug | Fix |
|---|---|---|---|
| C1 | LOW | The docs cited `Agent.cs:5522` for "the killer is read from `Blow.OwnerId`". That line is the victim's last-blow record, which `Die` uses only to re-own an ownerless killing blow (and which already swaps in a mount's rider, `:5516`). The kill goes `Agent.Die` to native `IMBAgent.Die` (`:4694`) and back into `Mission.OnAgentRemoved` (`Mission.cs:3006-3012`); how native picks that affector is unread | `elk.md`, `elephant.md`, this RCA and the CHANGELOG now cite `:5465` for the hit and name the kill path as native and unread |
| C2 | LOW | A consequence of D1 no lens named: with the rider as owner, a player's creature blows pass `CareerPerkMissionBehavior.OnScoreHit`'s MainAgent gate, and `AbilityDamageAttributionReporter` printed "+N from ability" for a blow that never ran the damage model the buff feeds (the warg's bite already did) | First cut: `OnScoreHit` skipped weaponless hits (`attackerWeapon == null`). Codex (O1 below) showed that also hid true lines for punches and kicks, so it now reads `CustomAttacksUtils.IsRegisteringSyntheticBlow`, set while TAOM registers its own blow; pinned by `SyntheticBlowScopeTests` |
| C3 | LOW | Pre-existing in a touched method: a non-finite product passed `ApplyMountStatModifiers`'s new `charge != 1f` gate and wrote into `MountChargeDamage` (HEAD did the same through its per-factor gates). The lens said a NaN magnitude could arrive because `float.Parse` accepts "NaN"; Codex showed the career loader already rejects NaN and infinity (`CareerConfigProvider.ParseFloat`), so only an overflowing product reaches it, and I had relayed the premise unchecked | `MountChargeMultiplier` returns 1 for a non-finite product, covering both consumers; two tests, RED first; comments now state the overflow case |

**Unverified, owed in game:** that native passes the killing blow's weapon flags to `Mission.GetAgentState` (a
campaign battle proves it); that native resolves `OnAgentRemoved`'s killer from `Blow.OwnerId` (the rider's kill
count and merit after a charge prove it); `HandleBlowAux` with a Blunt, KnockDown blow; the head-drop clip's priority under a
player-steered mount; the rider's scale on a 2x horse skeleton (observed unscaled on the 3x mumakil).

**My own miss in the question batch:** the D1 question told Mike creature-owned kills "give no XP". The data-flow
lens had said hit XP still reached the rider, and the engine confirms it: `BattleAgentLogic.OnScoreHit` swaps a
mount's rider in (`:116-118`). What was lost was the kill (`OnAgentRemoved` has no swap). The decision stands on
that, but the question overstated the cost, because I relayed half a lens claim without checking it
(`evidence-over-claims.md` §A.4). Recorded here and told to Mike.

## Codex adversarial review (`/review-codex`, gpt-6-astra at ultra, 2026-09-23)

Dispatched after the deep review converged, on Mike's request ("a deep review and then a codex review"). Prompt
`docs/reviews/codex-adversarial-elk-delta-2026-09-23.prompt.md`, output `docs/reviews/raw/codex-adversarial-elk-delta-2026-09-23.md`
(200,341 tokens). **0 P0, 0 P1, 0 P2; 2 P3 findings and 3 P3 observations, all confirmed, no false positive.** It
answered all seven Known Suspects with lines decompiled from the installed DLLs, refuted KS4 (ally buffs are evicted
in `OnAgentDeleted` and delayed restores check slot identity) and widened KS1 (the owner change also changes the hit
SOUND, and a human affector brings the attacking party's Doctor's Oath into the survival roll).

| # | Codex | Mine | Agree? | Verified against | Outcome |
|---|---|---|---|---|---|
| F1 | P3 | LOW | Yes | `CombatLogData` ctor (`:367`, `:386`) and `GetLogString` (`:185-189`): the 15th argument is `crushedThrough`, which prints "Crushed through!" | `TakeDamage` passed `knockDown` there since the helper's port: every creature knockdown the player saw printed it. The call now names all seventeen arguments, `crushedThrough: false` |
| F2 | P3 | LOW | Yes | `DefaultPartyHealingModel.GetSurvivalChance` returns 1 only for Blunt WITHOUT the flag; with it, the normal survival roll runs (`Mission.GetAgentState` rolls it) | `elk.md` smoke step 6 asked for "killed, not wounded", which a correct build can fail. Now: several isolated finishing charges, at least one kill; a single wound is inconclusive. The "It stays lethal" bullet says the roll still decides |
| O1 | P3 | LOW | Yes | `SandboxAgentApplyDamageModel` applies `DamageMultiplierBonus` outside the weapon branch (`:753`), so a punch or kick with an ability active IS amplified | The convergence fix's null-weapon gate hid those true lines. Replaced by an explicit marker, `CustomAttacksUtils.IsRegisteringSyntheticBlow`, set around `RegisterBlow` (the engine raises the callbacks synchronously inside it); 6 tests, RED first |
| O2 | P3 | Design | Yes | `BlowWeaponRecord.GetHitSound` (`:107-109`): a weaponless blow plays the charge-damage sound for a non-humanoid owner, a kick or punch sound for a humanoid one | A consequence of D1: every creature attack would sound like a punch (the warg's always has). Mike kept the creature's sound: `TakeDamage(chargeImpactSound: true)` marks the blow `NoSound` and replays the engine's block (`Agent.cs:5466-5484`) with the charge event; `CreatureImpactSoundTests`, RED first |
| O3 | P3 | LOW | Yes | The mount applies any finite product; the antler accepts only (0, 10] | "Cannot drift" overstated; the interface doc, service comment, test comment and `elk.md` now say the two agree for any product the antler accepts |

**Things Codex missed:** none found. **Unverified, unchanged:** native forwarding of the flag and of the death
affector; Blunt plus KnockDown in native; rider death and deletion timing (`IsEnemyOf` on the captured rider is a
native call); the 2x seating and scale.

**Fresh review of the post-Codex fixes (one deep-reviewer, 2026-09-23): 0 CRITICAL, 0 HIGH, 0 MED, 4 LOW, all fixed.**
It mapped all 17 `CombatLogData` and all 37 `GetAttackCollisionDataForDebugPurpose` arguments to their signatures (no
other misbinding), confirmed the marker's scope is synchronous on one thread with no re-entry, and confirmed the
replayed sound matches the engine's block for this blow exactly. Its LOWs: argument 14 compared the victim with itself
(`victim.RiderAgent == victim`, always false) where vanilla compares the victim's rider with the attacker
(`Mission.cs:6529`), unreachable today and fixed; neither half of the sound fix was pinned where it matters, so
`ChargeImpactFlags` is now a pure helper `TakeDamage` must call and an IL check proves the shared hit passes `true`;
a parenthetical in `elk.md`'s smoke step had drifted onto the sound sentence; and "now named arguments" was true of
three of seventeen until the call named them all.

### Root cause (Phase 3e)

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| F1 | `knockDown` passed into `crushedThrough` | Logic error: a positional argument bound to the wrong parameter | A 17-argument constructor of almost all bools, called positionally when the helper was ported from LOTRAOM; no lens mapped the arguments to the signature, and the deep review's F1 on the same struct looked only at `DamageType` | Named arguments at the call; lesson "Call a long all-bool engine signature with named arguments" (adapters-taleworlds-api) |
| F2 | A smoke step demanding one outcome of a random roll | Other: a probabilistic engine decision treated as deterministic | The flag was read as "lethal" rather than "no longer always non-lethal"; nobody read past the early return in `GetSurvivalChance` | Recurrence of today's testing-qa lesson, which now also says to state how many trials settle a rolled decision |
| O1 | A heuristic gate hiding true lines | Logic error: provenance inferred from a field other cases share | The convergence fix chose the cheapest discriminator and accepted "bare hands lose the line" without asking whether the ability reaches bare hands | Explicit marker plus tests; lesson "Mark TAOM's own blows; never infer them from a field the engine's blows share" (adapters-taleworlds-api) |
| O3 | Overstated parity claim | Other: a guarantee wider than the guards | Written when both consumers were fresh and believed identical | Wording fixed; no lesson |
| C3 premise | "`float.Parse` accepts NaN", relayed from the convergence lens | Relaying a subagent's claim unchecked (`evidence-over-claims.md` §A.4), the second time today after the XP claim in the D1 question | Neither claim was traced to its source before I repeated it | RCA note; the rule exists and was not followed |

## Improvements (Step 4)

- **Applied:** Design P2, the kill flag folded into the damage type (`ComposeWeaponFlags(DamageTypes)`, so
  `TakeDamage` lost its `canKillEvenIfBlunt` flag and a Blunt synthetic blow is always lethal); Design P4, the
  `damageType` doc line (F9). Design P1 (the typed split in the shared service) was superseded by D2: one blow needs
  only a type on the profile. Efficiency F1 (the elk's service resolved twice) and F2 (a second blow cancelling the
  first's knockdown) went with the deleted task.
- **Follow-up, not filed:** Design P3, one `LiveArmoryFixture` for the four places that each re-derive the game
  folder (`HowdahHarnessItemTests`, `HowdahPrefabTests`, `GameAssemblies`, `ElkConfigTests`). It edits three
  pre-existing test classes, so it is outside this change; offered to Mike.

## Root-cause patterns

**Documentation written in the new vocabulary cannot find the old (F2, F3, F12).** Third time in five days, each
time on creature work and each time caught by the Completeness lens, never by the builder. A lesson that is right
and not consulted needs a mechanical step: a check that lists every identifier the diff deletes from `*.cs` and no
longer exists in code, and greps `docs/` and `.claude/` for it. Recommended as tooling; not built here, because it
is its own change.

**A fact established later does not flow back (F1, F4, F9).** The truth about a mount's Character was already in
the codebase, and the combat log's field was one decompile away; in both cases an earlier statement said otherwise
and nobody swept for it. When a session establishes an engine fact, grep the claim it contradicts, not just the
code it changes.

## Why each agent missed these

Nothing was missed by a lens: every finding came from one, the three MEDIUMs from Engine (F1) and Completeness (F2,
F3), and the two MEDIUM decisions from Data flow (D1) and XML (D3). The lenses that did not report a given finding
were outside its domain.
