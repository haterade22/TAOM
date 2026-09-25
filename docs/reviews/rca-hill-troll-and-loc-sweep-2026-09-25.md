# RCA: hill troll on its own skeleton, and the localization stale sweep (2026-09-25)

**Scope:** one session's work, reviewed together. Change 1, the hill troll on `troll_skeleton_a` (race, Monster,
standalone `as_hill_troll_warrior`, 307 masters and 480 clips, the `hill_troll` troop, the #649 Brute Force tree
extended to it): the tooling Mike committed in `9354b0b2` and `fef273bf` plus the uncommitted edits on top.
Change 2, the localization sweep on Mike's temporary API key (uncommitted). Live `LOTRLOME_Armory` and `TAOM_Map`
files written in the window were swept and attributed. Reviewed by `/deep-review` in two waves of `deep-reviewer`
lenses: standards, engine compatibility, data flow and XML first; tooling correctness, efficiency, completeness and
design second. Scope file: the session scratchpad's `review_scope.md`.

## Summary

The troll's data held up: every bound clip exists, every master is on the troll skeleton, the Monster, race and
set agree, and the C# extension is engine-compatible. The defects clustered in two places. The translator runs
of Change 2 introduced two HIGH regressions the builder's own verification could not see, because it checked row
ids, counts and terminators, never the text beside a row in another file or the script a word was written in: 26
seeded duplicate rows per language that silently replaced curated ones (Italian "Dale" became "Valle" again), and
translations carrying words from the wrong writing system, in the rows and in the cache. The rest are claims the
builder wrote without enumerating what they depend on: "4,049 rarely played codes" without listing the engine's
non-battle consumers of an action set, "a stack in the Mordor lord template" without reading which template a lord
uses, and a reinstall-safe live change without the gate the trap index asks for. Two findings repeat lessons already
on file. Every confirmed defect in wave 1 is fixed, and Mike answered the four behaviour questions (reach by eye
height, trolls in three Mordor clans, reuse the troll's own idles, Duinhir keeps his title). Wave 2 found tools
whose default invocation was unsafe, a test module CI never ran, a verify gate that failed on the correct state, and
docs that lagged; every confirmed wave 2 defect is fixed except the export script's write-before-verify, which is
recorded with five optional proposals as a follow-up.

## Findings (wave 1)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The first translator run seeded the 26 `taom_aso_*` keys into every keybind file; they already had rows in the module file, and the engine keeps the row a language loads last, so 22 copies (IT "Valle", SP "Tierra Brune", DE/FR untranslated Blue Craig) replaced the curated rows | Tool (seeding without ownership) | The run printed "26 ids seeded into std_taom_keybind_strings"; the builder read it as other sessions' pending ids and verified only the troop-name files. No test compared one id across two files of a language | `translate_with_claude.py` `key_owners` + `skip_ids_for` (never seed or translate a key another source owns or another file carries); `LocalizationKeyConsistencyTests.EachLanguage_GivesAnIdOneText_AcrossAllItsRegisteredFiles`; lesson |
| 2 | HIGH | Translations with words from another writing system: three new ("[Ривенделл]新obranец", "estão投", "黑numenor"), about 120 older across nine languages, all also in the cache | Translator-model output | `absorb_translations` validates placeholders only; the builder's verification read ids, counts and terminators; the earlier half-translation pass looked only for English words taken from the source | `LanguageTextIntegrityTests.NoTranslatedString_MixesWritingSystems` (+ the incident's strings as DataRows); rows and cache repaired; lesson |
| 3 | MED | The new trap row in `orientation.md` was 197 characters (cap 180); `lint_docs.py --fail-on-drift` exits 1, so the commit hook and CI would refuse | Doc budget | The builder ran `lint_docs.py` in report mode and read only its dash line | Row shortened; lesson: run the mode the hook runs |
| 4 | MED | "4,049 rarely played codes still name the human clip" was wrong: the party-screen and encyclopedia idle, the map-conversation bodyguard idles and the victory cheers play human clips on the hunched rig, and 49 derived sets carry 2,376 human-clip overrides | Unverified claim | The builder judged frequency from code names (swim, ladder, cutscene) instead of listing the engine's consumers of the set (`CharacterTableau`, `ConversationMissionLogic` `_poses`, `AgentVictoryLogic`) and the derived sets | Ledger, binder docstring, CHANGELOG corrected; retargeting those clips put to Mike; lesson |
| 5 | MED | `troll-race.md` step 3 still said "Human clips need no retarget on a re-framed rig", which the same doc and four others contradict since 2026-09-24 | Stale claim | The 2026-09-24 correction sweep grepped for the claim's other wording and missed this one inside a numbered recipe step | Corrected; lesson: when reversing a claim, grep its key terms, not one phrasing |
| 6 | MED | No AI Mordor lord fields either troll: both stacks sit in the culture's `kingdom_hero_party_mordor_template`, and all 15 Mordor clans bind their own template (`Clan.DefaultPartyTemplate` falls back to the culture's only when a clan has none) | Unverified claim, pre-existing gap | The hill troll copied the cave troll's placement and its comment ("spawns via the Mordor hero party template") without tracing `LordPartyComponent` | Docs, CHANGELOG and the XML comment corrected; where trolls spawn put to Mike; lesson |
| 7 | MED | The Brute Force distances scale with `AgentScale` only, while the engine's reach is `arm_length` times scale: the hill troll's 2.79 at scale 1.09 against the cave troll's 0.9 at 1.9. The code inconsistency is confirmed; its in-game effect is not | Design (second Monster) | Extending the tree to a second Monster, the builder compared action sets and clips, not the Monsters' size fields | Known limitation in the CHANGELOG, measured in the owed smoke; fix put to Mike; lesson |
| 8 | MED | Nothing would notice a reinstall reverting the hill troll's skins, Monster and set | Repeat: trap "Unversioned modules" | The trap index says to land an in-repo gate with a live fix; the tool had read-back checks but no mode that could run as a gate | `wire_hill_troll_race.py --check` (three tests); lesson |
| 9 | MED | Regenerating `taom_troop_name_strings.xml` encoded another session's uncommitted Gondor renames, and the sweep re-translated 75 rows to them; committed alone, 11 languages would name troops the committed English does not | Commit coupling | A generator reads the working tree, other sessions' edits included | Commit note in the CHANGELOG and the memory card; lesson |
| 10 | LOW | Seeded rows took a bare `\r\n` in files that end lines in `\r\r\n` (156 repo rows, 228 live Armory rows) | Repeat: plan 022's lesson (2026-09-24) named the same defect and left the fix as a follow-up | The builder also wrote, earlier the same day, that the translator preserved terminators, having checked `write_back` and not the seeding path | `sync_missing_ids` keeps each line's own terminator (test); 384 rows repaired; memory corrected |
| 11 | LOW | `TrollBruteForceWiringTests` read the live Armory without `[TestCategory("LiveInstall")]` | Test hygiene | Inherited from #649; the extension rewrote two tests without checking the class tag | Tagged; the config-only test moved to `TrollBruteForceConfigTests` |
| 12 | LOW | `ActionSetId` (the cave troll's set) sat unprefixed beside `HillTrollActionSetId`; four summaries still said "cave troll" only | Naming, stale comments | The extension edited the lines it needed and not their neighbours | Renamed `CaveTrollActionSetId`; comments fixed |
| 13 | LOW | `clips_index.json` stored the master's root key count as `master_frames` (a future consumer would truncate 106 masters) | Latent naming trap | Written before the sparse-key finding | Renamed `master_root_keys` |
| 14 | LOW | The English "Nõldorin" typo spread into six re-translated rows; three languages turned "Bowman" into a crossbowman and FR carried a garbled row from HEAD | Translation quality | Re-translation copies what the source says; no collision or sense check covers near-synonyms | Source and rows fixed by hand |
| 15 | LOW | The snapshot bound `act_troll_brute_force` without declaring it; its README said 5 standalone sets and "the only LOTR race at risk" | Snapshot drift | #649's lines were held back while that session was open and never added after it closed | Snapshot equals live; README corrected |
| 16 | LOW | The Umbar clan split reaches new campaigns only (`Clan.Name` is saved) and the CHANGELOG did not say so | Undocumented limit | The builder did not check how an existing save keeps a clan's name | CHANGELOG note |
| 17 | LOW | `rebalance_troops.py` and `analyze_troop_balance.py` exempt `cave_troll` but not `hill_troll` | Copy-a-troop checklist | The troop copy updated the validator's lists, not the balance tools' | `rebalance_troops.py` lists it; `analyze_troop_balance.py` already caught it through its `troll` marker, so its added id was reverted in wave 2 |

Not defects, owed: a Kit re-save of `anim_hill_troll_2h_bash` and `hill_troll_a_geo.tpac` (each RDC entry predates
its last write; the load effect is native and unverified) and the Custom Battle smoke.

## Findings (wave 2: tooling correctness, efficiency, completeness, design)

Each re-checked against the code or data before it was fixed; the generator's verify claim was proven by running it.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 18 | MED | The translator's new skip rule hid 45 keys per language from every pass: a key both module and xslt strings declare was skipped for each because the other carried it | Fix that over-corrects | The wave 1 fix was tested for the seeding it stopped, not for the discovery it also changed | `skip_ids_for` subtracts the target's own rows; test "a key the target carries is still examined" |
| 19 | MED | The binder's 13 tests were pytest functions; CI's `unittest` collects 0 and cannot import pytest | Wrong runner | The author ran pytest locally and read green | Rewritten as 26 `unittest` tests; new ratchet `tools/tests/test_ci_runner_compat.py` fails on any new module that imports pytest |
| 20 | MED | Run as its docstring said, the binder bound a clip that was never written (`--clips-dir` defaulted to none) | Unsafe default | The live run passed the flag, so the default path never ran | `--clips-dir` defaults to the install's folder and a missing one is refused |
| 21 | MED | `gen_troll_anim_clips.ps1 -CloneByName -Verify` exited 1 on the correct live state (the clip refused by design counted as missing), and troll-race.md said both verify modes ran clean | Claim without exit code | "clean" was read off the counts, not the exit status | Refused-by-design clips listed and not counted, in verify and apply; proven on live (exit 0, `refused-by-design=1`); doc corrected |
| 22 | MED | The generator's default skeleton GUID is human, and BoneNum cannot tell it from `troll_skeleton_a`, so a re-import could wire troll masters to the human rig | Unsafe default | The only rig check compared bone counts | Every mode refuses a folder whose wired masters name another skeleton; proven on live |
| 23 | LOW | `tools/README.md` carried raw CR bytes that split the translator's table row | Byte hygiene | A Python edit wrote `\r\r\n` literally | Row rejoined with the escaped text; the Bash heredoc trap bit again during the fix, so the fix used a script file |
| 24 | LOW | `wire_hill_troll_race.py --check` passed an empty set and one the binder never bound | Envelope check | `--check` compared structure, not content (wave 1's pattern again) | `--check` also needs one action, one troll clip and exactly one Brute Force binding; 3 tests |
| 25 | LOW | Deleting a helmet-hair default left a doubled CR in a CRLF skins file | Terminator | Tested on LF fixtures only | Whole-line deletion with the tag's own terminator; CRLF test |
| 26 | LOW | A variant Monster under both its old and new id would pass the read-back | Read-back blind spot | The read-back builds a dict | Refused when both ids exist; test |
| 27 | LOW | The retarget script's `.DONE` said "done" even when clips failed, and an argparse error left no `.DONE` for a detached caller | Status signal | Only the happy path was traced | `.DONE` is `done` or `fail: ...`, written on every exit |
| 28 | LOW | `read_anim_keyframes_tpac.ps1` exited 0 with missing clips | Exit code | Misses were printed, not returned | Exit 1 on any miss |
| 29 | LOW | The generator's post-write failures never reached its exit code; the clone path skipped the checksum fix silently without python | Exit code | Two copies of one check drifted | One `Confirm-WrittenClips` for both paths, failures exit 1 |
| 30 | LOW | The terminator test would pass a `\r\r\r\n` line | Weak assertion | A count stood in for a byte comparison | Byte-exact comparison around the seeded line |
| 31 | LOW | `fighter_hill_troll` duplicated `fighter_cave_troll` byte for byte | Copy instead of reuse | The troop was built as a copy | The troop points at the cave troll's template; the copy is gone |
| 32 | LOW | The Brute Force reach scaled with `AgentScale` alone, which measures size only on `human_skeleton` | Proxy outside its domain | The cave troll's tuning was carried over | `BodySize` = scale times eye height over 1.70 (Mike's choice); cave troll unchanged and pinned |
| 33 | LOW | The binder's own process check failed open | Unsafe default | A local copy of a shared helper | Uses `_gamedir.game_or_kit_running` |
| 34 | LOW | Gate 2 checked C# only; a lord's title in `lords.xml` differed from its registration | Gate scope | The gate was written for the incident's C# shape | Gate 2 covers data XML and XSLT (proven red); Duinhir's title fixed on Mike's choice |
| 35 | LOW | Nothing checked that keys two English sources share have one English text | Gate scope | Not in the incident | `EveryKeyTwoEnglishSourcesShare_HasOneEnglishText` (proven red) |
| 36 | LOW | The writing-system gate read repo rows only, not the cache that repopulates all three modules | Gate scope | The live files are outside the repo | `NoCachedTranslation_MixesWritingSystems`; the split-word rule judged per hyphen part (a KO name) |
| 37 | LOW | Completeness: stale ledger redo path, stale troll-race.md sections, gates unregistered, README flags, live loc edits unrecorded, eight number slips, the clip-name limit only in one doc, the CNs troll names disagreeing | Docs lag the change | Docs were updated per edit, never re-read as a whole | All corrected; the redo path now names `--check`, the bind step and `-Renames` |

### Convergence pass (one `deep-reviewer` on the applied fixes)

| # | Sev | Bug | Why missed | Preventive action |
|---|---|---|---|---|
| 38 | MED | Fix 18 was incomplete: three `taom_aso_*` keys that only `global_strings.xml` declares kept their rows in the module file, so the owner's pass skipped them and no other source declared them | The fix was tested on the case that motivated it, not on every source-and-file combination | Rows moved to the keybind file in all 12 languages (0 hidden, measured); `ShippedKeyCoverageTests` fails on any key no pass examines |
| 39 | MED | The snapshot README's replay could not restore the 23 live rows that replaced an older translation: discovery only visits rows missing or still in English | The replay was written from the tool's name, not its discovery rule | The 23 ids listed with a reset-to-English step before the replay |
| 40 | MED | The skeleton guard could not see EMPTY masters (a fresh Kit import), and the ledger's redo path skipped the wiring step | The guard was proven on the wired live folder only | The Fab path refuses to wire EMPTY masters in another folder without an explicit `-SkeletonGuid`; `-CloneByName` lists UNWIRED masters, refuses to write and fails `-Verify` (all three proven on a zeroed copy); redo step names the wiring |
| 41 | LOW | The data-default gate missed 509 XSLT sites written `{{=key}}` | The regex matched the unescaped form only | Attribute values unescaped in `.xslt`; floor raised to pin them (6,621 sites measured) |
| 42 | LOW | The retarget `.DONE` said "done" when no clip matched | Only a run with clips was traced | `fail: no clip matched the inputs` |
| 43 | LOW | `-RetargetReport` read the scale through a locale-formatted string | en-US hides it | Doubles rounded, no string step |
| 44 | LOW | Six doc slips (derived-set counts, a stale RCA line, a README row, two "repeats the 26" lines, a tool count, a test count) | Written from memory of the previous state | Corrected against the data |

### Review of the four changes Mike asked for after the review (engine compatibility and data flow)

No engine API was incompatible: the health path (`CharacterObject.MaxHitPoints` into `BaseHealthLimit` in the
campaign, the Monster's `hit_points` in Custom Battle, no double count), `Monster.StandingEyeHeight`, the recruit
event callers and the party-template spawn all checked against the installed v1.5.3.

| # | Sev | Finding | Why missed | Action |
|---|---|---|---|---|
| 45 | MED | The reused Fab idles are not looping clips, while the vanilla clips behind the 172 codes are `cyclic` (starts continue into loops, cheers priority 64), and their consumers set the action once | The reuse rule matched clips by name and motion, never by the flags the code's consumers rely on | Known limitation in the CHANGELOG; the fix (per-code clips cloned from each code's vanilla clip) needs new packages and a Kit save, so it goes to Mike; in-game effect unverified |
| 46 | MED | Mordor's culture template (0 to 7 trolls) is not unused: a companion clan on a Mordor settlement falls back to it, and the average wage reads it | The claim came from the 15 shipped clans, not every caller of `DefaultPartyTemplate` | Texts corrected (CHANGELOG, troop comment, feature doc); trimming the stack is Mike's call |
| 47 | MED | The cave troll's `TroopWeight` row is still commented out, so it weighs 1.0 beside the hill troll's 4.0 | The copy took the row's value, not whether it was live | Known limitation; the uncomment changes the cave troll's balance, Mike's call |
| 48 | MED | The recruit cost is the player's own resource, and a Free-culture player loses a recruited troll to alignment desertion the next day; the CHANGELOG said "50 War Spoils" | `resource_id` read as the charged resource | Texts corrected; whether to refuse the charge is Mike's call |
| 49 | LOW | The Chinese Duinhir name rows used another transliteration (and, in Simplified, another lordship) than his bio | The title re-translation read the English only | Rows and caches aligned with the bios |
| 50 | LOW | The smash log reported the uncapped body size, and 0.00 on the early return | The log took the input, not the value used | The ring reports the scale the distances use |
| 51 | LOW | Two stale comments (the resource file's "gates any future path", the config's "grow with AgentScale") | Comments outside the edited lines | Corrected |
| 52 | LOW | The MCM toggle's hint does not say it now carries troll health; the +100 has no tooltip line; the offline auto-resolve simulator assumes 100 health | New effect on an old surface | Known limitations (the hint and tooltip need new player-facing text and a translator run) |

Not applied, with reasons: the efficiency lens's batched keyframe writes in the retarget script (worth it only if
more human-clip batches come), hoisting the translator's per-call parse (0.3 s per run), the export script's
write-before-verify (the workflow stages exports), deleting `RegisteredDefaultRoundTripTests`, the shared binder
writer and the 42 keys both string files declare (follow-ups, other files or other sessions' code).

## Root-cause patterns

**Verification that checks the envelope, not the content (1, 2, 10, 14).** Every translator check the builder ran
read ids, counts, terminators and placeholders. None read the text beside a row in another file of the same
language, or the script a word was written in. The engine's rule (last loaded wins) and the model's failure mode
(a word from another language) both live in content.

**Claims written from the builder's model, not from an enumeration (4, 5, 6, 7).** "Rarely played", "no retarget
needed", "spawns via the lord template" and "the same smash fits" were each true of the builder's mental model and
false of the engine or the data. Each would have been caught by listing the consumers: the engine's callers of an
action set, every doc carrying the claim, the template a lord actually reads, the Monster fields the engine uses.

**Repeats (8, 10).** Both had a written rule: the trap index's "land an in-repo gate", and plan 022's lesson naming
the seeding terminator bug. The rules were read and not applied, so the preventive action this time is code (the
`--check` mode, the tool fix and its test), not more text.

**Wave 2: the safe path was not the default path (20, 22, 27, 29, 33).** Each tool behaved correctly when called
the way the builder called it, with the right flag, on a machine with python, with the game closed. The default
invocation, the one a later reader copies from the docstring, bound a missing clip, would wire the wrong skeleton,
and could write while the game ran. Code made the fix: defaults that are safe, refusals where there is no safe
default.

**Wave 2: green on the wrong runner, clean on the wrong signal (19, 21).** The binder's tests passed under pytest and
never ran on CI; the generator's verify was called clean from its counts while it exited 1. Both are the evidence
rule's "read the exit code" at the level of the whole tool, and the ratchet test now makes the first mechanical.

## Why each lens caught or missed what it did

Standards found 3 and 11 to 13 and the missing C# drift gate, and could not see 1 or 2 (not its domain). Engine
compatibility found 1, 4 and 5 from the engine's own load order and callers. Data flow found 1, 2, 6, 7 and 9 by
tracing each key and troop to what the player sees. XML found 1, 2, 6, 8, 10 and 14 to 16 with its own byte-level
and script checks; none of the repo's existing gates covered any of them. In wave 2, tooling correctness found 18
to 30 by running every tool on real and synthetic inputs (it could not run the PowerShell verify; the orchestrator
did); design found 31 to 36 and the reach proxy; completeness found 37 by reading each doc against the data;
efficiency found no defect and three optional costs.

## Feedback to codify

The lessons below go to `docs/reviews/lessons/`. No new rule file: each prevention is either code (gates, tool
fixes) or a one-line lesson in the category the next builder reads. Wave 2 added four: "The safe invocation is the
default invocation" and "A verify mode that fails on the correct state is a dead gate" (build-tooling-workflow), "A
tool test must run under CI's runner, not only the author's" (testing-qa, with the `test_ci_runner_compat.py`
ratchet as its gate) and "Gate the translation cache, not only the rows" (localization-ui). The features review added two more (a reused clip must carry its code's flags, animation-skeleton; a culture template is not unused because every shipped clan binds its own, data-content-cultures).
