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
| 20261103-133112 | `0c4cf8b48` | Rivendell v Mordor | 157 / 1,471 | 2 / 1 | 10,283 / 25,898 | Rivendell 3.5 | 1x. Free heroes and the new AI buy cycle. |
| 20261103-165555 | `f3653f9bb` | Rivendell v Mordor | 140 / 1,332 | 4 / 0 | 1,033 / 38,138 | Rivendell 1.6 | 10x. |
| 20261103-165814 | `f3653f9bb` | Rivendell v Mordor | 170 / 1,327 | 5 / 2 | 20,129 / 46,583 | Rivendell 1.9 | 10x. |
| 20261103-170032 | `f3653f9bb` | Rivendell v Mordor | 146 / 1,544 | 2 / 1 | 16,113 / 21,440 | Rivendell 2.7 | 4x. |
| 20261103-190524 | `fd36dcb85` | Rivendell v Mordor | 148 / 1,512 | 3 / 1 | 18,145 / 34,866 | Rivendell 3.1 | 1x, except 10x from 45 to 99 s after a press of the speed control. Builds ran beside it: frames averaged 9.4 ms. |
| 20261103-192127 | `fd36dcb85` | Rivendell v Mordor | 155 / 1,549 | 2 / 2 | 17,935 / 20,924 | Rivendell 3.1 | 4x. |

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

- Each side's next hero now comes free every 5 minutes (Mike's rule, `heroes.freeArrivalSeconds`). An hour earlier a `hero` entry in `aiBuyCycle` made the AI save for one; the free arrival replaced it, because saving took gold from units and a bought hero took the slot.
- The AI calls a creature only from a `beast:` entry. Mordor and Dol Guldur now name theirs, as Harad did. The automatic call is gone.
- Upgrades and beasts in the cycle no longer wait for a free place in the assault lane.
- Mordor's creature prices drop: wargs 2.0 to 1.5, hill troll 4.0 to 2.5, cave troll 5.0 to 3.0 `eliteWeight`.
- The balance line now says where each side's gold came from and what it paid for.

**To check on the next Rivendell and Mordor match:** Mordor buys upgrades and uruks, both sides field heroes, Rivendell's unspent gold falls, and the creature trade improves.

### Which speed gives balance numbers

**Seen:** the same pair and rules ran twice at each of 1x, 4x and 10x (the table above). The first 1x match ran one build before the others; the only Fords code between them is the speed itself. 10x fell outside the 1x range in both matches: Mordor lost about 1,330 units against 1,471 and 1,512, Rivendell lost 4 and 5 towers against 2 and 3, and Rivendell's gold trade was 1.6 and 1.9 against 3.5 and 3.1. 10x also changed what the AI bought: 99 and 135 swordguards against 65 and 84 at 1x, and I read that as more lanes under threat, since a threatened lane buys the cheapest infantry pack. 4x stayed close: 1,544 and 1,549 units, 2 towers lost each time, gold trade 2.7 and 3.1. Mordor's tower damage at 4x read low, about 21,000 against 25,898 and 34,866.

**Decision:** balance runs use 4x, about 246 s of wall time against 928 s at 1x. A change that matters is confirmed at 1x, and tower damage is not judged from 4x alone. 10x is for smoke and crash runs, about 109 s; its numbers are not balance evidence. The cause of the 10x difference is not known. The engine's fast forward runs the AI on the main thread in steps of up to 0.1 s, but no test ties that to the extra tower contact.

### Heroes and the AI's spare gold (this change)

**Seen:**

- Heroes do not earn their price. In the three matches read with the new hero table, a hero made 0 to 2 kills per minute on the field. Glorfindel made none in 300 s. A swordguard costs about 43 gold and made about 12 kills per unit in `133112` (785 kills, 65 spawned), so a hero of 1,000 to 2,400 gold fought like one soldier. The best life seen was `lord_1_17`, 16 kills in 195 s.
- Heroes die fast and pay for it. Towers fire at a hero first. Mordor's heroes died 11 times in `190524`, worth up to 2,750 of Rivendell's 6,284 bounty gold.
- The AI spends unit gold on heroes. Heroes took 54%, 50%, 54% and 23% of Mordor's spending at 1x and 4x, and units only 27% to 46%. Cause in the code: `AiCommander` buys a hero back as soon as the gold beats the price plus a reserve of 300.
- Gold sits unspent. Rivendell ended on 2,465, 1,473, 1,372 and 1,949 gold at 1x and 4x, and Mordor on 1,593 and 1,620 at 4x. Cause in the code: a full lane returned before the buy cycle moved, so the cycle stopped at a pack and never reached its later upgrades.

**Changed:**

- A hero's blows now do 3 times the damage and it takes 0.4 times the damage (`heroes.damageFactor`, `heroes.damageTakenFactor`), through the same damage model seam as the army upgrades. The end-of-match `[FordsOfWar][Upgrade]` line now counts the blows these factors changed (`heroRaised`, `heroLowered`), so a run shows whether low kills come from low contact or low damage.
- The AI's reserve rises to easy 600, normal 800, hard 1000 gold, so heroes and buybacks come from surplus gold. No new rule was needed.
- While the lane is full, gold above that reserve buys the next upgrade the cycle names, and the cycle keeps its place for when the lane opens.
- `tools/fords_match_review.py` now prints each hero's lives, time on the field and kills per minute, flags a hero under 2 kills per minute, and splits tower damage by class.

**To check on the next Rivendell and Mordor match at 4x:** heroes pass 2 kills per minute, `heroRaised` shows that they land blows, Mordor's hero share falls and its unit share rises, and both sides end with less unspent gold. The factors are a first guess: one hero must not decide a lane by itself.

## Open questions

- **Swordguard:** 725 and 850 kills for 47 and 34 deaths. It may be simply the elite answer to level 11 orcs. Read it against Erebor and Gondor before any change.
- **Horde feeds bounty:** Rivendell earned 7,286 and 8,099 gold, of which passive income gives about 1,800. A horde's deaths pay its enemy. The new gold words will give the bounty share.
- **Creature fighting value:** a troll made 0 to 4 kills in each match. Lower prices answer the cost; they do not say why trolls kill so little.
