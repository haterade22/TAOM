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
| `balance` | A hash of the three JSON files: join `fortresses.json`, `waves.json` and `shop.json`, in that order, with `\n\0\n`, after changing CRLF to LF. Take SHA-256 of the UTF-8 bytes, and write its first 8 bytes in lower hex | Must match that release, and picks the board |
| `cheatMode` | `NativeConfig.CheatMode` at launch | Must be false |
| `mods` | Active modules, as `<id> <version>`. These versions are the only record of the engine changeset, the Armory and `TAOM_Map` | Shown to moderators |
| `autoplay`, `garrisonScale`, `targetWaves` | The run's options | Must be false, 1 and null: anything else is a test run |
| `startedUtc`, `endedUtc` | Real time | The wave times must fit between them |
| `final` (always false on a board run, so never required), `reason`, `wave`, `wavesCleared`, `kills`, `fled`, `fleeingPeak`, `playerDeaths`, `wavesFelled` | The result | The score and the rules below |
| `waves[]` | Per wave: `wave`, `kills`, `felled`, `lost`, `seconds` | The rules below |
| `gold` | `start`, `earned`, `spent`, `balance`, `bounty`, `waveBonus`, `earlyStart` | The rules below |

## The server's rules

The bot holds, for each released build, its commit, its `balance` and copies of all three JSON files, and it recomputes `balance` to prove those copies. The first rule that fails refuses the report and names itself. A rule marked **flag** does not refuse: it sends the entry to a moderator.

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
13. The rows' seconds fit between `startedUtc` and `endedUtc` within a small tolerance, and the run ended no later than the upload plus a day, because `endedUtc` is the player's own clock. A scoreboard view in this mission would add fast-forward and break this rule.
14. The file's SHA-256 is new, and so is its `fortress`, `startedUtc` and `endedUtc` together, because one added space gives a new hash.

**What these rules cannot see:**

- `waves[].lost`, the second rank key: zeroing it passes every rule, so only a clip of the end panel can prove it.
- Cheat mode switched on after launch: the field is read once, at launch. Nobody has tested whether a player can do that mid-mission. A latch read per tick would close it, and it is Mike's call as a guard for a case nobody has seen.

## The boards

- **One board per fortress and per `balance`:** a change to the three JSON files, even to a display name, opens a new board and keeps the old one readable. A release that changes troops, gear, the scene or AI code with the same files needs a board opened by hand, because the hash cannot see those.
- **Rank:** `wavesCleared`, then fewest defenders lost, summed over `waves[].lost`. `kills` is a weak tiebreak, because the wave table nearly fixes it. The game's own "Best run" line ranks waves, then kills, so it can disagree with the board.
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
5. The first release with the mode gives the bot its commit, its `balance` and its three files, read from the packaged install, and the board opens.
