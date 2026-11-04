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
| 20261103-193518 | `eedfca818` | Rivendell v Mordor | 164 / 1,466 | 5 / 1 | 13,211 / 52,608 | Rivendell 2.2 | 4x. Hero factors and spare upgrades. Mordor bought 3,990 gold of upgrades. Check 8 failed once. |
| 20261103-193955 | `eedfca818` | Rivendell v Mordor | 166 / 1,445 | 4 / 0 | 13 / 50,782 | Rivendell 1.8 | 4x. The same build. |
| 20261103-195038 | spare gold rule of `4297cd2f6`, no feat | Rivendell v Mordor | 173 / 1,618 | 3 / 0 | 2,682 / 44,071 | Rivendell 2.7 | 4x. Spare gold moves the buy cycle. |
| 20261103-195514 | spare gold rule of `4297cd2f6`, no feat | Rivendell v Mordor | 207 / 1,324 | 5 / 0 | 3 / 48,833 | Rivendell 1.5 | 4x. The same build. |
| 20261103-200151 | `4297cd2f6` | Rivendell v Mordor | 185 / 1,546 | 4 / 1 | 8,003 / 40,762 | Rivendell 1.4 | 4x. Mordor `damageToTowers` 0.6 added. |
| 20261103-200628 | `4297cd2f6` | Rivendell v Mordor | 180 / 1,583 | 4 / 0 | 493 / 36,226 | Rivendell 1.9 | 4x. The same build. |
| 20261103-201138 | `4297cd2f6` | Rivendell v Mordor | 878 / 4,603 | 6 / 0 | 7 / 64,717 | Rivendell 1.2 | 4x, 45 minutes of match time. No keep fell. Mordor ended on 8,973 gold. |
| 20261103-203523 | `4297cd2f6` | Dol Guldur v Erebor | 1,076 / 367 | 7 / 0 | 0 / 75,105 | Erebor 2.7 | 4x. Erebor won at 889 s: the first keep to fall. Poison 349 doses, 3,160 damage. Erebor ended on 4,510 gold. |
| 20261103-204007 | `4297cd2f6` | Gundabad v Isengard | 1,220 / 875 | 2 / 4 | 35,410 / 13,319 | Gundabad 1.4 | 4x. Gundabad ended on 5,448 gold. Isengard spent 6,125 on units and 480 on upgrades. |
| 20261103-204449 | `4297cd2f6` | Dale v Gondor | 783 / 653 | 4 / 0 | 7,979 / 44,366 | Dale 1.0 | 4x. |
| 20261103-204926 | `4297cd2f6` | Easterlings v Harad | 733 / 705 | 1 / 2 | 13,612 / 12,132 | Harad 1.1 | 4x. Poison 896 doses, 12,126 damage. |
| 20261103-205403 | `4297cd2f6` | Umbar v Dale | 678 / 805 | 1 / 3 | 39,715 / 8,014 | Umbar 1.1 | 4x. Umbar earned 2,623 from captures. |
| 20261103-205840 | `4297cd2f6` | Mirkwood v Rohan | 296 / 663 | 1 / 2 | 23,993 / 10,271 | Mirkwood 2.0 | 4x. Rohan's riders traded at 0.16 to 0.31. |
| 20261103-210317 | `4297cd2f6` | Erebor v Gundabad | 714 / 1,191 | 5 / 1 | 6,125 / 56,598 | Erebor 1.4 | 4x. |
| 20261103-210755 | `4297cd2f6` | Harad v Rohan | 744 / 687 | 2 / 2 | 16,013 / 24,852 | Harad 1.0 | 4x. Poison 664 doses, 8,134 damage. |
| 20261103-224617 | threatened-lane rule | Rivendell v Mordor | 141 / 1,438 | 5 / 0 | 522 / 55,502 | Rivendell 2.0 | 4x. Rivendell bought 2,380 gold of upgrades. |
| 20261103-225059 | threatened-lane rule | Dol Guldur v Erebor | 670 / 314 | 6 / 0 | no report | no report | 4x. The game crashed at about 760 s (`0x6B4C4E`, see the feature doc); numbers at 750 s. |
| 20261103-231633 | threatened-lane rule | Gundabad v Isengard | 1,170 / 675 | 0 / 3 | 28,469 / 0 | Gundabad 1.1 | 4x. Gundabad spent 5,298 on heroes; Isengard bought 2,190 of upgrades. |
| 20261103-232125 | threatened-lane rule | Dale v Gondor | 642 / 687 | 3 / 0 | 7,235 / 24,028 | Dale 1.5 | 4x. |
| 20261103-232603 | threatened-lane rule | Easterlings v Harad | 669 / 716 | 0 / 4 | 39,648 / 3,703 | Easterlings 1.2 | 4x. Easterlings bought 5,130 of upgrades. Poison 814 doses, 11,543 damage. |
| 20261103-233055 | threatened-lane rule | Umbar v Dale | 730 / 682 | 1 / 3 | 41,240 / 8,014 | Dale 1.2 | 4x. Umbar earned 2,490 from captures. |
| 20261103-233703 | threatened-lane rule | Mirkwood v Rohan | 256 / 681 | 1 / 3 | 26,667 / 21,849 | Mirkwood 2.9 | 4x. |
| 20261103-234225 | threatened-lane rule | Erebor v Gundabad | 561 / 1,201 | 6 / 0 | 5 / 57,423 | Erebor 1.3 | 4x. Gundabad spent 5,824 on heroes; Erebor bought 2,850 of upgrades. |
| 20261103-234757 | threatened-lane rule | Harad v Rohan | 642 / 661 | 3 / 2 | 25,505 / 23,569 | Harad 1.1 | 4x. Poison 718 doses, 8,692 damage. |
| 20261104-001612 | tower limit 50 | Rivendell v Mordor | 142 / 1,533 | 2 / 1 | 9,727 / 22,881 | Rivendell 2.4 | 4x. 1,566 blows `Crowded`. Mordor's feat removed. |
| 20261104-002054 | tower limit 50 | Dol Guldur v Erebor | 733 / 503 | 2 / 1 | 11,778 / 34,553 | Erebor 1.5 | 4x. No crash this time. |
| 20261104-002531 | tower limit 50 | Gundabad v Isengard | 1,162 / 631 | 1 / 2 | 21,571 / 8,407 | Gundabad 1.0 | 4x. Check 12 failed: one warg rider not back in 60 s. |
| 20261104-003008 | tower limit 50 | Dale v Gondor | 673 / 646 | 1 / 0 | 11,914 / 18,906 | Dale 1.1 | 4x. |
| 20261104-003451 | tower limit 50 | Easterlings v Harad | 681 / 701 | 0 / 3 | 26,105 / 3,212 | Easterlings 1.0 | 4x. |
| 20261104-003928 | tower limit 50 | Umbar v Dale | 722 / 643 | 1 / 0 | 20,757 / 9,790 | Dale 1.7 | 4x. |
| 20261104-004402 | tower limit 50 | Mirkwood v Rohan | 288 / 664 | 2 / 2 | 28,159 / 27,990 | Mirkwood 1.4 | 4x. |
| 20261104-004839 | tower limit 50 | Erebor v Gundabad | 529 / 1,284 | 3 / 1 | 6,092 / 34,602 | Erebor 1.4 | 4x. |
| 20261104-005316 | tower limit 50 | Harad v Rohan | 645 / 637 | 1 / 2 | 31,110 / 26,350 | Rohan 0.9 | 4x. Harad also 0.9. |
| 20261104-011218 | `6e509d555` | Rivendell v Mordor | 651 / 5,557 | 2 / 4 | 40,064 / 20,959 | Rivendell 1.8 | 4x, 45 minutes. No keep fell. Check 8 failed again on the same lane. |
| 20261104-012426 | `6e509d555` | Erebor v Gundabad | 2,163 / 5,622 | 1 / 6 | 41,037 / 20,266 | Erebor 1.1 | 4x, 45 minutes. No keep fell; Gundabad's last tower fell at 2,192 s. |
| 20261104-013631 | `6e509d555` | Dale v Gondor | 2,660 / 2,756 | 2 / 0 | 20,456 / 26,074 | Dale 1.0 | 4x, 45 minutes. No keep fell. |
| 20261104-014937 | `6e509d555` | Rivendell v Mordor | 145 / 1,609 | 1 / 0 | 4,669 / 16,870 | Rivendell 3.0 | 1x, the limit's confirmation. 290 blows `Crowded`. |
| 20261104-022226 | Rohan's rider prices | Mirkwood v Rohan | 278 / 654 | 1 / 3 | 32,417 / 17,347 | Mirkwood 1.0 | 4x. Rohan 0.7. |
| 20261104-022704 | Rohan's rider prices | Harad v Rohan | 654 / 670 | 2 / 3 | 25,529 / 16,206 | Rohan 1.2 | 4x. |
| 20261104-023142 | Rohan's rider prices | Rohan v Gondor | 587 / 620 | 1 / 0 | 8,526 / 16,171 | Gondor 1.0 | 4x. Rohan 0.9. |
| 20261104-023619 | Rohan's rider prices | Rohan v Mordor | 630 / 1,322 | 1 / 0 | 4,631 / 16,415 | Rohan 2.0 | 4x. |
| 20261104-024056 | Rohan's rider prices | Dol Guldur v Easterlings | 734 / 614 | 3 / 0 | 0 / 29,604 | Easterlings 1.4 | 4x. |
| 20261104-024532 | Rohan's rider prices | Dol Guldur v Rohan | 739 / 619 | 3 / 2 | 16,035 / 35,456 | Rohan 1.3 | 4x. |
| 20261104-025115 | Dol Guldur's prices | Dol Guldur v Erebor | 768 / 526 | 3 / 0 | 11,936 / 27,090 | Dol Guldur 1.1 | 4x. |
| 20261104-025552 | Dol Guldur's prices | Dol Guldur v Easterlings | 759 / 624 | 4 / 0 | 4,436 / 35,486 | Easterlings 1.3 | 4x. |
| 20261104-030029 | Dol Guldur's prices | Dol Guldur v Rohan | 722 / 616 | 3 / 0 | 4,474 / 34,476 | Rohan 1.4 | 4x. |
| 20261104-031711 | `0b9d37166` | Harad v Rohan | 630 / 651 | 2 / 3 | 25,287 / 18,530 | Harad 0.9 | 1x. Rohan also 0.9. The 4x match of this pair also ended 2 / 3. |

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

