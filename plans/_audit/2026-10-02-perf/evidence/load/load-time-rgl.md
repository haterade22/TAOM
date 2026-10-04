# Load times from the engine's own log, 2026-10-02

Written for: Mike and the writer of plan 042. Source: the engine's rgl logs in
`C:\ProgramData\Mount and Blade II Bannerlord\logs` (millisecond timestamps), sessions of 2026-10-02 on Mike's
desktop. One new-campaign load (`rgl_log_32020.txt`, the loading screen 12:45:00 to 12:45:50, matching
`taom_debug_2026-10-02_12-40-04.log`) and one custom battle load (`rgl_log_56116.txt`, 14:33:30 to 14:33:55). Two
loads are evidence of where the time goes, not of its spread; plan 040's stamps and plan 042's harness measure
that.

## Where a new campaign's 50 seconds go (`campaign-load-gaps.txt`, `campaign-load-xml.txt`)

| Step | Seconds | Evidence |
|---|---|---|
| Module XML merge, validation and object creation | about 28 | 329 files; NPCCharacters 56 files 13.3 s, Items 142 files 9.6 s, GameText 1.7 s, EquipmentRosters 1.4 s |
| The campaign map scene read | about 5 | 4.59 s after `atmosphere.xml` to "Town scene manager: Total mesh: 6909", then 0.39 s |
| New-game hero creation | about 6.6 | gaps of 3.37 s and 3.18 s among the "creating hero from template" lines |
| Campaign map terrain shaders, not in a sack, plus terrain setup | about 3.0 | 2.97 s from `rglTerrain_shader_generator::clear` past 138 "Missing shader from sack" lines (`terrain-shader-gap.txt`); a battle scene with its own sack takes 0.14 s for the same step. Corrected 2026-10-03: no shader was compiled in it (the variants came from the runtime terrain cache), so the 2.97 s mixes loading them with the terrain setup of a much larger map; the split is unmeasured (`shader-cache-native.md` section 6) |
| Face morph mapping | about 1.6 | 1.56 s before "gpu_morph_mapping: 6540780" |

## Why the XML merge is slow (engine code, v1.5.3 dump `MBObjectManager.cs`)

`CreateMergedXmlFile` (:962-978) merges a type's files one at a time. For each file it may compile and apply an
XSLT to the whole document so far (`ApplyXslt`, :980-991, a new `XslCompiledTransform` every time), builds and
compiles the XSD twice (`LoadXmlWithValidation`, :1057-1110), and round-trips the whole document so far through
`XDocument` and back (`MergeTwoXmls`, :993-1005). So the cost grows with the files merged before it: vanilla's
early NPC files, merged into 1.2 to 2.2 MB, take 0.01 to 0.1 s each; TAOM's, merged into 4.9 to 9.8 MB, take
0.14 to 0.29 s each even at 3 KB (`campaign-load-npc-sequence.txt`). Note on attribution: the engine logs
"opening <file>" before "opening <schema>", so the scripts' "before" seconds for a file are the previous file's
validation and merge plus this file's XSLT, and a type's list is shifted by one line: in
`campaign-load-npc-sequence.txt` the first NPCCharacters file (`spnpccharactertemplates.xml`) is missing and the
last row (`SandBox/ModuleData/heroes.xml`) is the Heroes type's first file. Per-type totals move by at most one
file at each boundary.

A custom battle pays it too (`custom-battle-load-xml.txt`): 274 files, NPCCharacters 48 files 8.3 s, Items 141
files 5.7 s. Game start does not (161 files, 0.2 s).

History: the merge is not new. On 2026-09-12 (Bannerlord v1.4.8, a saved-campaign load in the archived
`E:\taom-memory-2026-09-02\rgl_log_39684.txt`, 11:37:25 to 11:39:35) it cost about 19 s (NPCCharacters 54 files
11.8 s, Items 128 files 5.1 s); that load's 129 s were mostly two silent stretches, 67 s after the cheat-mode
dump line and 21 s before the morph mapping, which today's loads no longer have. The item merge has grown with
the Armory since (128 to 142 files, 5.1 to 9.6 s).

## What follows

- Plan 042 (brief `evidence/briefs/042-xml-merge-load-time.md`): measure the merge offline with the engine's own
  `CreateMergedXmlFile`, then an equivalent merge that keeps one document, caches compiled XSLTs and schemas, and
  proves byte-identical output; fewer files as the fallback lever.
- The terrain shader step is the first warm-cache measurement for the shader-sack question (FOR-MIKE item 1).

## Game start: about 33 of 53 seconds are shader compiles when the local cache is cold

`startup-shader-compiles.txt` (script `rgl_compile_window.py`). Two of the 2026-10-02 sessions, both on the full
module list (with the Armory), compiled the same 1,335 shaders at game start, all of the `pbr_metallic` family
(768 base, 288 gbuffer, 160 constant output, 80 shadow map, 39 point light): 34.3 s in the 12:39 session and 32.5 s
in the 13:16 session, ending 49 and 52 s after the log began. The session sequence that day:

| Session | Module list | Compiles |
|---|---|---|
| 12:39 | full (with the Armory) | 1,336 |
| 12:48 | without the Armory (a short session, 18 KB log) | 0 |
| 13:16 | full | 1,336 |
| 14:30 | full | 46 |
| 14:56 | TAOM without the Armory or the map | 6 |

So a warm cache serves them (14:30 after 13:16), and the 13:16 session paid them again after a session on another
module list. Verified on 2026-10-03 in the engine (`shader-cache-native.md`): the runtime cache is dropped when
the set of active module ids or the game build changes, and not by a TAOM update that keeps the same modules.
A player who plays vanilla or another mod between TAOM sessions pays about half a minute at the next start on a
fast machine (this one has an RTX 5080), more on a slower CPU; one who plays only TAOM pays it once per install
or game update. The sessions without the Armory compiled 0 and 6 shaders at start, so the 1,335 come from the
Armory's materials: 40 material flag combinations, eight of which are compiled in 104 engine contexts each.
Mike's install has a Kit-written Armory sack, and it did not serve them (`shader-cache-native.md` section 4);
whether to ship sacks stays the maintainer's decision (D8, FOR-MIKE item 1).
