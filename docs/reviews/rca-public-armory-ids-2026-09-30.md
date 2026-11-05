# RCA: 1.5.x troop tree on the 1.4.8 line (#697), deep review of 2026-09-30

**Scope.** The `/deep-review` pass run on the laptop's Bannerlord 1.4.8 install (six agents: standards,
1.4.8 engine acceptance, efficiency, completeness, cross-system data flow, and a 1.5.x-dependency
agent) over branch `fix/public-armory-ids-145`: 92a30ba4 (the 16 troop files, `gondor.json`, the
Gondor and enlistment equipment sets and `named_companions.xml` copied verbatim from
`bannerlord-1.5.x`, plus 998f054c's Gondor volunteer fallback) and the working-tree removal of
`harad_howdah_crew` and `hill_troll`.

**Top line.** The engine side is clean: the ported files use no element, attribute or enumerated
value absent from the 1.4.5 files, validate with 0 errors against the installed 1.4.8 XSDs, and every
troop, culture, pool and upgrade reference resolves. The build passed; one test failed on 92a30ba4
and named the first finding. Four findings survived verification, all of the same kind: a verbatim
data port carries rows whose meaning depends on code or data that exist only on the source branch.
Three were fixed in 10a52fb3; the rest are recorded as known limitations in the CHANGELOG.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | LOW (dead data) | `harad_howdah_crew` and the `hill_troll` troop row arrived with the port. On 1.5.x the crew is placed only by `HowdahCrewSpawner` (`ElephantConfig.HowdahCrewCharacterId`, #627) and the troll troop comes with its HP model, party templates and costs (4ceae903); the 1.5.x reachability test exempts both. This branch has none of that code, so the rows were unreachable and unspawned. | Cross-branch port | The port copied the data files and one C# fallback, and treated "no broken refs" as proof the data was complete. A reference validator checks what the data names; it cannot see what the data needs from code. The reachability test caught it on the first laptop build, because the desktop (1.5.3 only) cannot compile this branch. | Rows removed (10a52fb3). Lesson in `lessons/data-content-cultures.md`. |
| 2 | MED | `mirkwood_rochenlas` and `mirkwood_beleglas` were mounted on `taom_animalia_elk_a` / `taom_elk_a` with `taom_elk_saddle_a` (709649c3, c79a5852). The elk behaviour, attach logic and size sync live in `Main/Features/Elk`, `Animalia` and `MonsterSize`, all absent here. The 1.5.x sources say both Monsters are `base_monster="horse"`, so they would likely ride as plain horses, but nothing on 1.4.8 had tested that. | Cross-branch port | Same as 1; also item refs to the new Armory could not be resolved on the laptop's old Armory, so the elk rows looked like every other unverified item. | Reverted to the c5b84fb4 mounts (10a52fb3). |
| 3 | HIGH (plausible, not reproduced) | KEYforce's drop deleted `gondor_ring_peasant`, pooled at Glanhir at 50 percent since v2.0.25. 1.5.x accepted the save break. #670 reads Patch83 as blind to a troop whose row is deleted: the engine unregisters the non-ready object before `OnGameLoaded`, and `IsTroopUpgradeable` and the volunteer tick then dereference a hollow `CharacterObject`. On this branch the risk is higher, because public 1.4.8 saves load here. | Save compatibility | The decision to accept the break was made for 1.5.x, whose players were fewer, and travelled with the data when it was ported to the line the public plays. | Row restored as shipped, `is_hidden_encyclopedia="true"`, in no pool; `RetiredSaveCompatTroops_StillDefined_SoOldSavesResolveThem` pins it (RED before the restore). Lesson in `lessons/state-lifecycle-save.md`. |
| 4 | LOW, recorded | The port leaves companion data behind: Black Numenorean cavalry wear `sm_md_num_barding_*` while Mordor lord sets keep the old harness (ee20755a also changed `lords.xml` and the lord sets); 29 renamed and 21 new name keys have no Languages entries; `tools/generate_gondor_troops.py` and `tools/data/armor_roster_tiers.json` are 1.4.x and would regenerate the old tree. | Cross-branch port | Each companion change lives in the same 1.5.x commit as the ported data, in a file the port did not take. | Recorded in the CHANGELOG as known limitations. |

## Root-cause pattern

Every finding comes from one move: taking data files verbatim from a branch whose code the target
does not share. The check that would have caught all four is a list, per commit that shaped the
ported files, of the other files that commit changed. `git log <base>..<source> -- <ported paths>`
followed by `git show --stat` on each result took the 1.5.x-dependency agent a few minutes and
produced findings 1, 2 and 4. A reference validator could not, because a row that nothing references
and a mount that exists in the Armory are both valid data.

Finding 3 is a decision, not a defect, that was right on its branch and wrong on this one. The
question to ask when porting any deletion of a shipped id is who loads saves on the target branch.

## Why each agent missed or found these

- **Standards, efficiency:** out of scope by design; the C# change was a two-entry pool swap.
- **1.4.8 engine acceptance:** its question was whether 1.4.8 reads the data, and it does. Behaviour
  that depends on TAOM code is outside that question.
- **Completeness:** found the missing CHANGELOG entry and the missing issue (now #697).
- **Data flow:** confirmed no dangling reference to the removed rows, and flagged the elk riders
  independently of the dependency agent.
- **1.5.x-dependency agent:** added for this review because the change was a port; found 1, 2 and 4
  and pointed at #670 for 3.

## Feedback to codify

A port between engine branches gets the dependency pass as a standard lens: for each source commit
touching the ported paths, list the files it changed outside them, and classify each as ported,
unneeded, or a dependency the target lacks. Recorded as a lesson rather than a skill change until
it recurs.
