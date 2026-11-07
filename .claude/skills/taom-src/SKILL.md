---
name: taom-src
description: Use before patching, overriding, or adapting a TaleWorlds type — `taom-src path <Type>` decompiles + caches the installed engine, returning an absolute path.
argument-hint: "[path|list|remove|clean] <Type or FullyQualifiedType>"
---

# taom-src — one-command TaleWorlds source lookup

Wraps `ilspycmd` against the **installed DLLs** (auto-detects the engine version from `Version.xml`, currently v1.4.8) and caches per game build at `~/.taom-src/<version>-<stamp>/`, where `<stamp>` is `TaleWorlds.Library.dll`'s UTC write time, so a Steam hotfix that keeps the version string still gets a fresh cache (the old folder stays for any session reading it). Inspired by [vercel-labs/opensrc](https://github.com/vercel-labs/opensrc). Use this **before** every `[HarmonyPatch]`, `GameModel` override, or adapter that touches a TaleWorlds type; never guess signatures from the `E:\Decompiled_Bannerlord\` dump (it can lag the installed engine after a bump).

## Core pattern

```bash
# Decompile + cache (first call) or print cached path (subsequent calls)
pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel

# A short name works when one engine type holds it; an ambiguous one lists the full names
pwsh tools/taom-src.ps1 path DefaultPartyWageModel

# Compose with grep/cat/rg — exactly like opensrc
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
cat $(pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Formation)
```

Progress goes to stderr, path goes to stdout — safe in `$(...)`.

## Subcommands

| Command | Behavior |
|---|---|
| `path <Type>` | Print absolute path to cached `.cs`. Decompiles on miss. A name with no dot resolves through the build folder's `engine-types.tsv`; an ambiguous one throws and lists the candidates. |
| `path <FullType> -Dll <Name>` | Skip the lookup; force a specific DLL (needs the full name). |
| `list` | Show all cached types as a table. `--json` for raw output. |
| `remove <Type>` | Delete one cache entry. |
| `clean` | Nuke the entire cache. |

## How DLL resolution works (on cache miss)

0. **Short-name index** (a name with no dot): `engine-types.tsv` in the build folder lists the short name, full name and DLL of every top-level type in the `TaleWorlds*`, `SandBox*`, `StoryMode*` and `NavalDLC*` assemblies, built once per build from assembly metadata in a few seconds. One match resolves; several throw and list them; a miss rebuilds the index once. The row's DLL is only the first probe, so a moved type still falls through to steps 1 to 3.
1. **DLL index cache**: `dll-index.json` in the build folder (per build since #1038; a shared one in the cache root outlived an engine bump) remembers `type -> dll` from prior lookups. Instant hit.
2. **Namespace heuristic** — `TaleWorlds.CampaignSystem.X.Y` probes `TaleWorlds.CampaignSystem.X.dll`, then `TaleWorlds.CampaignSystem.dll`, then `TaleWorlds.dll`. Catches ~99% of cases on the first probe.
3. **Brute-force iteration** — Last resort: probes every `*.dll` across all search dirs, alphabetically within each.

**Search dirs (all three steps):** the primary `bin/Win64_Shipping_Client` first, then every
`Modules/<Name>/bin/Win64_Shipping_Client` that holds DLLs: the game's own modules (`$VanillaModules` in the script:
`Native`, `SandBox`, `SandBoxCore`, `StoryMode`, `CustomBattle`, `BirthAndDeath`, `Multiplayer`, `NavalDLC`,
`FastMode`) before every other module, so a mod's own copy of an engine assembly (Coop ships about 40, and
they go stale) is read last (#1038). **This is not optional trivia:** several engine assemblies ship
ONLY under a module. `TaleWorlds.MountAndBlade.View.dll` — which owns `CharacterTableau`,
`BasicCharacterTableau` and `AgentVisuals`, i.e. the whole tableau/encyclopedia render path — lives in
`Modules/Native/bin/` and is absent from `bin/` entirely. Likewise `SandBox.*` types live in
`Modules/SandBox/bin/`. Before 2026-08-06 the tool searched `bin/` only and threw "not found in any
DLL" for all of them. `ilspycmd` runs with the game's bin as its reference path (`-r`), so a type in a
module's own bin decompiles against the engine instead of leaving `//IL_` lines.

## When to use

| Situation | Action |
|---|---|
| About to write `[HarmonyPatch(typeof(X), nameof(X.Y))]` | `taom-src path X` first — verify `Y` exists with the expected signature |
| About to override `GetCharacterWage` on `DefaultPartyWageModel` | `taom-src path DefaultPartyWageModel` — copy the exact base signature |
| About to call a TaleWorlds API from an adapter | `taom-src path <Type>` — confirm the method exists in the installed engine with the expected signature |
| Bug investigation: "vanilla does X, what does its code actually look like?" | `taom-src path <ClassName>` then `rg` for the symptom |
| Want to know what's already cached | `taom-src list` |

## When NOT to use

| Situation | Use instead |
|---|---|
| Looking for a class but don't know the FQN | pass the short name (`taom-src path Formation`); an ambiguous one lists the full names to pick from |
| Need to browse a whole namespace tree | `ls E:/Decompiled_Bannerlord/<area>/` (the dump is fine for browsing patterns) |
| Need full type list of a DLL | `mcp__ilspy__list_types` (the ILSpy MCP is purpose-built for this) |

## Why this exists

The recurring failure mode (caught by Codex review 2026-05-06): two agents disagreed on a TaleWorlds API; the more confident one was wrong because it inferred from the decompile folder rather than the installed DLLs. A single primitive that always lands on the installed engine version: and caches so the cost is paid once per type: removes the temptation to skip verification.

## Environment requirements

- `$env:BANNERLORD_GAME_DIR` set to the Bannerlord install root (e.g. `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`).
- `ilspycmd` on PATH (`dotnet tool install -g ilspycmd`).

Both are reported with actionable error messages if missing (per `.claude/rules/environment-failures.md`).

## Related

- `.claude/skills/research/SKILL.md` — broader "decompile and analyze before implementing" workflow. Use that for full analysis sessions; use `taom-src` for one-shot signature lookups.
- `CLAUDE.md` "TaleWorlds Research — Lookup Order" — high-level routing.