### Heroes and the AI's spare gold (`eedfca818`)

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

### What `eedfca818` did, and the second pass (`4297cd2f6`)

**Seen on `eedfca818`, two matches at 4x:** the hero factors worked. Elladan made 20 kills with no death, and Mordor's heroes died 4 and 3 times, where they died 11 times in `190524`. The gold rules did not work. Rivendell lost 5 and 4 towers and took 1 and 0. Mordor put 56% and 71% of its gold into upgrades, five levels by about 500 s, and Rivendell bought one level in that time.

**Cause:** contact on Rivendell's towers doubled, from 2,058 to 2,213 unit-seconds to 4,075 to 5,080, while damage per blow rose from 14.5 to 15.2 up to 16.0 to 17.6. Without its 1,000-gold hero at about 210 s, Mordor brought a larger horde sooner. Upgrades then gave it more, because their price is flat and their value grows with the army.

**Changed:** the spare-upgrade rule is gone. While the assault lane is full, gold above the reserve buys the cycle's next pack for the lane with the most room, and the cycle moves on.

**Result, `195038` and `195514`:** spending now fits the design: Mordor spent 3,825 and 3,610 gold on units and 1,140 on upgrades, and fielded its first temple guard. Check 8 passed. The towers did not change: Rivendell lost 3 and 5 and took none.

