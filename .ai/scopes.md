# Scope routing and knowledge map

[routing.json](routing.json) is the machine-readable routing source. Paths use
forward slashes and case-sensitive Python `fnmatchcase` patterns: `*` also spans
directories. These are NOT Git pathspecs or Claude loader globs. Multiple lanes
can apply. `general` always matches everything, including unknown file types.
Rename detection is disabled intentionally: both deleted and added paths receive
review. Review neighboring unchanged consumers even when absent from the diff.

The rules below are plain Markdown, usable by every AI. Some technical rules
still live under `.claude/rules/` to preserve existing integrations. Read their
technical content directly; do not assume another client loads their frontmatter
or possesses their slash commands. New shared workflow policy belongs in `.ai`,
not in duplicated vendor files. The [shared policy](policy.md) governs roles and
authority when a legacy Claude workflow assumes the reader is a builder.

| Lane | Inspect and read first |
| --- | --- |
| `general` | Requested behavior, omitted files, dependencies, cross-feature interactions, positive requirements, and evidence. [Feature map](../docs/reference/feature-map.md), [lessons index](../docs/reviews/LESSONS-LEARNED.md), [prior review catalog](../docs/reviews/codex-track-record.md). |
| `managed` | C# services, adapters, hooks, GameModels, behaviors, tests, IoC, save/host authority, lifecycle and hot paths. [Architecture](../.claude/rules/csharp-architecture.md), [adapters](../.claude/rules/adapters.md), [Harmony](../.claude/rules/harmony-patches.md), [GameModels](../.claude/rules/gamemodels.md), [tests](../.claude/rules/tests.md), [ADRs](../docs/adrs/README.md). |
| `moduledata` | XML, XSLT, JSON configs and cross-file identity completeness. [XML](../.claude/rules/xml-data.md), [XSLT](../.claude/rules/xslt.md), [troops](../.claude/rules/troops.md), [vanilla comparison](../.claude/rules/vanilla-data-comparison.md), [validator limits](../docs/features/moduledata-validation.md). Additions require checking absent rows in other configs, not just present rows. |
| `ui` | ViewModels, prefabs, sprite identifiers, input, lifecycle, cleanup and native screen seams. [UI rules](../.claude/rules/gui-ui.md), [engine UI process](../docs/reference/engine/gauntletui-viewmodel-screen.md). |
| `tooling` | Python, PowerShell, shell, CI, packaging and data migrations. Inspect destructive targets, credentials, process exits, discovery counts, test fixture independence, artifact scope and reproducibility. [Tooling lessons](../docs/reviews/lessons/build-tooling-workflow.md), [tools index](../tools/README.md). |
| `ai-harness` | Instruction discovery, role boundaries, prompt injection, permission scope, policy drift, stale approval, provenance of reported identities and tool results. [Provider setup](providers.md), [Claude loader facts](../.claude/rules/harness-facts.md). Check vendor claims against current official docs. |
| `claude-hooks` | Hook contracts, activation context, timeouts, exit semantics and platform behavior. [Hook authoring](../.claude/rules/hook-authoring.md), [harness facts](../.claude/rules/harness-facts.md). Hooks do not enforce other clients. |
| `native-dependencies` | C++, vendored source and DLLs. ABI, lifetime, loader/build type, native failure modes, redistributable notices and binary/source correspondence. [Native port rules](../.claude/rules/native-cpp-ports.md), [provenance](../.claude/rules/provenance.md). |
| `assets-localization` | Binary assets, audio, textures, mounts, localized text, technical IDs versus display names, provenance and packaging. Use appropriate inspectors; an opaque binary is UNVERIFIED, not approved because Git cannot display it. [File catalog](../docs/modding/file-catalogue.md), [provenance](../.claude/rules/provenance.md). |
| `docs-policy` | Current claims versus source evidence, links, ADR consistency, schema/config consumers, stale examples, procedures that cannot actually run and exclusions that hide debt. [Docs index](../docs/INDEX.md), [evidence rules](../.claude/rules/evidence-over-claims.md). |

For engine questions, use [engine process docs](../docs/reference/engine/) before
raw decompilation. Use the installed game version/build for signatures. Shipping
client and editor DLLs are distinct. Record external data roots and DLL versions
in evidence; a source SHA alone does not pin installed game or mod assets.

## Beyond the checkout

The installed `TAOM_Map` and `LOTRLOME_Armory` modules contain content and XSLT
outside this repository. A repository packet cannot certify those live files.
If the task depends on them, inventory their exact paths and hashes separately,
record the installed versions, and grant only the required read access. Do not
copy licensed assets into a review packet. Missing external data stays explicit.

## Whole-repository coverage

Use `inventory` to plan and `prepare --full` for a head-pinned audit. Divide each
lane into bounded, explicit path sets. Reports can cover slices; the validator
requires two independent providers for every lane/path across their union. It
does not accept the number of feature folders as evidence of completion. Keep
the same full-audit head until all slices finish. On drift, prepare a new packet;
automatic carry-forward and dependency-based invalidation are not implemented.

After establishing a baseline, review each change and periodically audit unchanged
high-risk areas. Timing and scheduling remain maintainer-operated, not an installed
background task. Build a future calibration set from confirmed RCAs plus refuted
false positives; no automated model ranking or evaluation service exists yet.
