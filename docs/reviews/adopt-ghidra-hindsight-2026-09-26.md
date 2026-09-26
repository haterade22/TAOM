# Adoption Review: Ghidra and Hindsight

**Date:** 2026-09-26 · **Procedure:** [`docs/ai-includes/external-repo-adoption.md`](../ai-includes/external-repo-adoption.md) · **Issue:** [#688](https://github.com/haterade22/TAOM/issues/688)

| Source | License | Outcome |
|---|---|---|
| [NationalSecurityAgency/ghidra](https://github.com/NationalSecurityAgency/ghidra) 12.1.4 | Apache-2.0 | **Adopted** as an external tool behind [`tools/native_decompile.py`](../../tools/native_decompile.py) |
| [vectorize-io/hindsight](https://github.com/vectorize-io/hindsight) | MIT | **Rejected**; nothing ported |

Mike asked for both to be added to the toolkit. Both verdicts below were confirmed with him before
any work began.

## Ghidra

### What it is

The NSA's software reverse-engineering suite (Java; latest release 12.1.4 on 2026-09-21, pushed
to 2026-09-25 at review time). It disassembles, decompiles to C, recovers types and cross-references,
and runs headless. Ghidra 12.1 bundles PyGhidra 3.1.0, a Python API that hosts the JVM in-process
through JPype.

### Security pass

- **Downloads verified.** SHA-256 of `ghidra_12.1.4_PUBLIC_20260921.zip` matched the release body
  (`ddac49f9...d4db`); `OpenJDK25U-jdk_x64_windows_hotspot_25.0.4.1_1.zip` matched the Adoptium API
  checksum (`00c847d8...9283`).
- **Advisories.** The project lists ten. Three sit on paths an analyst could reach, and 12.1.4
  patches all three: GHSA-c3pf-92jf-vq96 (the Swift demangler runs a bare `swift-demangle` name, which
  Windows resolves from the working directory first; below 12.1.4), GHSA-pcfh-853f-q3gh (Swift
  demangler path restored from unverified config; below 12.1.3) and GHSA-vxmq-6v38-hgf6 (a cyclic x64
  unwind chain overflows the PE importer's stack; 10.0 up to 12.1.4). Our only input is TaleWorlds'
  own MSVC-built DLL, which never reaches the Swift analyzer. We run no Ghidra Server and import no
  foreign projects or archives, which is where the path-traversal advisories live.
- **Outbound network.** Not audited. Our use needs none: the JVM runs inside the Python process with
  no listening port, and the projects, settings, cache and temp directories are all local on `E:`.
- **Verdict, two ways.** Safe to learn from. Safe to install **as a standalone tool**, the way
  `ilspycmd` backs `taom-src`: it is not a Claude plugin, registers no hook or MCP server, and puts no
  text into any session's context. The never-install rule targets plugins that inject context; it
  does not apply here.

### Novel vs duplicative

| Need | TAOM already had | Verdict |
|---|---|---|
| Function bounds, referenced strings, callers of a crash RVA; minidump decode | `tools/native_crash_triage.py` | Keep ours |
| RTTI, vtables, xrefs, byte-pattern signatures | `tools/native_sig_author.py` | Keep ours |
| **What the function does, as C** | nothing: `/native-crash-triage` Phase 2 said to hand-decode the instructions | **Adopted** |
| GUI, Version Tracking, BSim, Ghidra Server, the debugger | nothing | Skipped: no current need |
| A third-party Ghidra MCP server (pyghidra-mcp and others) | nothing | Skipped: a script serves every AI client and a human alike, is testable, and adds no third-party code to the harness |

### What was built

`tools/native_decompile.py --rva 0x...` prints the function containing the RVA as decompiled C,
plus `--callers N` levels of callers. The first run on a binary imports and auto-analyses it into a
Ghidra project at `E:\ghidra\TAOM\<build folder>-<sha256[:16]>`; later runs open it read-only. The
hash key means a Steam overwrite or a wEditor update gets a fresh analysis, never a stale one.
`native_crash_triage.py` prints the matching command after naming a site. Setup, timings and use:
[`docs/features/ghidra-native-decompile.md`](../features/ghidra-native-decompile.md).

### Install traps found

1. **PyGhidra and this machine's Python.** `pyghidra 3.1.0` pins `Jpype1==1.5.2`, whose newest wheel
   is for CPython 3.13; the system Python is 3.14 and PyPI's JPype 1.7.1 wheel does not satisfy the
   pin. PyGhidra therefore lives in a Python 3.13 venv at `E:\Tools\ghidra-venv`, installed offline
   from the wheels Ghidra ships, and the tool re-runs itself under that interpreter.
2. **The Store Python sandbox.** The only 3.13 on the machine is the Microsoft Store build, which
   redirects writes under `AppData` into its package folder. The JVM runs inside that process, so
   Ghidra's settings and cache landed in
   `AppData\Local\Packages\PythonSoftwareFoundation.Python.3.13_...\LocalCache\`, and the first
   analysis died in Ghidra's OSGi bundle host (Felix: "The data file must be inside the data dir",
   then a `NullPointerException` in `GhidraScriptUtil.acquireBundleHostReference`). Pointing
   `application.settingsdir`, `application.cachedir` and `application.tempdir` at `E:\ghidra\user\`
   in `support\launch.properties` moved them out of the redirected tree, and the next run imported,
   analysed and saved the client DLL in 213 s.
3. **A failed start never exits.** That failed run printed its traceback and then stayed alive at
   1.5 GB until killed. A thread dump (`jstack`) showed one non-daemon Java thread besides `main`:
   `FelixDispatchQueue`, left running by the half-started OSGi framework, and the JVM does not let
   the process end while it lives. A successful run releases the bundle host and exits normally.
   The tool now ends with `os._exit` once the JVM has started (`exit_process`, after the project is
   closed), which ends the process whatever threads are left. Checked on an error raised after the
   JVM start (an RVA in the PE header): exit 2 in 3.5 s. The Felix failure itself was not
   reproduced, because the directory fix above removed its cause.
4. **Java 8 first on PATH.** Ghidra needs JDK 25. `JAVA_HOME_OVERRIDE` in `support\launch.properties`
   selects it, so the machine's `JAVA_HOME` and PATH were not touched.

### Review

One `deep-reviewer` pass through the tooling lens, on the working tree. Every finding was
reproduced or checked against the code before it was fixed, and each fix has a test:

| Severity | Finding | Fix |
|---|---|---|
| MEDIUM | A crash in an x64 leaf function (no `.pdata` entry) made triage exit blaming the offset math, with no decompile line, although Ghidra names the function (`0x404B17`, reproduced) | triage names the leaf case and prints the line on that path too |
| LOW | A DLL in a folder Ghidra refuses as a project name (`crash #635`), or a relative `--project-dir`, ended in a Java traceback | the key keeps only `[A-Za-z0-9_-]` from the folder name (the existing key is unchanged); the project dir is made absolute |
| LOW | A `.gpr` left without its `.rep` failed every later run with a traceback | exit 2 naming the two files to delete |
| LOW | A `$TAOM_GHIDRA_PYTHON` that is a launcher without PyGhidra could re-run the tool without bound | the re-run sets `TAOM_GHIDRA_CHILD`, and a child never re-runs |
| LOW | The caller cap and a caller shared by two functions were untested | two tests |

Considered and left: `GhidraProject.importProgram(File)` is deprecated for removal since 12.0 but
works on the pinned 12.1.4 (noted in the feature doc for the next upgrade); a failed decompile of the
requested function still exits 0, which no consumer reads.

## Hindsight

### What it is

An agent memory server: `retain` extracts facts, entities and relationships from text with an LLM,
`recall` searches them (semantic, BM25, graph and temporal, then reranked), and `reflect` answers
questions over them. It runs as a Docker image or a pip-installed daemon with an embedded Postgres,
and needs an LLM provider for extraction. It ships a Claude Code plugin (marketplace 0.7.5).

### Security pass (raw files read, not summaries)

From `hindsight-integrations/claude-code/` on `main`:

- **`hooks/hooks.json`** registers `SessionStart`, `UserPromptSubmit` (`recall.py`, 45 s timeout),
  `Stop` (`retain.py`, async) and `SessionEnd`. Every command runs `python3 ... || python ...`.
  CLAUDE.md records that `python3` on this machine is a Store alias that hangs, so the recall hook
  could hold each prompt up to its timeout (not measured).
- **`scripts/recall.py`** runs on every prompt of five characters or more (`autoRecall` is on in the
  shipped `settings.json`). It queries the memory bank and returns the hits as
  `hookSpecificOutput.additionalContext` inside `<hindsight_memories>` (lines 252-278). Anything
  retained earlier, including text from reviewed code, logs or pasted reports, comes back as
  context in later sessions: a standing injection channel, which AGENTS.md "Untrusted input" exists
  to keep closed.
- **`scripts/retain.py`** posts the new part of the transcript to the daemon every tenth turn
  (`retainEveryNTurns: 10`) and once more at session end. Tool calls are excluded by the shipped
  settings but included when the key is absent (`retain.py:149`).
- **`scripts/lib/llm.py`** picks the extraction provider from the first key it finds in the
  environment: `OPENAI_API_KEY`, then `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, `GROQ_API_KEY`
  (lines 18-22), and hands it to the daemon. Each retain is a paid call on whichever key is present.
- **`scripts/lib/daemon.py`** starts the daemon as `uvx hindsight-embed@latest` (lines 40-42): an
  unpinned package fetched and executed at hook time.

**Verdict, two ways.** Safe to learn from. **Not safe to install**: a per-prompt context injection,
an unpinned download executed from a hook, and paid LLM calls on an auto-detected key.

### Why it does not fit TAOM, even with the plugin set aside

- **ADR-011 "Memory".** Machine-local memory holds resume cards only, and every durable fact lives
  in the repository, where the laptop, Codex and a reviewer can read it. ADR-011 Alternative 5
  already rejected durable knowledge in auto memory; Hindsight is the same thing in an opaque
  database.
- **AGENTS.md "Paid AI dispatch".** Retain spends money on a schedule, not on an explicit request.
- **Duplicative.** Recall over TAOM's knowledge already runs through grep over
  `docs/reviews/lessons/`, the graphify code graph, `/doc-graph`, Serena and the `taom-moduledata`
  MCP. No measured gap exists that semantic recall would close. If one appears, the route is a
  measured trial under the conditions in the adoption procedure, over the archive only, with no hooks.

Nothing was ported.

## Provenance

Both have rows in [`docs/reference/provenance-register.md`](../reference/provenance-register.md):
Ghidra `interop-only` (TAOM runs the installed tool; no Ghidra code is copied, and nothing from it
ships), Hindsight `comparison-only`.
