# Agent Operating Manual

This manual describes the existing **Claude subagent** runtime. Codex sessions
use the [Codex operating guide](codex-operating-guide.md); all providers follow
the [shared policy](../../.ai/policy.md) and the user's assigned role. The tool
allowlists, slash commands and dispatch behavior below are client-specific,
not a grant of authority or a description of Codex's available tools.

**Audience:** any subagent spawned in the TAOM project (the custom agents in `.claude/agents/`, and ad-hoc `Explore` / `Plan` / `general-purpose` agents). Explore and Plan agents read this at the start of the run; other agents consult it. It tells you the execution model, which tools to run and how, and which skills exist (so you can *recommend* them; see below).

> **What reaches you:** custom and general-purpose agents get CLAUDE.md, its imports (AGENTS.md and `docs/ai-includes/orientation.md`) and the unscoped `.claude/rules/` automatically, and a path rule loads when you read a matching file. The built-in Explore and Plan agents get none of these, and no subagent gets the main session's memory. This manual adds the execution model and the tool catalog.

---

## 1. Execution model (the rules that constrain you)

1. **You can only use the tools in your `tools:` frontmatter.** It's an enforced allowlist with no fallback. If you reach for a tool you don't have, it fails.
2. **You cannot invoke skills (slash commands), and you cannot spawn other agents.** None of the TAOM custom agents are granted the `Skill` or `Task` tool. So when a job calls for a skill, **you recommend it — you do not run it.** Put the recommendation in your final report and let the orchestrator (the main session) invoke it. Examples you will commonly want to recommend:
   - build won't compile → recommend **`/build-fix`**
   - a TAOM C# bug / "why is this broken" → recommend **`/investigate`**
   - a NATIVE crash (`0xC0000005` / AV in `TaleWorlds.Native.dll`) → recommend **`/native-crash-triage`** (you CAN run its tool yourself: `python tools/native_crash_triage.py --rva 0x<offset>`)
   - the installed game version changed / "GAME VERSION DRIFT" in session output → recommend **`/engine-bump`** and treat all crash evidence as suspect until it runs
   - creature/mount authoring work → recommend **`/new-creature-mount`** (and READ `docs/ai-includes/creature-mount-authoring.md` yourself — it is the authoritative workflow)
   - scope-locking edits to one dir → recommend **`/freeze`**
   - pre-merge review of a finished feature → recommend **`/deep-review`** / **`/ship`**
   - an engine-binding or signature concern → recommend **`/verify-bindings`** / **`/research`**
3. **Report, don't guess, on environment failures.** Missing tool, broken path, MCP down, unset `BANNERLORD_GAME_DIR` → state it and stop; don't try to self-heal infra (`.claude/rules/environment-failures.md`).
4. **Stay in your lane.** Respect the scope your spawn prompt gave you. `Main/IoC.cs` and `Main/SubModule.cs` are single-owner convergence files — recommend the edit, let the orchestrator make it.
5. **Have a retry budget.** Same file + same error across ~3 attempts → stop and report what you tried; don't whack-a-mole.

---

## 2. Tool catalog — how to actually execute (via your Bash tool)

