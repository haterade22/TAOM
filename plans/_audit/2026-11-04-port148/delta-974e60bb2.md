# 1.5.x delta 4d1a9105a..974e60bb2

WP145 covers the 1.5.x commits up to `4d1a9105a`. This records the classification of the commits after it, done on 2026-11-05.

| Measure | Count |
|---|---|
| Commits in the range | 45 |
| Non-merge | 37 |
| Merges | 8 |
| Code ports | 0 |
| Docs facts carried | 2 |
| Skipped as unverified on 1.4.8 | 1 |

## Docs facts carried

Both were re-checked on the installed v1.4.8 and written by hand for this line, with no mention of features it lacks.

1. **`GameNetwork.IsSessionActive` reads false in a co-op battle.** It answers from `MBCommon.CurrentGameType` (`GameNetwork.cs:381-391`), and BannerlordCoop's source has no `CurrentGameType` (read at `4ad976c8c`). Carried to `docs/reviews/lessons/adapters-taleworlds-api.md` and `docs/features/coop-interop.md`.
2. **The mission clock has three multipliers in `MissionState.TickMission`:** the fixed step (`:155-158`), the time speed skipped in a network session (`:159-164`), and fast forward's `num * 9f` (`:173`) before the frame (`:196`). Carried to `docs/reference/engine/mission-frame-threads-and-native-costs.md`.

## Skipped

- **The crash uploader deleting the engine's dump** (`974e60bb2`, an unattended-run fix). Whether v1.4.8's uploader deletes the `crashes` folder the same way was not verified, so nothing is carried.

## Noted, not acted on

- `Main/Features/DreadAura/Hooks/DreadMissionGate.cs:38` and `Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs:33` read only `GameNetwork.IsSessionActive`. Co-op status: read from source for BannerlordCoop only: Dread is owner-gated by DreadAgentGate, MountDespawn and BattleCorpses are per-peer presentation; Bannerlord Together and a two-peer run unverified; no action under D7.
- The 1.5.x Spacing Repair in Marshal's Word is a possible future work package. It is not ported.
