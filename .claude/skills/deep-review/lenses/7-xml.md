# Agent 7 lens: XML & ModuleData Integrity

XML & MODULEDATA INTEGRITY REVIEW. Treat every changed XML line as code the engine will run
without telling you when it misreads it. Most XML mistakes this project shipped were silent: the
engine logged them, ignored them, or loaded the file with a default, and every C#-centric lens
passed.

FILES: the list in your spawn prompt (repo and live install, absolute paths for live files).
DIFF: `git diff` for repo files. Live-install files have no history: read the whole file, and
compare with the snapshot under docs/reference/ when one exists.

FIRST: which machine is this (docs/reference/development-machines.md)? On the laptop the live
installs are partial, so broken refs into them are environment gaps, not defects.

1. RUN THE GATES. Use the "Gate per file kind" section of `.claude/rules/moduledata-validation.md`
   (the rule loads with any ModuleData XML you Read in the repo; if it has not, Read only that
   section: Grep `^## Gate per file kind`, then Read from that line) and run
   every gate its table lists for the kinds of file in scope; always run
   `python tools/validate_xml_schemas.py <every changed engine XML file>` (it finds each file's
   module itself; add that module's SubModule.xml to the list when it changed). Quote each
   command and its summary line. A gate that could not run (no install, no lxml) is NOT RUN with
   the reason, never a pass. Do NOT run dotnet: parallel reviewers building at once collide.
   NAME the shipped-data tests that cover your files (CulturePartyTemplateTests,
   CharacterFaceCoverageTests, StarterKitCoverageTests, CareerCultureCoverageTests,
   LanguageDataXmlTests, ...) and the orchestrator runs them.

2. ENGINE SEMANTICS AND LOAD PATH, per changed element or attribute. Validators check TAOM's
   rules; the engine has its own. Open the Deserialize that reads each new or changed name
   (`pwsh tools/taom-src.ps1 path <Type>`) and quote the read. A name no deserializer reads is
   silently ignored. An XSD failure is strong evidence but the XSD can lag the code: confirm
   against the deserializer, and report XSD-STALE if the code does read it. Also check the value
   type it parses (int.Parse throws on "1.5"), the ref prefix it expects (Item., NPCCharacter.,
   Culture.), and what an ABSENT attribute defaults to (an NPCCharacter with no <face> renders as
   a toddler). Load path: `validate_xml_schemas.py` names how the engine loads a file. LANGUAGE FILE
   (listed by a `language_data.xml`) and STYLESHEET (applied to the data merged before its
   registration) load, but no XSD checks them. NATIVE means a `project.mbproj` row names it (or the
   stylesheet beside it); the line gives the row's id and type and claims no load, since the engine
   loads a row only when it requests that id or reads that type, and the gates are in
   `moduledata-validation.md` "Gate per file kind". NOT REGISTERED means no `<XmlName>`, manifest or
   `project.mbproj` row loads it, though the engine can still read it by fixed name
   (`global_strings.xml`, the scene lists); that is expected for C#-read data. Every line that says
   "(a real fault)" (a manifest the engine cannot load, a `Languages/` file no loadable manifest
   lists) is a finding, and it fails a by-name run (exit 1, summary FAIL); the summary line counts
   them. MISSING is a registration that loads nothing, or a `project.mbproj` the engine cannot load. Confirm each XSLT still matches the elements it targets; a new file in a
   folder-registered Armory directory loads only after a game RESTART, so report the restart plus
   visual check as OWED; Agent 5 item 10 owns the cross-module ref trace, and never ask for a
   DependedModule row. Then read the Traps table in CLAUDE.md and apply every row your files touch; each linked doc
   names its gate.

3. BYTE FIDELITY for any file a script or bulk edit touched: BOM kept or dropped as the original
   had it, each line's own terminator kept (the language files vary by type and even by line: measure,
   never assume; `docs/localization/TRANSLATOR_GUIDE.md` "Line endings and encoding"), no whole-file rewrite behind a two-line
   change (compare `git diff --stat` with the intended edit), no `--` inside an XML comment, no
   backup with an .xml extension left in a globbed folder.

4. INTENT. Read the issue, feature doc or commit body the change serves and confirm the data
   does what it says: the right troop in the right tree, the tier and stats the spec names,
   player-facing text wrapped as {=KEY} and registered for translation.

OUTPUT FORMAT:
- GATES: one line per gate: the command, then PASS / FAIL (count) / NOT RUN (reason)
- Each finding: file:line, what is wrong, the gate or engine evidence, severity (CRITICAL: load
  failure, CTD, hang, server boot kill; HIGH: silent misread or wrong gameplay data; MEDIUM: dead
  or redundant data; LOW: byte hygiene), and the fix
- TESTS FOR THE ORCHESTRATOR: the shipped-data test classes to run
- OWED: in-game checks no static gate can prove (restart plus visual, new-campaign-only data), written in the format of the "How to verify in game" section of `docs/features/TEMPLATE.md`
Summary: N files, G gates run (F failed, X not run), K findings by severity
