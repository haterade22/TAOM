Plan 042: cut the 15 to 28 seconds the engine spends merging module XML at every campaign load and custom battle start, without changing one byte of the merged result, measured first with the engine's own code.

WHY (measured 2026-10-02 from the engine's rgl logs, C:\ProgramData\Mount and Blade II Bannerlord\logs, scripts
plans/_audit/2026-10-02-perf/evidence/load/rgl_xml_cost.py and rgl_xml_sequence.py):
- A new campaign's loading screen lasts 50.2 s (rgl_log_32020.txt, 12:45:00 to 12:45:50). 329 ModuleData files are opened
  in it; the per-type merge costs NPCCharacters 56 files 13.3 s, Items 142 files 9.6 s, GameText 30 files 1.7 s,
  EquipmentRosters 31 files 1.4 s: about 28 s of the 50. By module: TAOM 100 files 15.1 s, LOTRLOME_Armory 153 files
  8.6 s, SandBoxCore 3.2 s, SandBox 2.0 s, Native 0.9 s.
- A custom battle load (rgl_log_56116.txt, 14:33:30 to 14:33:55) merges 274 files: NPCCharacters 48 files 8.3 s, Items
  141 files 5.7 s.
- Game start is not affected (161 files, 0.2 s): the cost is in the validated, merged types.
- The cost per file grows with what was merged before it: vanilla's NPC files, merged into a 1.2 to 2.2 MB document,
  cost 0.01 to 0.1 s each; TAOM's, merged into 4.9 to 9.8 MB, cost 0.14 to 0.29 s each even when tiny
  (characters/creature_bandits.xml, 3 KB: 0.20 s). TAOM's 2.6 MB characters/lords.xml costs 1.5 s.

THE ENGINE CODE (v1.5.3 managed dump E:\Decompiled_Bannerlord\_categories_v1.5.3\Core\TaleWorlds.ObjectSystem\
TaleWorlds.ObjectSystem\MBObjectManager.cs; re-check every line against the installed DLL with
pwsh tools/taom-src.ps1 path TaleWorlds.ObjectSystem.MBObjectManager before relying on it):
- CreateMergedXmlFile (:962-978) loops over the type's files. Per file: if an XSLT sits beside it (HandleXsltList,
  :945-960: "<file>.xsl" or "<file>.xslt"), ApplyXslt (:980-991) builds a NEW XslCompiledTransform, compiles the
  stylesheet and transforms the WHOLE accumulated document; then CreateDocumentFromXmlFile (:1339-1354) reads the
  file and, when validating, LoadXmlWithValidation (:1057-1110) builds a NEW XmlSchemaSet from the XSD, compiles it,
  injects `_replaceWhileMerging` into every complex type, reprocesses and compiles again, then loads the file with
  validation; then MergeTwoXmls (:993-1005) converts the WHOLE accumulated XmlDocument to an XDocument
  (ToXDocument), merges (MergeElements), and converts the result back to an XmlDocument (ToXmlDocument). So the
  merge is O(files x accumulated size), and the schema and every XSLT are compiled again per file.