**What that shows:** the even towers before this pass came from Mordor wasting its gold. Value lost in the logs puts an orc at about 5 gold, and its blow does about the same tower damage as an elite's, so a cheap pack is the best tower killer in the game. An 8,000-point elven tower fell 32 and 34 s after its first logged blow, where a Mordor tower took 163 and 216 s against Rivendell.

**Changed:** Mordor's units deal 40% less damage to towers (`damageToTowers` 0.6), and a fourth trait line tells the player.

**Result, `200151` and `200628`:** Rivendell's towers fell later: at 222, 246, 621 and 852 s, and at 213, 396, 512 and 882 s, where they fell at 181, 220 and 412 s, and at 187, 205, 305, 382 and 628 s, without the feat. Rivendell still lost 4 and took 1 and 0.

**Who wins, `201138`:** the first match run past 15 minutes on these rules, 45 minutes at 4x. No keep fell. Rivendell lost all 6 towers, the last at 2,372 s, and Mordor lost none. Rivendell dealt 7 tower damage in 45 minutes. Its two inner towers lasted about 25 minutes under attack, which fits the 30 to 45 minute target. Mordor ended on 8,973 gold.

**Check 8:** it failed once, in `193518`: a held Mordor lane stood in elven tower fire for four samples. The hold rule already stands back by the extra reach (`LaneMarchRules.Hold`, 82.5 m from an elven tower), so that is not the cause. It passed in the next five matches. The cause is not known.

