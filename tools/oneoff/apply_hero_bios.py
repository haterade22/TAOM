#!/usr/bin/env python3
"""
One-shot: inject text= attributes into heroes.xml for ~47 Gondor + Mordor lords.

Reads BIOS table, finds each Hero block by id="..." attribute, inserts
    text="{=key}prose"
before the closing /> if not already present.

Exits non-zero if any expected hero isn't found or already has text=.
"""

import re
import sys
from pathlib import Path

HEROES_XML = Path(r"c:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\characters\heroes.xml")

# (hero_id, key, prose)
# Key convention: aom_lord_<id>_bio
BIOS = [
    # ----- Gondor: vanilla-passthrough houses (batch A) -----
    ("lord_1_9_6",  "aom_lord_1_9_6_bio",  "Lothériel of the House of Imrazôrionath was born to the silver shores of Belfalas, where the sea-wind tempers the daughters of old Númenor as surely as the sword tempers the son. Though yet young in her years, she has thrice ridden with the Swan-Knights upon the watch of the southern roads, and the elder ladies of Dol Amroth name her quick of wit and steady of hand. Her house holds firm to Lord Denethor, for the City must not falter while the shadow grows long, and the oath of the Stewards is an oath of stone."),
    ("lord_1_9_5",  "aom_lord_1_9_5_bio",  "Lothwen, dowager-aunt of the House of Imrazôrionath, has counted seventy and two winters upon the white cliffs of Belfalas, and her counsel is sought ere any voyage is undertaken or any betrothal sealed. In her youth she sailed with her brother to the haven of Edhellond and there, it is whispered, looked upon the last of the Elven mariners; the silver brooch she yet wears is the token of that meeting. She abides by the Steward of the City and chides any kinsman who would speak of a king unproven, for vigilance is the long virtue of her line."),
    ("lord_1_11_2", "aom_lord_1_11_2_bio", "Silwen, brought in dower from a sister-house of Pelargir to the House of Eärnurionath, has the bearing of the sea-roads in her speech and the wariness of the haven-watch in her eye. She wedded Calion in the autumn of her three and thirtieth year, and to her dowry she added a chart of the southern shoals drawn by her grandsire who fell at the burning of the quays. With her husband she holds for the King returned, for she has seen, year upon year, how the Corsairs grow bold while the City waits, and waiting alone shall not now suffice."),
    ("lord_1_11_1", "aom_lord_1_11_1_bio", "Calion of the House of Eärnurionath keeps the sea-wall of Pelargir as his fathers did before him, since the days when the Faithful first laid stone upon that haven in the Second Age. He bears a long blade named Sîrlam, the River-Tongue, which his great-grandsire wielded at the breaking of the corsair fleet off Tolfalas. In these darkening years he has lent his voice and his ships to the cause of the King returned, holding that the oath of the Stewards was sworn until such a one should come, and come he has."),
    ("lord_1_40_2", "aom_lord_1_40_2_bio", "Eleniel of the House of Barahirionath is but fourteen summers old, yet already she is named among the fairest maidens of Lossarnach, the Vale of Flowers. She is taught the old songs of plenty and the older songs of mourning, and her father's foresters have begun to school her in the axe-craft for which her vale is famed. Her house keeps faith with Lord Denethor, as the husbandmen of Lossarnach have ever kept faith with the City whose granaries they fill; the young lady knows no other loyalty, and asks none."),
    ("lord_1_52_4", "aom_lord_1_52_4_bio", "Nauriel of the House of Halboronionath, sixty winters told and the institutional memory of her line, dwells in the upland halls of Lamedon above the cold rush of Ciril. In her middle years she rode with the hillmen against the brigands of the high passes, and she yet keeps the dented helm of that summer upon the mantel of her hall. She is among the firmest voices for the King returned, holding with Angbor and the stubborn lords of the dales that the long waiting has worn the realm thin, and that the oath must at the last be honoured."),
    ("lord_1_53_1", "aom_lord_1_53_1_bio", "Calaniel was wedded into the House of Malandilionath out of the green wolds of Pinnath Gelin, and she brought with her the fair hair of Hirluin's country and a small banner of pale green that her mother stitched for her bridal-journey. She is eight and twenty, gentle of voice but not of purpose, and she has borne a son who shall one day raise the house's standard upon the western hills. With her husband's kindred she stands by the Steward of the City, for the green hills lie far from any northern claimant and near indeed to the beacons of Anórien."),
    ("lord_1_53_2", "aom_lord_1_53_2_bio", "Ciriondir of the House of Malandilionath, fourteen years of age, is the chosen heir of his father's hall in the green country of Pinnath Gelin. He is taught the bow and the long shepherd's stride, for the youths of his vale must outpace the wolf and the worse than wolf when the watch-fires are lit. Reared upon his father's knee in the loyalties of his house, he is sworn in his heart to Lord Denethor, and he counts the days until he may bear his green pennon eastward to the muster of the City."),
    ("lord_WE8_1",  "aom_lord_WE8_1_bio",  "Dorwen came to the House of Olindurionath out of a lesser fief upon the windward coast of Anfalas, where the huntsmen reckon their kindred by the seasons of the seal-hunt rather than by the rolls of Minas Tirith. Three and thirty winters she has counted, and she has not unlearned the sea-wisdom of her father's strand, nor the silence that the long coast teaches. She walks with the King's faction as her wedded house does, holding that the Langstrand has waited too long upon a City that forgets its further shores."),
    ("lord_WE8_2",  "aom_lord_WE8_2_bio",  "Eleneth, second of the wedded ladies of the House of Olindurionath, was given in alliance out of an old huntsmen-line of the Anfalas coast, and she is thirty winters told. She keeps a small reliquary of weathered driftwood said to bear the mark of a Númenórean keel cast up in the year of the Dimming, and she names it her dower above any gold. She holds, with her house, for the King returned, for the wind-bitten shore has long memory and longer hope, and the watchers of the Langstrand would see the old line restored."),
    ("lord_WE8_4",  "aom_lord_WE8_4_bio",  "Talborin, fifty winters told, wedded into the House of Olindurionath out of a kindred-hall of the Langstrand, and he bears upon his shield the grey gull of his fathers quartered with the device of his wife's line. In his youth he sailed with the watch-galleys that ward the long coast against the corsair sail, and a scar of that work yet runs from his temple to his jaw. He stands with his house for the King returned, for he has watched too many years of vigilance go unrewarded by the lords of the inland City."),
    ("lord_1_71_1", "aom_lord_1_71_1_bio", "Laswen of the House of Olindurionath, three and fifty winters of age and the eldest sister of the present lord, is the keeper of the house's chronicles upon the rim of the Langstrand. From her seat above the grey strand she has counted the sails of three generations of corsairs, and she has written down the names of the watchmen who did not return. She is the steady voice of the King's cause within her hall, for she has weighed the long ledger of the coast's neglect and finds the Steward's keeping wanting."),
    ("lord_WE8_3",  "aom_lord_WE8_3_bio",  "Hirilwen of the House of Olindurionath, eight and forty years of age, never wedded, and is named among her kindred the watch-lady of the western tower. Through the salt-rimed nights of her middle years she has trimmed the lamp that warns the fisherfolk of the long reef, and she keeps in a chest of black oak the hunting-knife of her brother who fell upon the dunes in the corsair raid of her youth. She holds, as her house holds, for the King returned, for the Langstrand has too often watched alone."),
    ("lord_WE9_3",  "aom_lord_WE9_3_bio",  "Derufin of the House of Danuhirionath, three and thirty winters of age, was reared in the shadowed dales of Morthond beneath the stern crags that look toward the Paths of the Dead. He bears the long yew bow of his fathers, and his arm is counted among the surest of Duinhir's archer-country, where the old oaths of watchfulness are not lightly set aside. With the stern lords of the Blackroot Vale he stands by Lord Denethor, holding that in the hour the shadow gathers the realm must keep one hand upon the helm, and that hand the Steward's."),
    # ----- Gondor: extended houses 10-11 (batch B) -----
    ("lord_1_59",   "aom_lord_1_59_bio",   "Rondamir, head of the House of Hýarthulionath, holds the upland fastness of Erech-march in Lamedon, where his forefathers swore fealty to Mardil Voronwë in the dimming after Eärnur rode forth. Tall and grey-eyed in the old Númenórean mould, he bears the sword Calanril, drawn in his great-grandsire's hand at the Crossings of Erui. In this hour of sundering he holds firm to the Steward of Gondor, deeming the oath unbroken while the Tower of Ecthelion yet stands. Now, as the shadow grows long upon the Ephel Dúath, he summons his hillmen to the muster of Minas Tirith."),
    ("lord_1_59_1", "aom_lord_1_59_1_bio", "Rondiel, wedded to Lord Rondamir, was born of a lesser branch of the House of Ecthelionath in Lossarnach, and brought to her husband's hall the gentle learning of the Vale of Flowers. She is versed in the Sindarin tongue and in the lore of healing herbs gathered ere the frost. Her marriage knit the stern hill-folk of Lamedon to the orchard-lords of the south, and she is held in honor as a Lady of measured counsel. With her lord, she stands faithful to Denethor son of Ecthelion."),
    ("lord_1_59_3", "aom_lord_1_59_3_bio", "Galinwen came out of Pelargir, daughter of a shipwright-house of old Faithful blood that has watched the sea-roads against the Corsairs of Umbar since the days of Castamir's flight. By her hand the House of Hýarthulionath has gained tidings of every keel betwixt Ethir Anduin and the Bay of Belfalas. She wedded Silmador in the autumn of a year of fair harvest, binding hill to haven. In the present strife she follows her husband's house and the Steward's banner."),
    ("lord_1_59_2", "aom_lord_1_59_2_bio", "Silmador, kinsman of Lord Rondamir and second of the hall, is reckoned the swiftest rider of the house and warden of its eastern marches toward the Mering Stream. He bears a coat of mail wrought in the smithies of Erech, bequeathed to him by his uncle who fell on the Pelennor of an older war. By his wedding to Galinwen of Pelargir he has drawn the salt-wind of the haven into the high vales. He holds, as his kinsman holds, with Denethor of the House of Húrin."),
    ("lord_1_59_4", "aom_lord_1_59_4_bio", "Galdirion, son of Rondamir and Rondiel, is but fourteen summers old, yet already the household masters at arms set him to the practice-blade each dawn upon the courtyard stones. He is grey-eyed as his father, and quiet in the manner of the old Númenórean stock, given to long readings of the Annals of the Stewards. The hall names him heir of Erech-march, though he has not yet ridden to war. Ere the Shadow is broken, men say, his hour shall be required of him."),
    ("lord_EW_9",   "aom_lord_EW_9_bio",   "Thingol, in his five-and-fiftieth year, is the elder kinsman of the house and warden of its archives, and was first sword-bearer to Rondamir's father in the campaigns along the Poros. He is grave of speech, sparing of laughter, and the names of all the fallen of the house lie graven in his memory. In council he speaks last and seldom, yet his word turns the chamber. He counsels his lord that the oath to the Steward must endure while the King is not yet come."),
    ("lord_1_60_1", "aom_lord_1_60_1_bio", "Nimriel, Lady of the House of Caladionath, was born in Anfalas of the windward stock of Langstrand, daughter of a huntsman-lord whose hawks were prized in the courts of Dol Amroth. She came to Eldorion's hall in her twentieth year, bringing the sea-wisdom of the coast and a keen falconer's eye that has more than once warned the watch ere the beacons were lit. She is of the elder Númenórean cast, fair-skinned and tall, and walks the battlements at evening as her lord walks the muster-yards. With Eldorion she stands faithful to Denethor son of Ecthelion."),
    ("lord_1_60_3", "aom_lord_1_60_3_bio", "Orlendir came from the Morthond Vale, of an archer-house that has kept the Stone of Erech in its long sight since the breaking of the oath of old. He is two-and-twenty years of age, dark of hair and quiet of bearing, reckoned the keenest bowman to ride beneath the banner of Caladionath. His marriage to Orlathiel bound the watchful Blackroot to the northern bulwark of Eldorion's hall. He holds, as his good-father holds, with the Steward of Gondor."),
    ("lord_1_60_2", "aom_lord_1_60_2_bio", "Orlathiel, daughter of Eldorion and Nimriel, is two-and-twenty, and counted among the fairest of the ladies of the upper hall, grey-eyed and dark-haired in the manner of her father's line. She was schooled in the histories of the House of Húrin and in the songs of the Stewards, and rides with her husband's archers upon the long patrols toward the eastern fences. The folk of the hall name her the Lady of the Morning Watch. In the present sundering she follows her father's faith, and the Steward's banner."),
    # ----- Gondor: extended houses 12-14 (batch C) -----
    ("lord_EW_14",   "aom_lord_EW_14_bio",   "Vorondir, Lord of Garvirionath, holds the green vales of Anorien beneath the beacon of Amon Din, where his fathers have kept watch since the days of Steward Belecthor. Gallant in the saddle and grave in council, he bears Lossen, the silver sword forged at Pelargir for his grandsire who fell at the Crossings of Erui. In these dimming years he has set his oath unbroken with Lord Denethor, holding the old custodial bond above the rumour of a king out of the wild North. By day he rides the marches with Rohirric scouts; by night he watches the eastern stars and counts how long the shadow has grown."),
    ("lord_EW_14_1", "aom_lord_EW_14_1_bio", "Silmariel, born of the lesser house of Tarondor in Lebennin, came to Garvirionath in her twenty-second summer bringing a dowry of fair river-meadows and the loyalty of pikemen of the Five Streams. She is a lady of soft speech and unbending mind, and the hearth-hall of Amon Din has prospered under her hand. In the matter of the Steward and the claimant from the North, she counsels her lord to keep faith with Minas Tirith ere all is sundered."),
    ("lord_EW_14_3", "aom_lord_EW_14_3_bio", "Rumen of the line of Cirdacar rode out of Pinnath Gelin to wed Nimethil, bringing the green banner and the troth of shepherd-spears unto Garvirionath. Though yet young in years, he proved his valor at a skirmish nigh Cair Andros, where he held a broken ford against orc-scouts until the beacon-riders came. He follows his lord-father Vorondir in the cause of the Steward, and dreams of a sword-name of his own ere his beard is grown."),
    ("lord_EW_14_2", "aom_lord_EW_14_2_bio", "Nimethil, only daughter of Vorondir and Silmariel, was raised within sight of the beacon-fires and learned to ride ere she learned to weave. She is dark-haired and grey-eyed after the manner of her kindred, slender as a rowan and twice as proud. In the gathering quarrel she stands with her father for Denethor, though she has been heard to speak softly of the old prophecies of the king's return."),
    ("lord_EW_23",   "aom_lord_EW_23_bio",   "Anaratan, great-uncle to Lord Vorondir, is the living chronicle of Garvirionath, having ridden in his youth with Ecthelion the Second against the Corsairs at the haven of Pelargir. Sixty winters have whitened his hair but not dimmed the iron in his glance, and the household defers to his counsel in matters of lineage and oath. He holds firm with Denethor, saying that an old house keeps its word though the heavens fall."),
    ("lord_EW_1",    "aom_lord_EW_1_bio",    "Amandir, Lord of Hirilionath, is the eldest of the Tier lords of Gondor, and his memory reaches back unto the boyhood of the Steward himself. His seat is at Ethring above the fords of the Ringlo, in upland Lamedon, where the hill-spearmen have served his house for nine generations. He bears the silver-on-sable banner of his fathers, and the long sword Tirnaith, named for a kinsman lost at the Dagorlad of old. In the matter of the king out of the North, Amandir has cast his weight with the claimant, holding that the Steward's oath was sworn until the king should come again, and that this hour, ere he himself passes, is that hour."),
    ("lord_EW_1_1",  "aom_lord_EW_1_1_bio",  "Ciriel was born to the seafaring house of Lhanthir in Belfalas, and the salt of Dol Amroth is yet upon her speech though sixty-seven winters have passed since she rode inland to wed Amandir. She brought to Hirilionath the alliance of Swan-Knight kinsmen and a chest of pearls from the silver shore. In her age she counsels her lord and her son alike, and stands with Amandir for the King."),
    ("lord_EW_1_3",  "aom_lord_EW_1_3_bio",  "Dorwen of the house of Maeglion in Morthond Vale came to Hirilionath bearing the troth of Blackroot bowmen and the stern watchfulness of her shadowed valley. She is grave of countenance and slow of word, and has borne her husband Pelamir one son and many years of patient stewardship while the old lord lingered. She follows the house in its turning toward the claimant, though she remembers the old oaths to the White Tower with sorrow."),
    ("lord_EW_1_2",  "aom_lord_EW_1_2_bio",  "Pelamir, only son of Amandir and Ciriel, has stood three and forty years in the long shadow of his father, learning patience as other men learn the sword. He is grey already at the temples though his sire yet lives, and the hill-folk of Lamedon call him quietly the Waiting Lord. With Amandir he has declared for the king out of the North, and many say that when the old lord at last passes, Pelamir shall ride east at the claimant's banner without a backward glance."),
    ("lord_EW_1_4",  "aom_lord_EW_1_4_bio",  "Sarnion, son of Pelamir and Dorwen, is two and twenty and the youngest sword of Hirilionath, dark-haired and lean as a hill-hound. He was sent in his fifteenth year to foster with kinsmen in Morthond, that he might learn the bow of his mother's people alongside the spear of his father's. He follows his grandsire and his father in the cause of the King, and burns with the hot certainty of the young."),
    ("lord_EW_20",   "aom_lord_EW_20_bio",   "Balatar, cousin-german to Lord Amandir, has kept the lower hall of Ethring these forty years and is reckoned among the last men living who remembers the funeral of Steward Turgon. He is broad-shouldered yet, though his beard is wholly white, and the household captains seek him out for tales of campaigns long ended. He stands with his kinsman Amandir for the King, saying it is meet that an old man should see the old oath fulfilled ere he is laid in stone."),
    ("lord_EW_6",    "aom_lord_EW_6_bio",    "Orondir, Lord of Baranionath, came to the headship of his house at nineteen, when his father Baranor was slain upon the eastern shore of Anduin in a sortie against Ithilien orc-bands. Now two and twenty, he rules the coastal fastness of his fathers at Edhellond in southern Belfalas, and the weight of an ancient name lies heavy upon young shoulders. He bears Calanaith, his father's blade, recovered with grim cost from the field where Baranor fell, and the swan-prowed ships of his haven answer to his horn. In the gathering quarrel he keeps faith with Lord Denethor, holding that vigilance, not hope, is the bulwark of these dark years."),
    ("lord_EW_6_1",  "aom_lord_EW_6_1_bio",  "Falathwen was born of a knightly house of Anfalas, the wind-bitten Langstrand, and was betrothed to Orondir when both were yet children, the alliance sworn between their fathers ere the eastern war took Baranor. She is gentle of bearing but resolute, and has borne her young lord twin children before her own four and twentieth year. She follows him in his fealty to the Steward."),
    ("lord_EW_6_2",  "aom_lord_EW_6_2_bio",  "Orweniel, daughter of Orondir and Falathwen, was born with her twin brother Talendir under the harvest-moon of a year of dark tidings. At fourteen she is already tall, grey-eyed and quiet, and has begun her lessons in the lore of her foremothers of Belfalas. The household reckons that the line of Baranionath shall not fail while she yet draws breath."),
    ("lord_EW_6_3",  "aom_lord_EW_6_3_bio",  "Talendir, twin-brother to Orweniel, is fourteen and the hope of Baranionath, fair-haired after his mother and grey-eyed after his sire. He rides daily upon the strand at Edhellond and is taught the sword by old men who served his grandfather. In the quarrel of these days he knows only that his father stands with the Steward, and that is enough."),
    # ----- Mordor: 9 uruk-captains (batch D) -----
    ("lord_M10_1", "aom_lord_M10_1_bio", "Mauhoshat was hammered into a captain in the marshalling camps of Udun, where the legions of the Morannon are sorted and counted before the long march out. He took the Arki standard by goading three rivals into a knife-quarrel and walking out of the pit alone, a tale the muster-clerks of Lugburz are said to have written down. He covets the favour of the Great Eye above all, and trains his uruks to be noticed by the wraiths who ride down from the iron gate. The Ikhon under Maugrukh he names a company of pit-rats, for they reported his muster-count light at the last great gathering."),
    ("lord_M16_1", "aom_lord_M16_1_bio", "Gulnak stands at Mauhoshat's right hand in the Udun camps, and the clerks of Lugburz mark him as the sharper blade of the two. He has been passed over twice for a banner of his own, and the resentment of it sits behind his teeth like a second tongue. He waits for Mauhoshat to be summoned to the Black Gate or thinned in the Pelennor, and reckons the Arki muster will fall to him before the next great war-march. Until that day he flatters his captain and counts his rivals."),
    ("lord_M11_1", "aom_lord_M11_1_bio", "Maugrukh commands the watcher-tower of Cirith Ungol, that high paranoid roost above the Morgul Vale where Shelob lairs in the under-passes. He took the tower by cutting down the previous captain on the stair, an old quarrel about a shipment of mail looted from the tarks at Osgiliath. The Eye sees little of him there, and he likes it so, for a captain far from Lugburz may eat his own ghash and answer to none. He hates Mauhoshat of Udun, who once named his garrison a kennel of snaga."),
    ("lord_M17_1", "aom_lord_M17_1_bio", "Thulnar serves as Maugrukh's lieutenant on the Cirith Ungol stair, and he has counted every coin in the captain's strongbox twice. Loot is his drive and the tower his hunting-ground, for caravans bound from Minas Morgul up to the pass are easily skimmed by the orcs who watch the road. He pays his uruks in trinkets stolen from the men of Gondor, and keeps the heavier plunder for a day when Maugrukh slips on the stair. He nurses an old grudge against Kuragh of the Akheth, who once stole his share of a Morgul-train."),
    ("lord_M12_1", "aom_lord_M12_1_bio", "Ruklash holds his banner in the slag-pits and forges of Gorgoroth, under the red light of Orodruin, where his sire was a taskmaster of the bellows. He clawed up out of the labour-gangs by breaking the captain who had flogged him as a snaga, and dragged the body up the slag-heap so the smiths would see who walked down. The men of Gondor know nothing of him yet, but the smith-orcs name him the Hammer of the Pit. He despises the Arki of Udun, who he says do nothing but stand in lines and be counted."),
    ("lord_M18_1", "aom_lord_M18_1_bio", "Kuragh of the Akheth is Ruklash's foremost uruk in the Gorgoroth forges, and the smith-snaga fear him more than the bellows. Plunder is his sickness, and he has been known to march his company up the Morgul road on private errands of robbery. He took a fat share from a Cirith Ungol train two winters past, and Thulnar has not forgotten it. He watches Ruklash for any sign of weakness, and reckons that a hammer-captain who tires at the anvil will not last a march to the Black Gate."),
    ("lord_M13_1", "aom_lord_M13_1_bio", "Thurak stands sentinel at the Morannon, the iron Black Gate where the legions of Lugburz pour out into the northern wastes. Fear of the Great Eye is the marrow of him, for he was once flogged on the gate-stones for letting a tark scout slip past in the dusk. Since that day he has not slept the whole night through, and his uruks know better than to be found drowsing on the wall. The Brughash of Durzan he watches with a particular dread, for they keep their own counsel in the south and do not answer the muster as swiftly as the Gate would like."),
    ("lord_M14_1", "aom_lord_M14_1_bio", "Uznash is an overseer-captain of the Nurn slave-pens, where the thralls of Harad and the East break their backs to feed the legions. He came up through the whip-gangs, and his hatred of Men is a cold practiced thing, learned on the fields and not in any battle. The horse-boys of the Mark have never heard of him, and that is a wound he means to repair when the great muster comes. He has an old quarrel with Gulnak of the Arki, who named his uruks farm-snaga at a Udun counting."),
    ("lord_M15_1", "aom_lord_M15_1_bio", "Durzan commands a Tower-guard company in the shadow of Barad-dur itself, chosen for cunning over bulk by an officer of the Mouth's household. Payback is the engine of him, for a Morgul-captain reported his company light at a muster three years past and his banner was nearly broken for it. That captain is dead now in a stair-quarrel none can quite explain, and Durzan has been seen at the Mouth's gate more than once since. He keeps a particular eye on the Cirith Ungol garrison, and on Maugrukh's accounting of the dead."),
]