| Need | Command | Notes |
|------|---------|-------|
| **Engine process understanding** | `Read` [`docs/reference/engine/`](../reference/engine/) | **First for "how does X work" questions.** 19 processes pre-analyzed + TAOM-relevant gotchas: lifecycle, formations, mount/rider, campaign-mission seam, heartbeat, agent spawn, GauntletUI, GameModel, save/object system. Check here before cold decompile. |
| **TaleWorlds signature / decompile** | `pwsh tools/taom-src.ps1 path <FullTypeName>` | **PRIMARY for signature verification.** Decompiles the installed DLL (auto-detected version) on cache miss, prints an absolute `.cs` path. Compose: `rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)` |
| Browse engine source for patterns | `Read`/`Grep` under `E:\Decompiled_Bannerlord\` | The dump, with older baselines beside it, can lag an engine bump. Fine for browsing; for authoritative signatures still prefer `taom-src`. ⚠️ SHIPPING-CLIENT build: editor-only types (`MBEditor`, `AnimalSpawnSettings`, FBX toolchain) only exist in `_editor_build\`. "Absent from dump" ≠ "doesn't exist." |
| Decompile fallback | `ilspycmd "<dll>" -t "<Type>"` or the `ilspy` MCP | Only if `taom-src` fails. |
| **Build** | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | Use this, NOT `./build.ps1`, during agent work (avoids `out/` contention). Keep both flags: `-p:ModuleId=` skips the build package's copy targets and `-p:DisableModuleCopy=true` skips TAOM's `DeployTAOMDependenciesStubs`; see the caveat below. |
| **Test** | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Add `--filter "FullyQualifiedName~X"` to narrow. |
| Engine-binding gate | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | Verifies patch/GameModel/reflection bindings resolve against the installed engine. |
| Troop equipment refs | `python tools/validate_all_troop_refs.py` | Underwear-bug gate across all 7 culture troop XMLs. Proves refs resolve **on disk** — NOT that the engine loaded a NEW item file (those load only at a full game restart; naked-in-game with a green gate = new-file-not-loaded, not a data defect). |
| API signature snapshot | `pwsh tools/snapshot_api_surface.ps1 [-Check]` | Regenerate / verify the committed signature snapshot (self-labels the installed version). |
| **Native crash site naming** | `python tools/native_crash_triage.py --rva 0x<EventLog-fault-offset>` (or `--ip 0x<RIP> --base 0x<module-base>`) | Names a native CTD site WITHOUT symbols: pdata function bounds, hexdump, referenced strings, caller chains. Full protocol (Event Log, debugger setup): `.claude/skills/native-crash-triage/SKILL.md`. |
| **Creature-mount data parity** | `python tools/audit_mount_parity.py` | Diffs a mount's Monster/usage/action surfaces vs warg/elephant/horse. Run BEFORE battle-testing creature changes; extend its `FILES`/`MOUNTS` maps for new creatures. |
| Doc health | `python tools/lint_docs.py --summary` | Dead links / stale-version refs / orphan docs / config-example drift (doc JSON vs shipped config) / version mismatch (the AGENTS.md Target line or a snapshot header vs the pin) / the ADR-011 context budget. |
| **Claude-config / foreign-skill security audit** | `python tools/audit_claude_config.py` (self) or `--root <repo> --external` (vet a foreign skill at full severity) | Stdlib + optional YARA; deterministic, read-only. Self-audit before ship; `--external` BEFORE adopting an outside skill. Full skill: `.claude/skills/security-scan/SKILL.md`. |
| Doc graph | `python tools/graph_query.py metrics` (+ `explain <doc>` / `path <a> <b>`) | Query/audit the docs link graph: god-nodes/bridges/orphans (`metrics`), a doc's neighbourhood (`explain`), shortest path between two docs (`path`). `--json` for machine output. Full ref: [`docs/features/doc-graph.md`](../features/doc-graph.md). |
| Full tool list | see [`tools/README.md`](../../tools/README.md) | Generators, rebalancers, localization, faction-map, etc. |

**The target engine version** is the `Target:` line in [AGENTS.md](../../AGENTS.md), pinned in `.claude/pinned-game-version.txt`; `taom-src` auto-detects the installed one. A doc naming an older version as the current target is stale. `lint_docs.py` catches only marker phrasing, so a clean run does not prove there is none ([#405](https://github.com/haterade22/TAOM/issues/405)).

> **⚠️ `-p:DisableModuleCopy=true` does NOT prevent deployment to the game install** (verified 2026-08-06 against `bannerlord.buildresources` 1.1.0.129). In that package's `build/Basic.targets`, only the `PostBuildCopyToModules` *wrapper* is gated on the flag; `CopyBinariesWindows` (L53) and `CopyModule` (L64) each carry their own `AfterTargets="PostBuildEvent"` with conditions that omit it, so they fire regardless. Every "safe" agent build has in fact been writing to `<game>\Modules\`. This is invisible while Bannerlord is closed and becomes a hard `UnauthorizedAccessException` / `IOException` build failure the moment it is **running**, because the game holds `0Harmony.dll` and `Bannerlord.ButterLib.dll`.
>
> **If a build fails on `CopyFolder` with "used by another process", the game is open — do not kill it** (the user may be mid-repro). Add `-p:ModuleId=` to genuinely skip all three copy targets while leaving assembly references intact: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`. That only skips `SubModule.xml` token replacement, which matters for deployment, not for compiling or running tests.

