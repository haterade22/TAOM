# Adopting yotthani's engine research for TAOM's performance (2026-10-02)

Written for: TAOM maintainers deciding what to take from yotthani's engine knowledge base for TAOM's
own performance and memory work. Procedure: `/adopt-external`
([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)), third pass on MithrilForge after
[adopt-mithrilforge-2026-09-29.md](adopt-mithrilforge-2026-09-29.md), whose claims are not repeated here.
The programme this pass feeds is recorded in `plans/_audit/2026-10-02-perf/`.

## What was read

| Source | Commit | Read |
|---|---|---|
| `yotthani/MithrilForge` (private; the maintainer's account can read it) | `7c556554`, 2026-10-02 | `docs/engine/` in full (README, native.md, hooks.md, ai-formations.md in German, combat.md, taom-combat.md, rider-ik.md, modding-kit.md), `docs/anim-findings.md`, the README's `TpacFormat` sections, `src/MithrilForge.Anim/ClipLoading.cs`; a local clone in the session scratchpad |
| `yotthani/bannerlord` (private) | `2e44db7`, 2026-10-02 | `HoN/DualWield/Core/DwPerf.cs`, `bn faces/FaceLearner/PERFORMANCE_ANALYSIS.md`, the DualWield perf and limb-ray commit messages (`db97bc5e`, `6bf89e75`, `29795cc7`, `e0186626` and the 2026-09-28/29 series), two design specs; a sparse local clone |
| Not available | | MithrilForge `docs/perf-audit/` (`limb-ray-given-agent.md`, `clip-reader-lock.md`) and the `feat/perf-audit` branch: cited by yotthani's commits, on no pushed branch |

## Security

- **Safe to learn from:** yes; documents and source were read, nothing was built or run.
- **Licensing:** MithrilForge is MIT; `yotthani/bannerlord` has no licence file (`UNKNOWN`). Nothing was
  copied: facts are restated in TAOM's words, and every native fact TAOM relies on was re-derived on
  TAOM's own copy of the client. Register rows updated in
  [provenance-register.md](../reference/provenance-register.md).

## What TAOM re-derived, and what it found that the sources do not say

Checked with `tools/native_decompile.py`, `tools/native_sig_author.py` and the v1.5.3 managed decompile on
2026-10-02; the full write-up is
[mission-frame-threads-and-native-costs.md](../reference/engine/mission-frame-threads-and-native-costs.md).

| # | Claim | Source | Result |
|---|---|---|---|
| 1 | The native agent cap is 2,040 (`MOV EAX,0x7F8`) | yotthani native.md | **Confirmed**: `IMBAgent.GetMaximumNumberOfAgents` at `0x6E4FA0` returns `0x7F8` |
| 2 | `GetNearbyAgents` returns active humans only | yotthani combat.md (measured) | **Confirmed and sharpened**: the native filter is `AgentFlag.IsHumanoid` plus `AgentState.Active`; also 40 per call under a global lock, and a grid only up to 15 m |
| 3 | Clips at Loading Type 1 or 2 take a per-bone reader lock and block the sampling worker | yotthani, citing an unpublished doc | **Confirmed in mechanism**: compare-and-swap on a per-clip reader count, a condition-variable wait until loaded, an atomic release per access |
| 4 | (not in the sources) on-demand clip data is evicted past a fixed budget | TAOM | **New**: 12 MiB, a float read by the eviction pass only (`0xB2E2DC`); clips nobody is reading are freed and reloaded on next use |
| 5 | (not in the sources) the process timer resolution, the frame order and the worker cap | TAOM | **New**: one `timeBeginPeriod(1)` at start-up, `OnMissionTick` before the async agent tick starts, a `/maxThreadCount` argument |
| 6 | Bannerlord runs on Mono | TAOM's own toolchain doc | **Refuted**: the Steam game runs on the .NET Framework CLR; Mono serves only the native-hosted entry. The toolchain doc is corrected |
| 7 | Battle size: menu array, `/2` per mounted soldier, the native cap | yotthani combat.md | **Confirmed** in the v1.5.3 decompile by this run's audit |
| 8 | Kit texture formats by suffix, texture flags, visible distance, particle config, edit data in packages | yotthani modding-kit.md | **Not re-derived**: tagged as yotthani's in the reference page |
| 9 | Limb-ray costs and one-agent semantics | yotthani combat.md and commits | **Not re-derived**; TAOM casts no limb rays today |

## Where it went

| Destination | What |
|---|---|
| [mission-frame-threads-and-native-costs.md](../reference/engine/mission-frame-threads-and-native-costs.md) (new) | Every fact above, tagged TAOM-verified or yotthani |
| [bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md) | The Mono claim corrected |
| `plans/_audit/2026-10-02-perf/` | The performance programme's report, decisions and plans 028 to 037 |

## Recommendation

The method is the part worth keeping: per-subsystem cost attribution (DualWield's `DwPerf`), shadow-mode
A/B before switching a cheaper path live, and native verification of every engine claim. TAOM builds its
own: the mission tick profiler (plan 028), the log comparison tool (plan 029) and the clip memory probe
(plan 036). Nothing is installed or run from either repository. Ask yotthani for the unpushed
`docs/perf-audit/` notes and a licence line for `bannerlord`.
