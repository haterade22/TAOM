# RCA: refuge reduction for crews and mounts (#741), creature bandits as enemies only (#742), deep review 2026-10-06

## Top-line

Two changes shared one review. **#741** makes the refuge defender reduction (#507) reach the howdah and mumak crews,
by reading the victim's party through its origin's `BattleCombatant`. On the maintainer's word it also reaches mount
hits, now credited to the rider's party. **#742** stops `taom.spawn_troops <creature> <n> ally` from putting a creature
bandit on the player's side: `CreatureBanditRules.SpawnsOnPlayerSide` owns the rule, and the console asks it before it
picks a team.

`/deep-review` ran in two waves:
- **Wave 1 (#741 only):** Standards, Engine compatibility, Data flow, Efficiency.
- **Wave 2 (both changes):** Completeness, Design, Data flow on the new paths, and Standards with Engine compatibility.

Results:
- 0 engine incompatibilities on v1.5.4 and 0 HIGH findings.
- 1 MEDIUM, older than the change (F1); the maintainer decided it and it is applied.
- 10 LOW defects and gaps, all fixed.
- 4 design proposals: 3 preserving, 1 changing console text only. All applied.
- Logging: both data-flow passes found none warranted. The reduction runs on every hit, and the existing Blow Diagnostics switch logs each blow's damage for the in-game check.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | A mount hit never got the refuge reduction: the hook read `VictimAgentOrigin`, which is null for a mount, while the career passives and vanilla credit a mount hit to the rider. Pre-existing since #507. | Mount-blind origin consumer (repeat) | #507 read the struct's origin field without the mount branch the 2026-06-26 lesson requires. The #627 and #737 reviews looked at crews and at the refactor, not at mounts. | The hook now takes `in AttackInformation` and picks the rider's origin for a mount (maintainer's choice over keeping mounts out). The 2026-06-26 lesson in `adapters-taleworlds-api.md` was widened to every origin consumer. |
| F2 | LOW | The corrected knockback claim still left out vanilla's body-part and `CanKnockDown` conditions; Sauron's mace carries `CanKnockDown`. | Engine claim, incomplete | The correction was written from `CanWeaponKnockback`'s branch on blow flags, without decoding its outer condition. | Restated in all four places from the decompile (`:929-942` and the helper's `:63-66`). |
| F3 | LOW | `combat-mechanics.md` credited kicks and bashes to `CanWeaponKnockback`; the helper grants them before it asks. | Engine claim, misattributed | Two methods were folded into one sentence. | Corrected. |
| F4 | LOW | The hook's origin parameter and the service's party id said "never null"; the engine passes a null origin on a hit with no victim agent. | Nullable contract | Annotations were copied from the old signature. | `IAgentOriginBase?` and `string?`, and the tests' `null!` workarounds dropped. |
| F5 | LOW | The new comment and `refuge.md` named the crews only; a `SimpleAgentOrigin` hero and BannerlordCoop's `CoopAgentOrigin` resolve a party too. | Doc scope narrower than the code | The comment described the cases that motivated the change, not every implementer. | Both named; all implementers listed from a metadata scan. Whether co-op peers share the refuge book is unverified. |
| F6 | LOW | Test and RCA text overclaimed: base calls "not pinned" while two are; MobileParty "get-only" while it has a private setter; the #737 RCA listed two of four pins. | Comment accuracy | Written ahead of the final shape. | Corrected; the test sets the property through its setter instead of a compiler-generated field name. |
| F7 | LOW | The mumak in-game check did not say the refuge's own party, so a mumak in the player's party would read as a failure. | Owed check ambiguity | The check named the beast, not the party the book keys on. | Reworded in `mumakil.md`, `elephant.md` and the new `refuge.md` Owed line. |
| F8 | LOW | #742's wiring pin checked the rule call came before the team lookup, not that its answer was used: deleting the assignment left every test green. | Pin on a call, not its effect | The pin looked for the call text. | The answer is assigned in the same statement as the call, and the pin targets that statement. |
| F9 | LOW | The console's note was dropped exactly when the side flip makes the spawn fail (a town or village has no enemy team), and on success it claimed "spawned" next to the count. | Report path lost on failure | The formatter's failure branch returned before the note. | The note follows the failure reason too, and states the rule ("`ally` is ignored"); one formatter test. |
| F10 | LOW | `refuge.md`'s inserted paragraph had no full stop and split "both apply" from the two consumers it named; its list of scripted blows was short. | Doc edit | Inserted mid-block. | Moved below the paragraph, completed, the list extended (trample, war ram, elk, Animalia). |
| F11 | LOW | One rule test duplicated a DataRow; one pin comment named a risk (the agent origin) the model no longer carries. | Test tidiness | Written before the origin moved into the hook. | Deleted; the clause now points at the hook's own tests. |

## Root-cause patterns

**A consumer resolved an agent's party by its own rule.** F1 and the crew gap behind #741 share this cause. Each
consumer did its own mapping from an origin to a party: one switched on two origin types, and none took the mount
branch. The engine already says how: an origin's `BattleCombatant`, and the rider's origin for a mount. That is how
the parent model and vanilla do it. The producer side was already written down: the crew origins forward
`BattleCombatant` (2026-09-19 lesson). The consumer side was not, and the gap stayed open for 17 days after it was
recorded.

**An invariant lived in a fall-through prefix.** #742's "never on the player's side" was enforced only by
`Patch93_CreatureBanditSpawn` declining the player's side, and for a declined troop it falls through to the vanilla
spawn. The one caller that names the side itself, the console, got a husk on the player's team.

## Why each lens missed or caught them

- **Data flow** caught F1 in wave 1, by tracing every victim kind against the parent model and vanilla. In wave 2 it confirmed the mount branch and caught F9.
- **Engine compatibility** caught F2, F5 and the provenance of every origin implementer, by scanning installed metadata.
- **Standards** caught F3, F4, F6 and F7.
- **Completeness and Design** independently caught F8 to F11.
- **Efficiency** found nothing in the change. It raised the follow-up below.

## Follow-ups (not in this change)

- **MCM read per refuge hit:** `RefugeSettingsProvider` re-reads `TaomSettings.Instance` on every refuge hit, and `HotPathSettingsProvidersTests` does not list it. The fix is to apply the cached pattern and add its rows. Resolved 2026-10-06 in #745.
- **A second origin switch:** `BannerColorPersistence`'s `Mission_SpawnAgent_Patch` switches on origin types too. The #746 review kept it on purpose: the crews' spawners already copy the parent's colours, and a `BattleCombatant` read would recolour every BannerlordCoop battle troop (`CoopAgentOrigin`, puppets included) away from its faction colours. A comment there now says so.
- **MCM reads on hot paths:** `CreatureBanditTuning` and `SignatureStrikesSettingsProvider` per hit, `SupplyLinesSettingsProvider` per campaign frame, `CampSettingsProvider` per campaign frame while a camp stands. Resolved 2026-10-06 in #746.
- **Co-op:** whether every co-op peer that computes a blow holds the same refuge book is unverified.

## Feedback to codify

Lessons were appended to `adapters-taleworlds-api.md` (the mount lesson widened, plus the consumer rule),
`harmony-il.md` (an invariant belongs in the pure rule, not a fall-through prefix) and `testing-qa.md` (pin the
effect, not the call). No rule file changes: each pattern is a one-line rule its category file carries.
