# RCA: review of PR #1306, creature saddle families and the mount reins guard

PR #1306 (REDACTEDSTUFF, head 68be41024) adds two features. Patch153 (`MountReins`) swaps the one
`AddHorseReinsClothMesh` call in `MountVisualCreator.AddMountMesh` for a hook that skips the cloth reins on a
mount whose Monster has no rein skeleton. Patch154 (`CreatureSaddles`) gives the warg, fell warg, dire wolf and
spider their own harness families (11, 12, 13) in memory after the engine reads the XML. A session behaviour
moves a mismatched harness off a player clan hero in an old save. The review ran at tier 1 (a transpiler, two
postfixes on engine deserializers, a campaign behaviour): lens 5 (data flow) and lens 2 (engine compatibility),
each at the agent definition's `max`. No XML changed, so lens 7 was not in scope. Codex did not run: an outside
PR needs Mike's word for each paid pass.

Lens 2 found nothing incompatible: the IL swap keeps the stack balanced on the installed v1.5.7 IL, and all 15
groups of TaleWorlds members resolve. Two native behaviours stay UNVERIFIED (which pointer the crash reads, and
whether native code expects the cloth after a skip). The battle lab on 2026-11-15 is the only evidence for them.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `CreatureSaddleRosterTests` read only `<equipment>` children, so it skipped the 6,134 TAOM sets that use `<Equipment>`. Its floor of 50 pairs still passed with 81 (lens 5, G1) | Gate blind spot | The floor proved the reader found something, not that it found the rosters | Fixed: the test reads both spellings. A full scan with troop overrides and template sets found 0 mismatches before and after. Existing lesson `testing-qa.md:392`; new lesson on the floor appended |
| 2 | MED | `creature-mount-authoring.md:296` and checklist item 9, and `spider.md:115`, say family 1 carries the vanilla rider-death and dismount surface; the PR changes the family in memory without a word there (lens 5, I1) | Conflicting docs | The authoring guide predates the PR, and the PR did not search for other claims on `family_type` | Fixed: the three lines name Patch154. Lens 2 found no `FamilyType` reader in a single-player mission except the inventory and the conversation gestures. The rider-death check on a creature mount is owed in game |
| 3 | LOW | `coop-interop.md:360` said the host's repair reaches the client with the save. The authority's `Clan.PlayerClan` is not a guest's clan (lens 5, I2) | Co-op claim | The row assumed a single player clan | Fixed in `coop-interop.md` and `creature-saddles.md` |
| 4 | LOW | `clan.Heroes.Concat(clan.Companions).Distinct()`: `Clan.AddCompanion` already adds the companion to `Heroes` (v1.5.7 `Clan.cs:1252-1264`) (lens 5, I3) | Redundant code | The two lists read as separate | Fixed: `clan.Heroes` with a comment |
| 5 | LOW | Doc claims: the cloth branch is reached "through `AgentVisuals`" (only `MissionScreen` reaches it); "once per module copy of an id" (the engine merges copies into one node first); the "taken" families omitted 3, 5, 6 and 8; the chariot is the Armory's; the writer comment omitted the engine's copies; the roster counts matched nothing that computes them (lens 2 rows 1, 2, 6, 7; lens 5, I4) | Engine claims from names | Each sentence describes the expected engine path, not the traced one | Fixed in the registry, both feature docs and three comments; the family test now avoids 0 to 6, 8 and 10; the counts are removed. Existing lesson `adapters-taleworlds-api.md:466` (merge layer) |
| 6 | LOW | `MountReinsLiveDataTests` required family "1" while `CreatureSaddleRosterTests` accepted 1 or TAOM's value, so an Armory update to TAOM's families would fail the reins test with a wrong message (lens 2 LOW 8; lens 5, I5) | Inconsistent premise | Two tests written for two features | Fixed: both accept either value |
| 7 | LOW | A rider on a creature mount gets no mounted gestures in a mission conversation (`GetAnimDataForRiderAndMountAgents` knows families 1 and 2 only; it does not throw) (lens 2 LOW 4) | Side effect | Only a native and DLL-wide scan finds the reader | Documented under "Not covered" |
| 8 | LOW | `AddHorseReinsClothMesh` has no reflection-site row (lens 2 LOW 1) | Gate coverage | Not a reflection call: the lookup uses `nameof` | NOT APPLIED: `nameof` fails the compile on a rename, and `MountReinsBindingTests` fails when the lookup returns null |
| 9 | LOW | The catch in `MountReinsHook.cs:28-39` guards reads that cannot throw (lens 2 LOW 7) | Dead guard | House style puts a guard in every hook | NOT APPLIED: it costs nothing at runtime, and a future read could throw. FOLLOW-UP no issue: a simplicity pass may remove it |

## Why each lens caught or missed these

- Lens 2 alone found findings 5 (the engine paths), 7 and 8: each needs the engine source or a scan of every
  installed DLL.
- Lens 5 alone found findings 1, 2, 3, 4 and 6 by reading each consumer and test against the data.
- Neither lens can prove the native behaviour; that stays with the battle lab and the in-game check.

## In-game checks owed

- A campaign battle with a dire wolf and `light_harness` at character detail above 2 (Patch153).
- The inventory refuses a horse saddle on a warg; the old-save repair moves one (creature-saddles.md).
- A rider on a warg, dire wolf and spider dies, dismounts and falls as before (finding 2).