---

## 3. Skill catalog — what to RECOMMEND (you can't invoke these)

Grouped by purpose. Authoritative list + when-to-use routing: the **Skills** index in [`CLAUDE.md`](../../CLAUDE.md).

- **Build/debug:** `/build-fix` (compile errors, minimal diffs), `/investigate` (root-cause C# debugging), `/native-crash-triage` (native CTDs — Event Log offsets + `tools/native_crash_triage.py` + debugger protocol), `/engine-bump` (game version changed — baseline-preserve, regen, re-verify), `/agent-introspection-debugging` (failing agent runs).
- **Build/verify a change:** `/verify` (build+test+git), `/verify-bindings` (engine API bindings + snapshot), `/deep-review` (multi-agent review), `/review-codex` + `/codex-verify` (Codex — costs money), `/ship` (full completion sequence).
- **Research:** `/research` (decompile + analyze a TaleWorlds class), `/taom-src` (one-shot signature lookup), `/xslt-check` (XSLT passthrough).
- **Authoring:** `/new-feature`, `/new-culture`, `/new-creature-mount` (rideable creatures — warg-parity workflow over `docs/ai-includes/creature-mount-authoring.md`), `/lord-skills`, `/author-armor`, `/localize`, `/new-adr`, `/issue`.
- **Scope/hygiene:** `/freeze` + `/unfreeze` (edit lock), `/scope-check`, `/deslop`, `/commit-split`, `/context-save` + `/context-restore`, `/context-budget`, `/skill-stocktake`.
- **Adoption/security:** `/adopt-external` (review an external repo/article and fold useful parts into TAOM — if your job is evaluating an outside source, recommend this), `/security-scan` (audit TAOM's own Claude config for secrets / permission / hook-exfil / MCP risk via `tools/audit_claude_config.py`; for a FOREIGN/untrusted skill repo run `python tools/audit_claude_config.py --root <repo> --external` to fire the SkillSpector-derived threat categories at full severity BEFORE recommending adoption — the automated supplement to `/adopt-external`'s manual security-pass).

When your job hits one of these situations, finish your analysis and **name the skill in your report** so the orchestrator runs it.

---

## 4. Where the project conventions live (read on demand)

Custom agents already hold AGENTS.md's invariants. Read these when your task touches them (Explore and Plan agents especially):

- Architecture / layers: [`architecture.md`](./architecture.md) · patterns: [`patterns.md`](./patterns.md)
- TDD workflow: [`tdd-enforcement.md`](./tdd-enforcement.md) · testing: [`testing-guide.md`](./testing-guide.md)
- Adapter pattern (ADR-007), thin entry points (ADR-002), no #region/[Obsolete]/#if DEBUG (ADR-003/004/005): [`docs/adrs/`](../adrs/README.md)
- TaleWorlds research workflow: [`taleworlds-research-guide.md`](./taleworlds-research-guide.md)
- The path-scoped `.claude/rules/*.md` load by themselves when a custom or general-purpose agent reads a matching file. Explore and Plan agents: no document says they do for you, so read the area's rule directly before touching its files.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-teams.md](./agent-teams.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/README.md](../modding/README.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