- TAOM ships 8 XSLTs (Main/_Module/ModuleData/*.xslt: heroes, lords, spclans, spcultures, spkingdoms,
  module_strings, comment_strings, action_strings), the live Armory 8, TAOM_Map 1.

WHAT:
1. Measure first, offline, with the engine's own code: a test or tool (net472, referencing the installed
   TaleWorlds.ObjectSystem.dll and TaleWorlds.Library.dll, test category LiveInstall so hosted CI skips it) that
   builds the exact per-type file list the game builds for the campaign (the module order and XmlNode lists of
   the active modules' SubModule.xml files, game types honoured as the engine honours them: read
   GetMergedXmlForManaged :873-943) and times MBObjectManager.CreateMergedXmlFile per type. Record the times per
   type; they must land near the rgl figures above (state the machine). This is the baseline the fix is judged on.
2. An equivalent merge that produces the identical document: the leading option is a Harmony prefix on
   CreateMergedXmlFile (or the narrowest method that works) that keeps the accumulated document as one XDocument
   across the loop, converts each file once, merges with the engine's own MergeElements, applies XSLTs from a
   per-process cache of compiled transforms (input: the XDocument's reader), reuses one compiled XmlSchemaSet per
   XSD path (including the `_replaceWhileMerging` injection), and converts to XmlDocument once at the end. The
   writer verifies every engine call it keeps or replaces against the installed DLL and decides whether a prefix
   that returns false is acceptable under .claude/rules/harmony-patches.md, or a narrower patch fits.
3. Proof of equivalence: for every type the campaign and custom battle merge, the new path's output serialised
   with the same settings is byte-identical to the engine's (OuterXml or a canonical writer), on the live install's
   real module set, in the same offline harness; plus unit tests on small fixtures for each branch (no XSLT, XSLT,
   keepDuplicates, empty xsd, `_replaceWhileMerging` replace semantics, a file whose XSLT removes nodes).
4. The second lever, if the first is refused or insufficient: fewer files. A build step that concatenates TAOM's
   per-culture files of one type into one generated file in the deployed module (sources stay per culture in the
   repo; SubModule.xml names the generated file), proven identical by the same harness. The Armory's 131 item
   files are the maintainer's (unversioned); propose, do not touch.
5. Logging (D6): at each merge, one INFO line per type: `[XmlMerge] type=<id> files=<n> ms=<total> path=<fast|vanilla>`
   plus the xslt and schema cache hits; a fallback to the vanilla path logs its reason once. A load-end summary line
   with the total.

TESTS AND GATES: the harness's equivalence check is the gate; record the dotnet suite's base failure set first
(EveryLanguage_DeclaresARowForEveryEnglishKey) and the Python suite's (three named in baseline.md).
OUT OF SCOPE: changing any XML content, any XSLT's behaviour, the live Armory or TAOM_Map.
STOP conditions to include: the offline harness cannot reproduce the engine's merged output for a type (stop
before writing the fast path); any byte difference in the equivalence check; MergeElements or the schema
injection is not reachable without copying engine code wholesale (say how much would be copied).
ADDENDUM (orchestrator, 2026-10-02, measured after the brief; binding):
- Step 1 is proven feasible and already prototyped: plans/_audit/2026-10-02-perf/evidence/load/merge-bench.md and
  merge_bench.ps1 (Windows PowerShell 5.1 loading the installed TaleWorlds.ObjectSystem.dll). The engine's
  CreateMergedXmlFile runs offline once the schema tables are filled (XmlResource.ReadXsdFileAndExtractInformation per
  XSD path). Engine merge, four types: about 8.8 s; a prototype that keeps one XDocument across files and compiles each
  XSLT once, using the engine's own CreateDocumentFromXmlFile, MergeElements, ToXDocument and ToXmlDocument: about 2.4
  to 3.1 s, with OuterXml identical character for character for NPCCharacters, Items, EquipmentRosters and GameText.
  The C# test harness the plan builds should port this script (same list rules, same equivalence check).
- Of the prototype's remaining NPCCharacters time, lords.xslt is about 1.2 s (transform, not compile: 396 per-lord
  templates over the whole merged document; generated by tools/complete_lords_xslt.py). Per-file schema compilation is
  the next cost. Treat both as later stages of the plan, each proven by the same harness; the XDocument fast path is
  stage one.
- lords.xslt is not a pure replacement (orchestrator, read 2026-10-02): its node is XSLT-only (TAOM SubModule.xml
  XmlName id="NPCCharacters" path="lords", no lords.xml beside it), so it rewrites the vanilla and SandBox lords
  merged so far, and each of its 396 templates sets the attributes afresh (xsl:copy drops the originals) and replaces
  face, skills, Traits and Equipments while keeping the lord's other original children
  (apply-templates select="node()[not(self::face or self::skills or self::Traits or self::Equipments)]"). A plain
  override file would need _replaceWhileMerging (which the engine's MergeElementAttributes copies into the output, so
  equality holds only with that marker removed) and the vanilla children baked in, regenerated from the XSLT's own
  output at every engine bump. That trade (about 1.2 s per load against a regeneration step) is the maintainer's to
  decide; the plan presents it and does not make it.
