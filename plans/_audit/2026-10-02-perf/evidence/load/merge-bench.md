# The module-XML merge, measured offline with the engine's own code, 2026-10-02

Written for: the writer and executor of plan 042, and Mike. Harness: `merge_bench.ps1` and `xslt_cost.ps1` in this
folder, run with Windows PowerShell 5.1 (the .NET Framework CLR, 64-bit) on Mike's desktop; raw output in
`merge-bench-results.txt`.

## How

`merge_bench.ps1` loads the installed `TaleWorlds.ObjectSystem.dll` and `TaleWorlds.Library.dll`, rebuilds the
exact file and XSLT lists the engine builds for one type (v1.5.3 `MBObjectManager.GetMergedXmlForManaged`: the
session's module order from its rgl log, `ModuleHelper`'s path rules, the "Campaign" game-type filter), fills the
schema tables the game fills at startup (`XmlResource.ReadXsdFileAndExtractInformation`), then:

1. times the engine's own `MBObjectManager.CreateMergedXmlFile(list, xsltList, skipValidation: false)`;
2. times a prototype that keeps one `XDocument` across all files and compiles each XSLT once, calling the
   engine's own private `CreateDocumentFromXmlFile`, `MergeElements`, `ToXDocument` and `ToXmlDocument` by
   reflection, and converting to `XmlDocument` only around an XSLT and once at the end;
3. compares the two merged documents' `OuterXml` character for character.

The NPCCharacters list it builds (57 entries: 56 files plus one XSLT-only node) is the same 56 files, in the
same order, that the 2026-10-02 campaign load opened between its first and last NPCCharacters schema lines in
`rgl_log_32020.txt` (from `SandBoxCore/ModuleData/spnpccharactertemplates.xml` at 12:45:15.805 to
`TAOM/ModuleData/named_companions/named_companions.xml` at 12:45:28.583). `campaign-load-npc-sequence.txt`
lists them shifted by one line (see the attribution note in `load-time-rgl.md`).

## Results (two runs each; all four outputs identical to the engine's, character for character)

| Type | Entries | XSLTs | Engine merge | Prototype | Prototype breakdown (second run) |
|---|---|---|---|---|---|
| NPCCharacters | 57 | 1 (`lords.xslt`) | 5,432 / 5,647 ms | 2,384 / 1,774 ms | XSLT 1,246 ms, file load and validation 361 ms, MergeElements 51 ms |
| Items | 142 | 0 | 2,404 / 2,420 ms | 374 / 343 ms | file load 174 ms, MergeElements 147 ms |
| EquipmentRosters | 31 | 0 | 591 / 552 ms | 102 / 101 ms | file load 55 ms, MergeElements 18 ms |
| GameText | 33 | 3 | 379 / 384 ms | 218 / 220 ms | MergeElements 74 ms, XSLT 75 ms, file load 60 ms |
| **Total** | | | **about 8.8 s** | **about 2.4 to 3.1 s** | |

The in-game phases measured larger (about 23 s for NPCCharacters and Items at the 2026-10-02 campaign load),
so the in-game saving is at least the offline 6 s and probably more; plan 040's per-type stamp measures it.

## What is left after the prototype

- **`lords.xslt`, about 1.2 s on its own.** Compiling it costs 0.14 to 0.21 s (`xslt_cost.ps1`; every other
  TAOM XSLT compiles in 14 ms or less). The rest is the transform: an identity copy of the whole merged NPC
  document plus 396 templates, one per lord (`match="NPCCharacter[@id='lord_1_1']"` and so on), so every
  NPCCharacter element is tested against 396 patterns. Each template replaces its lord wholesale. It is
  generated (`tools/complete_lords_xslt.py`). It is not a pure replacement: its XmlNode has no XML file
  (`path="lords"`), so it rewrites the vanilla and SandBox lords merged so far, and each template keeps the
  lord's original children other than face, skills, Traits and Equipments. A plain override file would need
  `_replaceWhileMerging` (which the engine copies into the output, so it is equal only with that marker
  removed) and those vanilla children baked in, regenerated at every engine bump. About 1.2 s per load
  against a regeneration step: Mike's decision.
- **Per-file schema compilation**, inside the 361 ms of NPC file loads: `LoadXmlWithValidation` builds and
  compiles a new `XmlSchemaSet` twice per file. Caching one per XSD path is the next step.
- `MergeElements` regroups every element merged so far on every call; at 51 to 147 ms per type it is not
  worth touching yet.
