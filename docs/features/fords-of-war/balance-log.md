# Fords of War balance log

What each match showed, and what changed because of it. `python tools/fords_match_review.py <run folder>` gives the numbers; a person reads them for the design. One match proves little, so a change here names the matches behind it, and the same pair runs again after the change.

**How to read the numbers:** "gold trade" is the gold value a side's troops killed divided by the value they lost. It counts free units at their value, so it measures fighting, not spending. All matches are AI against AI on `taom_fords_01_forceatmo`, 900 live seconds, no keep fell.

## Matches

| Run | Build | West v East | Units lost | Towers lost | Tower damage dealt | Gold trade | Notes |
|---|---|---|---|---|---|---|---|
| 20261103-101036 | `5e4871724` | Harad v Rohan | 743 / 577 | 3 / 1 | 13,922 / 23,145 | no balance lines yet | One mumak, 2 elephants. Harad fielded no hero. Modding Kit open: 1,550 shader compiles. |
| 20261103-103023 | `5e4871724` | Rivendell v Mordor | 199 / 2,027 | 1 / 6 | 57k / 8k | no balance lines yet | Mike's report: "Rivendell is absolutely slapping mordor". |
| 20261103-104707 | `5e4871724` | Erebor v Gundabad | 555 / 1,048 | 4 / 1 | not read | no balance lines yet | The game crashed at 810 s beside the Kit. |
| 20261103-113154 | `826e77aef` | Rivendell v Mordor | 154 / 1,485 | 2 / 0 | 2,987 / 23,300 | Rivendell 3.2 | After the first pass. |
| 20261103-115341 | `826e77aef` | Mordor v Rivendell | 1,496 / 147 | 2 / 2 | 24,518 / 21,686 | Rivendell 3.4 | The same pair with the sides changed. |

## Findings and changes

### Rivendell v Mordor, first pass (`d5500dd9a`)

**Seen:** Rivendell lost 199 units to Mordor's 2,027 and took 6 towers to 1. Mordor's free orcs were level 1, and the engine's armour formula takes a weak blow close to nothing.
**Changed:** Mordor's free waves and `orc_rabble` start at level 11. Rivendell's free waves are 3 units every 60 seconds, not 5 every 40, and its elite packs cost a quarter more.
**Result:** Mordor now wins tower damage or draws it (2 towers lost each in the second match), while Rivendell still wins the fighting by about 3 to 1 in gold value. That split suits the two factions' design, so neither number is a target by itself.

### The AI spends its gold badly (this change)

**Seen in both Rivendell and Mordor matches:**

- Mordor spent 5,925 and 5,505 gold. The creatures it lost were worth 4,766 and 4,482, and they killed 442 and 515. Mordor bought no upgrade and no hero in either match. Cause in the code: `AiCommander.CallCreature` ran before the buy cycle at every decision with the timer ready, and took whatever gold there was.
- Rivendell ended one match with 1,854 gold unspent. Cause in the code: the AI bought nothing, upgrades included, while its assault lane held the threshold, and a small faction's threshold is low.
- Heroes almost never come. Rivendell fielded one at 682 s and at 810 s. Mordor, Harad, Erebor and Gundabad fielded none. Cause in the code: the AI fields a hero only when its gold beats the price plus a reserve of 300, and the buy cycle spends the gold first.
- The one fielded Rivendell hero, `lord_R1_3`, made 1 kill in about 220 s and 3 kills in about 90 s, for 1,000 gold. Too little data to judge heroes yet.

**Changed:**

- A `hero` entry in every faction's `aiBuyCycle` makes the AI save for its cheapest hero.
- The AI calls a creature only from a `beast:` entry. Mordor and Dol Guldur now name theirs, as Harad did. The automatic call is gone.
- Upgrades, beasts and heroes in the cycle no longer wait for a free place in the assault lane.
- Mordor's creature prices drop: wargs 2.0 to 1.5, hill troll 4.0 to 2.5, cave troll 5.0 to 3.0 `eliteWeight`.
- The balance line now says where each side's gold came from and what it paid for.

**To check on the next Rivendell and Mordor match:** Mordor buys upgrades and uruks, both sides field heroes, Rivendell's unspent gold falls, and the creature trade improves.

## Open questions

- **Swordguard:** 725 and 850 kills for 47 and 34 deaths. It may be simply the elite answer to level 11 orcs. Read it against Erebor and Gondor before any change.
- **Horde feeds bounty:** Rivendell earned 7,286 and 8,099 gold, of which passive income gives about 1,800. A horde's deaths pay its enemy. The new gold words will give the bounty share.
- **Creature fighting value:** a troll made 0 to 4 kills in each match. Lower prices answer the cost; they do not say why trolls kill so little.