### The eight-pair survey, and a side that falls behind

**Seen:** one match of each other pair at 4x, on `4297cd2f6`, with no crash and no failed check. Harad's matches and Easterlings v Harad were close. Four were one-sided on towers: Erebor beat Dol Guldur 7 to 0, Gondor beat Dale 4 to 0, Gundabad beat Erebor 5 to 1, and Umbar beat Dale 3 to 1. In each, the loser dealt little tower damage: 0, 7,979, 6,125 and 8,014. First towers fell at about 160 to 290 s in every pair.

**Cause in the code:** `AiCommander.Defend` bought only the cheapest spear or infantry pack, and it ran first. With every lane threatened, `PickAssaultLane` finds no lane, so `BuildAssault` never ran and the buy cycle stopped. A side that fell behind therefore got no elites and no upgrades, and fell further behind. Rivendell v Mordor showed it first, but it holds for any side that falls behind, not only a small army. Isengard spent 6,125 gold on units and 480 on upgrades.

**Changed:** with a buy cycle, a threatened lane now gets the cycle's next entry, and the AI saves for it. A faction with no cycle keeps the old rule.

**Result, the same nine pairs at 4x:** the rule does what it was for. Sides that fell behind now buy upgrades: Rivendell 510 to 2,380 gold, Isengard 480 to 2,190, Erebor against Gundabad 900 to 2,850. The towers did not even out: Rivendell lost 5 and took 0, Gundabad beat Erebor 6 to 0 and Isengard 3 to 0, and Easterlings beat Harad 4 to 0. Losers still dealt almost no tower damage (0, 5 and 522). So this rule alone does not bring a losing side back, and the keep is reached as fast as before.

**Other findings to read against that run:**

- **Dol Guldur** is the weakest so far: gold trade 0.34, 3,478 gold earned, and its poison did 3,160 damage where Harad's did 8,134 and 12,126.
- **Rohan's riders** traded at 0.16 to 0.31 in two matches, though the design makes them the best cavalry. Over the nine matches of the new rule, by role: ranged 2.19 gold trade, 1.18 kills per death; infantry 1.15, 0.82; cavalry 0.71, 1.12; spears 0.78, 0.67; horse archers 0.13, 0.50. Cavalry fights well per body, so its price is the lever. Horse archers fight badly at any price.
- **A winner's or a horde's gold:** Erebor ended on 4,510 and Gundabad on 5,448, with nothing left to buy.
- **Frame time** at 4x rose from about 4 ms in `170032` to about 10 ms from `190524` on. The builds between differ only by where the speed button stands. The cause is not known.

### The tower assault limit (this change)

**Why:** the measure above left the towers one-sided, and the cause sat in the tower rule. Every body at a wall counts in full, so a cheap pack is the best tower killer: an elven tower fell 32 s after its first blow, about 250 damage a second, while Rivendell's own attacks did 21 to 38 a second. That conflicts with the design, where a tower takes many waves. A horde then stands in every lane, so the other side never reaches a tower.

**Changed:** a tower takes at most 50 points a second from ordinary blows (`towers.assaultDamagePerSecond`). Heroes and siege units pass the limit and spend none of it. An outer tower now lasts at least 160 s against soldiers alone. Mordor's `damageToTowers` 0.6 and its trait line are gone, because the limit treats every horde the same way.

**Result, the same nine pairs at 4x:** towers lost moved from 5 / 0 to 2 / 1 for Rivendell v Mordor, 6 / 0 to 3 / 1 for Erebor v Gundabad, 3 / 0 to 1 / 0 for Dale v Gondor, and 1 / 3 to 1 / 0 for Umbar v Dale. Easterlings v Harad stayed one-sided at 0 / 3. The side that was behind now reaches towers: Rivendell's tower damage rose from 522 to 9,727, Dol Guldur's from 0 to 11,778, Isengard's from 0 to 8,407, and Erebor's from 5 to 6,092. No keep fell in 15 minutes, and first towers fell later, between 324 and 905 s in most pairs. Between 569 and 1,736 blows per match were `Crowded`.

**At 1x:** one match of Rivendell v Mordor (`014937`) agrees: 1 tower lost to 0, Rivendell's gold trade 3.0, Mordor's tower damage 16,870. One match of Harad v Rohan on the final prices (`031711`) was close, as at 4x: 2 towers to 3, gold trades 0.94 and 0.89.

