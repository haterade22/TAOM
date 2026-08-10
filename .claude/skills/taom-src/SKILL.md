---
name: taom-src
description: Use before patching, overriding, or adapting a TaleWorlds type — `taom-src path <Type>` decompiles + caches the installed engine, returning an absolute path.
argument-hint: [path|list|remove|clean] <FullyQualifiedType>
---

# taom-src — one-command TaleWorlds source lookup

Wraps `ilspycmd` against the **installed DLLs** (auto-detects the engine version from `Version.xml` — currently v1.4.8) and caches at `~/.taom-src/<version>/`. Inspired by [vercel-labs/opensrc](https://github.com/vercel-labs/opensrc). Use this **before** every `[HarmonyPatch]`, `GameModel` override, or adapter that touches a TaleWorlds type — never guess signatures from the `E:\Decompiled_Bannerlord\` dump (it can lag the installed engine after a bump).

## Core pattern

```bash
# Decompile + cache (first call) or print cached path (subsequent calls)
pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel

# Compose with grep/cat/rg — exactly like opensrc
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
cat $(pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Formation)
```

Progress goes to stderr, path goes to stdout — safe in `$(...)`.

## Subcommands

| Command | Behavior |
|---|---|
| `path <Type>` | Print absolute path to cached `.cs`. Decompiles on miss. |
| `path <Type> -Dll <Name>` | Skip namespace heuristic; force a specific DLL. |
| `list` | Show all cached types as a table. `--json` for raw output. |
| `remove <Type>` | Delete one cache entry. |
| `clean` | Nuke the entire cache. |

## How DLL resolution works (on cache miss)

1. **DLL index cache** — `~/.taom-src/dll-index.json` remembers `type → dll` from prior lookups. Instant hit.
2. **Namespace heuristic** — `TaleWorlds.CampaignSystem.X.Y` probes `TaleWorlds.CampaignSystem.X.dll`, then `TaleWorlds.CampaignSystem.dll`, then `TaleWorlds.dll`. Catches ~99% of cases on the first probe.
3. **Brute-force iteration** — Last resort: probes every `*.dll` across all search dirs, alphabetically within each.

**Search dirs (all three steps):** the primary `bin/Win64_Shipping_Client` first, then every
`Modules/<Name>/bin/Win64_Shipping_Client` that holds DLLs — modules shipping `TaleWorlds.*`
assemblies before third-party ones. **This is not optional trivia:** several engine assemblies ship
ONLY under a module. `TaleWorlds.MountAndBlade.View.dll` — which owns `CharacterTableau`,
`BasicCharacterTableau` and `AgentVisuals`, i.e. the whole tableau/encyclopedia render path — lives in
`Modules/Native/bin/` and is absent from `bin/` entirely. Likewise `SandBox.*` types live in
`Modules/SandBox/bin/`. Before 2026-08-06 the tool searched `bin/` only and threw "not found in any
DLL" for all of them.

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
| Looking for a class but don't know the FQN | `grep -r "ClassName" E:/Decompiled_Bannerlord/` to find namespace, **then** `taom-src path` to verify against the installed engine |
| Need to browse a whole namespace tree | `ls E:/Decompiled_Bannerlord/<area>/` (the dump is fine for browsing patterns) |
| Need full type list of a DLL | `mcp__ilspy__list_types` (the ILSpy MCP is purpose-built for this) |

## Why this exists

The recurring failure mode (caught by Codex review 2026-05-06, see `feedback_codex_caught_api_misread.md` in memory): two agents disagreed on a TaleWorlds API; the more confident one was wrong because it inferred from the decompile folder rather than the installed DLLs. A single primitive that always lands on the installed engine version — and caches so the cost is paid once per type — removes the temptation to skip verification.

## Environment requirements

- `$env:BANNERLORD_GAME_DIR` set to the Bannerlord install root (e.g. `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`).
- `ilspycmd` on PATH (`dotnet tool install -g ilspycmd`).

Both are reported with actionable error messages if missing (per `.claude/rules/environment-failures.md`).

## Related

- `.claude/skills/research/SKILL.md` — broader "decompile and analyze before implementing" workflow. Use that for full analysis sessions; use `taom-src` for one-shot signature lookups.
- `CLAUDE.md` "TaleWorlds Research — Lookup Order" — high-level routing.
