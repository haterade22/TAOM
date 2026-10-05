# RCA: tournament fighters wear their own armour, reviewed 2026-10-05

**Scope:** Mike's decision of 2026-10-05: in a tournament every fighter wears his own armour, and only arena practice
fights keep the fighter's own culture's practice kit. `TaomTournamentModel.GetParticipantArmor`, a new
`TournamentService.ArmourDummyId`, their tests and seven docs. Six `/deep-review` lenses (1 to 6; XML and Tooling not
in scope) in two waves, then one convergence pass.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The first version keyed the tournament on its mission (a skipped match never sets `MissionMode.Tournament`), then returned `base.GetParticipantArmor`, which checks that same mode. So every skipped or simulated match (Skip, Skip All, Leave, forfeit, every match after the player is out) still dressed all fighters in the host faction's kit, and the simulation scores that armour. Lenses 1, 2, 4 and 5 found it independently | Override re-applies the base condition it keyed around | The key was chosen against base's condition, then base was called as if the condition were gone; the unit tests pinned the service's `null`, not what base did with it | Fixed: in a tournament the model returns `participant.RandomBattleEquipment` itself. Lesson appended to `lessons/gamemodels-services.md` |
| 2 | LOW | `ResolveDummyId`'s settlement fallback could not run in production (the only caller passed `null`), yet five tests and three docs described it as a participant, settlement, empire chain (lenses 1, 5, 6) | Dead branch kept alive by tests | Carried from the #137 extraction; `docs/audits/cluster-gamemodels.md` had flagged it | Folded into `ArmourDummyId` (participant culture, else empire), five tests removed, one empty-culture test added; same practice-fight behaviour as before |
| 3 | LOW | Stale docs: tournament armour still described as the culture kit (`arena.md` overview and how-to, the NPC handbook), test counts, `SubModule.cs:1227` (now 1201), "five methods", the registry and index one-liners (lens 4) | Doc sweep | The first doc pass searched for the vanilla claim, not for every description of TAOM's own behaviour | All updated in the same change |

**Risk checked, no change needed:** returning own armour to the skip simulation. Its defence term divides by armour
weight: no armour gives NaN (the fighter falls at the first exchange), zero-weight armour would give infinity and could
hang the loop. No armour item in any installed module weighs 0, and no troop that can enter a tournament (tier 3 to 5,
the culture's basic and elite troops, garrison troops) lacks a battle set or armour; the armourless sets belong to the
hill troll and the bandit cultures' creatures (lens 5). **Accepted:** one null-guard ternary in the override body
(`GetObject<CharacterObject>(null)` throws); the policy lives in `ArmourDummyId` (lens 1, 6).

**Follow-ups, not applied:** `ITournamentService`'s header still says members take sealed types (none do) and imports
three unused namespaces (lens 6, outside the diff); the six reskinned cultures' practice dummies never resolve
(`tournament-armor-assignment.md`, an existing content decision); the `bannerlord-1.4.5` line still gives tournament
fighters the culture kit (Mike's call); no GitHub issue yet (public, on Mike's word).

## Why each lens saw what it saw

Lenses 1, 2, 4 and 5 traced the skip path into vanilla's base and found finding 1; lens 3 confirmed the change is
cheaper than before; lens 6 judged the shape optimal apart from the dead branch.