def main():
    # Sanity check: no XML special chars in any prose
    bad = []
    for hid, key, prose in BIOS:
        for ch in '<>&"':
            if ch in prose:
                bad.append((hid, ch))
        if "—" in prose or "–" in prose:
            bad.append((hid, "em-dash/en-dash"))
        if "‘" in prose or "’" in prose or "“" in prose or "”" in prose:
            bad.append((hid, "curly-quote"))
    if bad:
        print("FATAL: XML-unsafe characters in prose:", file=sys.stderr)
        for hid, ch in bad:
            print(f"  {hid}: {ch!r}", file=sys.stderr)
        sys.exit(2)

    text = HEROES_XML.read_text(encoding="utf-8")
    original_text = text
    applied = []
    skipped_already_has_text = []
    not_found = []

    for hid, key, prose in BIOS:
        # Match <Hero ... id="HID" ... /> non-greedy, capturing the whole block up to />
        pattern = re.compile(
            r'(<Hero\b[^>]*?\bid="' + re.escape(hid) + r'"[^>]*?)(/>)',
            re.DOTALL,
        )
        m = pattern.search(text)
        if not m:
            not_found.append(hid)
            continue
        block_attrs = m.group(1)
        if 'text="' in block_attrs:
            skipped_already_has_text.append(hid)
            continue
        # Insert text="..." before the closing />
        # Match existing trailing-tab indentation: heroes.xml uses tab-indented attributes
        # Use \n\t\t to align with sibling attributes (matches existing lord_1_60 pattern).
        replacement = f'{block_attrs}text="{{={key}}}{prose}" {m.group(2)}'
        # But block_attrs may already end with a space/newline; preserve trailing whitespace
        # Use a regex that captures trailing whitespace separately
        pattern2 = re.compile(
            r'(<Hero\b[^>]*?\bid="' + re.escape(hid) + r'"[^>]*?)(\s*)(/>)',
            re.DOTALL,
        )
        m2 = pattern2.search(text)
        if not m2:
            not_found.append(hid)
            continue
        block_attrs2 = m2.group(1)
        trailing_ws = m2.group(2)
        # Determine indent: look at the LAST attribute's indentation (newline + tabs)
        # Most lines look like:  \n\t\tattr="..."
        # We want the new text= line to have the same indent.
        # Find the last "\n[\t ]*" in block_attrs2 — that's the indentation
        indent_match = re.search(r'\n([\t ]*)\S[^\n]*$', block_attrs2)
        indent = indent_match.group(1) if indent_match else "\t\t"
        new_block = f'{block_attrs2}\n{indent}text="{{={key}}}{prose}"{trailing_ws}{m2.group(3)}'
        text = text[:m2.start()] + new_block + text[m2.end():]
        applied.append(hid)

    if not_found:
        print("FATAL: heroes not found in heroes.xml:", file=sys.stderr)
        for hid in not_found:
            print(f"  {hid}", file=sys.stderr)
        sys.exit(3)

    if text == original_text:
        print("WARN: no changes made.", file=sys.stderr)
        sys.exit(4)

    HEROES_XML.write_text(text, encoding="utf-8")

    print(f"Applied: {len(applied)}")
    print(f"Skipped (already had text=): {len(skipped_already_has_text)}")
    if skipped_already_has_text:
        for hid in skipped_already_has_text:
            print(f"  {hid}")
    print(f"Expected total bios in table: {len(BIOS)}")


if __name__ == "__main__":
    main()
