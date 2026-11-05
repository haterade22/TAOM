# The Long Defeat leaderboard: plan

Issue: [#1080](https://github.com/haterade22/TAOM/issues/1080). Mode: [long-defeat.md](../long-defeat.md). The report fields below are built. The bot and its page are not, and they need accounts only Mike can create.

## What it can and cannot do

No check can prove a score from this mod. The repo is public, so a signing key in `TAOM.dll` would be public too, and a server cannot replay a battle. The rules below catch mistakes and hand edits. A determined cheat can still rebuild a consistent file with a script, so a top 10 entry also needs a clip, and moderators can remove any entry.

A report signature was set aside for the same reason: its key would be public, and the rules already catch casual edits.

## The flow

1. The game writes `long_defeat_report_<log stamp>_ld<N>.json` in `bin\Win64_Shipping_Client\Logs` when a run ends. The newest 20 stay.
2. The player runs `/submit` in the TAOM Discord and attaches that file.
3. The bot checks it, stores the result, and replies with the rank or the first rule it failed.
4. The bot updates a leaderboard channel and a web page.

The mod stays offline: TAOM has no network code, and this adds none. A direct upload from the game can come later as an option, with a consent prompt.

**The end panel helps both:** it names the report's file, without its folder, because a Steam library under a user folder would show a Windows user name in a public clip. It also shows the defenders lost in cleared waves, because that is the second rank key and no rule can check it.

## The report, schema 1

`LongDefeatRunReport` writes these. The report holds no player name and no machine data.

| Field | Meaning | The server's use |
|---|---|---|
| `schema` | 1 | Picks the rule set |
| `fortress` | Fortress id | Picks the board |
| `build` | The informational version of `TAOM.dll`, with its commit | Must be a released build |
| `balance` | A hash of the six JSON files (schema 1 hashed the first three): join `fortresses.json`, `waves.json`, `shop.json`, `affixes.json`, `boons.json` and `classes.json`, in that order, with `\n\0\n`, after changing CRLF to LF. Take SHA-256 of the UTF-8 bytes, and write its first 8 bytes in lower hex | Must match that release, and picks the board |
| `cheatMode` | `NativeConfig.CheatMode` at launch | Must be false |
| `mods` | Active modules, as `<id> <version>`. These versions are the only record of the engine changeset, the Armory and `TAOM_Map` | Shown to moderators |
| `autoplay`, `garrisonScale`, `targetWaves` | The run's options | Must be false, 1 and null: anything else is a test run |
| `maxSpeed` | The highest mission speed the run used: 1, 2, 4 or 10. It is added to schema 1 without a bump, because no released build writes schema 1 yet. A report with no `maxSpeed` predates the field and reads as 1 | Rule 13 scales by it. A run at any speed counts on the board, see "The boards" |
| `startedUtc`, `endedUtc` | Real time | The wave times must fit between them, scaled by `maxSpeed` |
| `final` (always false on a board run, so never required), `reason`, `wave`, `wavesCleared`, `kills`, `fled`, `fleeingPeak`, `playerDeaths`, `wavesFelled` | The result | The score and the rules below |
| `waves[]` | Per wave: `wave`, `kills`, `felled`, `lost`, `seconds` | The rules below |
| `gold` | `start`, `earned`, `spent`, `balance`, `bounty`, `waveBonus`, `earlyStart` | The rules below |

## The server's rules

The bot holds, for each released build, its commit, its `balance` and copies of all six JSON files, and it recomputes `balance` to prove those copies. The first rule that fails refuses the report and names itself. A rule marked **flag** does not refuse: it sends the entry to a moderator.

All six reports on record pass rules 4 to 12, each against its own table. All six are autoplay runs, so rule 3 refuses them, and a passing fixture for the bot needs a hand-played run.

1. The file is at most 64 KB and parses as JSON with schema 1. The bot never runs anything from it and stores only its known fields.
2. The commit after `+` in `build` is a released commit, with no `.dirty` or `.nogit`, and `balance` is that release's hash. `build` also holds a time stamp per build, so the whole string never matches. Read both values from a report made on the packaged install, because a later deploying build replaces the files Mike packages from.
3. `cheatMode` is false, `autoplay` is false, `garrisonScale` is 1 and `targetWaves` is null.
4. `waves` holds `wavesCleared` rows, numbered from 1, and `wave` is `wavesCleared` or `wavesCleared + 1`. A run can end between waves, for any reason.
5. For each row, `kills + felled` equals that wave's count from the roster that `fortresses.json` names: the authored count, then the last count plus `countPerWave`, capped at `maxCount`. All 19 rows on record hold this. **Flag**, not refuse, when `fled` or `fleeingPeak` is above 0, because a routed attacker counts as no kill.
6. For each row, `felled` is at most `stragglerShare` of the count, because the run fells attackers only when at most that share of the wave is left (`StalemateRule`).
7. `kills` at the top is at least the sum of the rows' kills, and at most that sum plus the next wave's count.
8. `gold.start` is the fortress's `startingGold`, `gold.start + gold.earned - gold.spent` equals `gold.balance`, and `gold.earned` equals the sum of its three sources.
9. `gold.waveBonus` equals the sum over cleared waves of `waveBonusBase + waveBonusPerWave * wave`.
10. `gold.earlyStart` is at most `earlyGoldCap * wave`.
11. `gold.bounty` lies between `kills` times the lowest bounty in the waves played and `kills` times the highest. The waves played include the wave in progress, past the authored list they use its last mix, and a troop with no bounty pays 0. A report with no waves skips this rule.
12. **Flag** a row whose `seconds` is under 120. The 19 cleared waves on record took 334 to 664 s, the first loss came 51 to 80 s into a wave, and 10 percent of a wave was left at 223 s at the earliest. All of that is autoplay and predates the straggler rule, so this stays a flag until human runs exist.
13. The sum of the rows' seconds is at most `(endedUtc - startedUtc) * maxSpeed` within a small tolerance, and the run ended no later than the upload plus a day, because `endedUtc` is the player's own clock. The rows' seconds are mission time, and the mission has its own speed control (1x, 2x, 4x, 10x), so at speed above 1 they exceed the wall time by up to that factor. `maxSpeed` is the player's own claim and only the clip of the end panel checks it: a report that says 1 and sums to more than the wall time fails, one that says 10 passes any sum up to ten times the wall time. A missing `maxSpeed` is 1.
14. The file's SHA-256 is new, and so is its `fortress`, `startedUtc` and `endedUtc` together, because one added space gives a new hash.

**What these rules cannot see:**

- `waves[].lost`, the second rank key: zeroing it passes every rule, so only a clip of the end panel can prove it.
- Cheat mode switched on after launch: the field is read once, at launch. Nobody has tested whether a player can do that mid-mission. A latch read per tick would close it, and it is Mike's call as a guard for a case nobody has seen.

## Schema 2 (Phase 2)

Phase 2 changes the arithmetic of six schema 1 rules, so its reports carry `schema` 2 ([phase2-plan.md](phase2-plan.md), section 7). Every schema 1 field keeps its name, type and meaning, so the end panel and rules 4 to 7 read a schema 2 file as written, and `waves[].kills`, `waves[].felled` and the top-level `kills` count roster soldiers only. A schema 1 reader refuses the file at its rule 1, which names itself. A schema 2 reader is a schema 1 reader plus the amendments below, in one code path. Slice 1 writes the fields at their empty values; each later slice fills its own and adds its rule.

**New fields:** `seed` (the run's seed, a whole number from 0 to 2^53-1 (9007199254740991): the cap keeps every digit when a reader parses JSON numbers as doubles, as a JavaScript bot does, and rounds nothing), `class` (the class id played, from Slice 5: one of the ids in `classes.json` that has a kit for the fortress's defender culture, empty only when none is on offer. A person picks it in the second setup question; an unattended run takes the one armed, or the draw at stream 3 `(0, 0)` over the classes on offer, so a bot run's class replays from its seed), `startWave` and `startRerolls` (test options, 1 and 0 on a board run), `boons[]` (per pick: `pick`, `wave` offered, `offered`, `taken`, `takenBeforeWave`), `rerolls` (`earned`, `used`, `banked`), per wave `affixes`, `healthFactor`, `damageFactor`, `summoned`, `summonSpawned`, `summonKills`, `summonFelled` and `commander` (null, or `troop`, `rank`, `steps`, `outcome`, `uses`, `usesAfterEnd`), `gold.commander`, `gold.boon`, and `poison` (`doses`, `hits`, `damage`, `ownerMissing`, `withheld`).

**Amended rules:**

- Rule 2: `balance` joins six files in this order with the same separator and line-ending rule: `fortresses.json`, `waves.json`, `shop.json`, `affixes.json`, `boons.json`, `classes.json`. A missing file joins as empty text.
- Rule 3: also `startWave` is 1 and `startRerolls` is 0, and `seed` is a whole number within the cap.
- Rule 5, from Slice 2: the count line stays as written, and each row's `healthFactor` and `damageFactor` are 1 inside the authored list and `min(maxFactor, 1 + perWave * extra)` past it, with `extra` the waves past the last authored one and the three numbers from the roster's `scaling` in `waves.json`. The game stores each factor as a 32-bit float, so a reader compares with a relative tolerance of 1e-5, never exactly: a double recomputation of 1 + 0.05 * 14 gives 1.7000000000000002 against the report's 1.7.
- Rule 6, from Slice 7: `felled` is at most `stragglerShare` of the count plus `summoned`, because the stalemate rule counts the wave's summons in the wave it takes the share of (`SurvivalRun.RunWave` passes the roster plus the summons queued). `felled` stays roster soldiers only.
- Rule 8: `gold.earned` equals the sum of five sources (the three above, `commander` and `boon`). From Slice 4 `gold.boon` is the sum of the `gold` effects of the boons taken.
- Rule 9: each wave bonus is multiplied by the wave-bonus factor in force at that clear, recomputed from `boons[]`. The factor is `max(0, 1 + sum / 100)` over every `waveBonusPercent` of the boons taken before that clear, their drawbacks included, and the bonus is `round(base * factor)` with halves away from zero.
- Rule 10: the cap is multiplied by the horn factor, `max(0, 1 + sum / 100)` over `earlyGoldPercent`; `EarlyGoldCap` and the per-second rate are both multiplied.
- Rule 11: the band is taken over roster and summon kills, over the tier-substituted mixes and the summon mixes of the waves played, times the bounty factor in force (`bountyPercent`, the same sum).

**New rules** (each lands with its slice):

- Rule 15: replay `seed` with the derivation below. Each row's `affixes` is the prefix of that wave's roll, its length from the boons taken before the wave began. Each pick's `offered` equals the draws given the boons taken before it, `taken` is one of the last three offered, and `rerolls.used` is at most `earned`, with the bank capped at 3. The boon half is live from Slice 4: `boons[]` lists each pick in order (`pick` from 1, `wave` the clear that offered it, `offered` one list per draw with the first draw 0 and each reroll after it, `taken` null when the run ended with the pick open, `takenBeforeWave` the wave number that starts next, 0 when nothing was taken), the draws replay from stream 2 at `(pick, draw)` over the eligible rows in `boons.json` file order (class rows for the report's `class`, then war chest, fortress and Legend; a boon taken before is out), each card is `Below(total weight)` over weights of 10 and `legendWeight` for a Legend row, a reroll leaves out the cards of the draw before it unless fewer than three others remain, `taken` is a card of the last draw, only the last pick may be left open, at most `wavesCleared / 3` picks exist, `rerolls.used` is the number of draws after each pick's first, `rerolls.banked` is `earned - used` and at most 3, and `earned` is at most the start bank plus `wavesCleared / 5`. A Legend boon's `affixCount` shortens the prefix of the roll from `takenBeforeWave` on. The affix half is live from Slice 3: a row's `affixes` holds the power ids the wave carried (the whole roll, until a boon can cut it), the roll is the partial Fisher-Yates over the eight ids in the fixed order `lash, drums, eye, discipline, hide, hammer, terror, blades` (for i from 0, `j = i + Below(8 - i)`, swap; as many draws as the schedule gives the wave), drawn from stream 1 at `(wave, 0)`, and the schedule is the `schedule` of `affixes.json`. Its vectors are pinned in `AffixPlanTests.cs` and `tools/tests/test_run_long_defeat.py`.
- Rule 16: `waves[].commander` is present exactly at multiples of the roster's `every`, with the configured troop and rank.
- Rule 17: `summonKills + summonFelled == summoned` on every cleared row.
- Rule 18 (**flag**): `poison.ownerMissing` is 0.

**The derivation of a draw:** the game only ever arms or takes a seed up to the cap (a clock seed is masked into it), but `RunRandom.For(seed, stream, a, b)` itself accepts any 64-bit value, so its pinned vectors include 2^63-1. It starts SplitMix64 at `seed ^ (stream * 0x9E3779B97F4A7C15) ^ (a * 0xBF58476D1CE4E5B9) ^ (b * 0x94D049BB133111EB)`, all unchecked 64-bit, and `Below(n)` is `next() % n`. Streams: affixes `(1, wave, 0)`, boon offers `(2, pick, draw)`, the bot's class `(3, 0, 0)`. No floating point is used in a draw. The vectors in `RunRandomTests.cs` and `tools/tests/test_run_long_defeat.py` are the same rows, so the two implementations cannot drift apart while both suites pass.

**What these rules cannot see, added:** whether the player picked the card the report names (the end-panel clip shows the boons), and the Cavalry remount, which the rules cannot see at all.

## The boards

- **One board per fortress and per `balance`:** a change to any of the six JSON files, even to a display name, opens a new board and keeps the old one readable. A release that changes troops, gear, the scene or AI code with the same files needs a board opened by hand, because the hash cannot see those.
- **Rank:** `wavesCleared`, then fewest defenders lost, summed over `waves[].lost`. `kills` is a weak tiebreak, because the wave table nearly fixes it. The game's own "Best run" line ranks waves, then kills, so it can disagree with the board.
- **A run at any speed counts (Mike, 2026-11-04).** `maxSpeed` above 1 needs no separate board, flag or refusal. The report still records it, so a board can show it. Four unattended runs at 10x cleared 5 waves once, against three of three at 1x, and Mike accepted that 10x may play harder.
- **One entry per Discord account per board:** the best one shows.
- **The top 10:** an entry shows as unverified until a moderator accepts a clip link. A clip must show the end panel of that run, with its waves, kills and defenders lost.
- **Opening:** with the first release that holds the mode, once its balance settles. No released build exists yet, so rule 2 cannot pass today.

## Hosting and safety

A small separate repo holds the bot, because it is not part of the mod. A Discord application's interaction endpoint on a small hosted worker with a small database fits this. Cloudflare Workers with D1 is the first choice, but nobody has checked its current limits and prices.

- **Untrusted input:** every upload is a stranger's file. The bot checks size and shape first, keeps only known fields, escapes every shown string, and limits uploads per account.
- **Secrets:** the Discord key and the host's token live in the host's secret store, and never in a repo.
- **Stored data:** the Discord account id, the report's known fields, the file hash and any clip link. A player can ask for removal.
- **Moderators:** a slash command removes an entry or bans an account. Each action is logged.

## What Mike provides

1. A Discord application added to the TAOM server, and the channel for the board.
2. A hosting account.
3. Who may moderate.

## Steps

1. Report fields in the mod (built, proved by unit tests; an unattended run owes the proof).
2. The end panel names the report file and the defenders lost, with their `/localize` rows (built; no game run has shown them yet, and their translations are owed in #763).
3. The bot and its tests: each rule gets a real report as its fixture and one edited copy that the rule must refuse.
4. A dry run in a private channel: one person submits a real report, then an edited one.
5. The first release with the mode gives the bot its commit, its `balance` and its six files, read from the packaged install, and the board opens.