**How long a match lasts now:** three matches of 45 minutes (`011218`, `012426`, `013631`). No keep fell in any. Rivendell, which lost all 6 towers and took none before the limit, now leads Mordor 4 towers to 2 at 45 minutes, so a small army comes back as the match goes on. Erebor took all 6 of Gundabad's towers by 2,192 s and did not finish the keep in the 510 s left. Dale and Gondor reached 2 towers to 0: each side dealt only about a third of the 75,000 points that a side's towers and keep hold. So the limit fixed who can win, and a match between even sides now runs past the 30 to 45 minute target. Raising the limit would not solve that: only 15 to 25 percent of blows on any side's towers were `Crowded`. Even sides fight mid-lane, and few of them reach a tower.

### Rohan's riders (this change)

**Why:** over nine matches, by role, cavalry made 1.12 kills per death, close to ranged units' 1.18 and above infantry's 0.82, but its gold trade was 0.71 against 2.19 and 1.15. Rohan's riders traded at 0.09 to 0.49. So they fight as cavalry should, and the price is what fails the design's "finest cavalry in Middle-earth".

**Changed:** Rohan's rider packs cost 0.7 of the full price for their level, and its horse archers 0.5, which makes them the only riders so discounted.

**Result, six matches at 4x:** cavalry's gold trade across all factions rose from 0.71 to 1.20, and Rohan's from 0.47 to 0.72 against Mirkwood, 0.93 to 1.22 against Harad, and to 1.96 against Mordor. Rohan took 3 towers to 1 from Mirkwood and 3 to 2 from Harad. Horse archers improved to 0.38, and they remain Rohan's weak pack.

### Dol Guldur (this change)

**Why:** Dol Guldur lost its fights against all three opponents at about 0.6 gold trade (0.64 Erebor, 0.67 Easterlings, 0.62 Rohan), and earned about 5,000 gold where they earned 7,500. Creature calls returned 0.14 of their gold across those six matches.

**Changed:** its four cheapest packs cost 0.8 of the full price, spider riders 1.5 where they cost 2.5, and fell wargs 1.2 where they cost 2.0. Its income and poison stay as its trait lines state.

**Result:** mixed. Its gold trade against Erebor rose to 1.06, but it stayed at 0.66 against Easterlings and 0.63 against Rohan, and it took no tower in the three matches. The change stays because it does no harm, and Dol Guldur is still the weakest faction. Its next lever is its income or its poison, and each needs a new trait line.

## Open questions

- **Match length (a decision for Mike):** even sides do not finish in 45 minutes. A time-based rule would end such matches, for example tower damage rising with the match clock after a set minute. That is a new rule rather than a number, so it waits for his word.
- **Check 8 on Rivendell's middle lane:** it failed in `193518` and `011218`, both times with a held Mordor lane 1 in the fire of `tower_west_1_0`, and the lane's front stood closer than its hold point (mean 54 m, hold point 82.5 m). The cause is not known.
- **Check 12, a raging warg's return:** `20261104-002531` was the first match of these to see a rage at all, and that rider was not back in its formation within 60 mission seconds. One sample at 4x.
- **Harad and creature calls:** Harad lost 3 towers and took none against Easterlings. Creature calls return little of their gold in every faction that has them (0.14 to 0.28), though their kills per body are the highest of any role.
- **Spears:** 0.73 to 0.79 gold trade in every set of matches. No lane here brings them the cavalry they are for.
- **A small army's attack:** Rivendell dealt 7 tower damage in 45 minutes before the limit, and 9,727 in 15 minutes with it. Read it again in the next long match.
- **A horde's late gold:** with every lane full, every hero out and its cycle's upgrades bought, Mordor has nothing to spend on. It ended a 45-minute match on 8,973 gold.
- **Mordor's heroes:** 0.4 to 1.5 kills per minute in `200151` and `200628`, where Rivendell's made 1.7 to 5.4. In `201138` Sauron made 4.6 and the Nazgul 0.9.

- **Swordguard:** 725 and 850 kills for 47 and 34 deaths. It may be simply the elite answer to level 11 orcs. Read it against Erebor and Gondor before any change.
- **Horde feeds bounty:** Rivendell earned 7,286 and 8,099 gold, of which passive income gives about 1,800. A horde's deaths pay its enemy. The new gold words will give the bounty share.
- **Creature fighting value:** a troll made 0 to 4 kills in each match. Lower prices answer the cost; they do not say why trolls kill so little.
