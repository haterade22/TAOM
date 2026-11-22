# Training Grounds

## Overview

Since #1527 the town arena reads as the Training Grounds in English and its keeper as the Training Master. Ids, scenes and the
tournament itself are unchanged; only the English words differ. Two features sit on top of that place: a team version of the
practice fight in the towns of the Free Peoples, and a wave fight with wagers at the Training Master. Code for both lives under
`Main/Features/TrainingGrounds/`.

- [arena.md](arena.md): the name, the English rows that carry it, and the gate that keeps the old word out of TAOM's English.
- [team-practice.md](team-practice.md) (#1528): the practice fight as a team fight, the player and two allies against four.
- [training-waves.md](training-waves.md) (#1529): waves of the town's fighters against the player, with a wager before each wave.

## How to verify in game

This page has no behavior of its own. Each feature page above lists its own in-game checks: `tools/ingame_checks/1528.toml` and
`tools/ingame_checks/1529.toml` hold the matching manifests.

## GitHub Issue

- **Issues:** #1527 (the English names), #1528 (team practice), #1529 (wave fights)
- **Status:** see each feature page above
