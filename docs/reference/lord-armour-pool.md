# Lord armour pool per culture

Written on 2026-11-15 by `python tools/fix_lord_kits.py --pool-md <path> --values planned` (lord kit, #1271). Armour is head/body/arm/leg from the restat dry run (planned values) when it ran; "family" is the look family the assignment groups variants by; "lowest troop" is the lowest reference troop that wears the piece in a battle set (a troop the reference leaves out, such as a Harad mumak rider, is not counted); "templates" are the generic lord templates that wear it, the promoter clones left out (each clone wears the pieces of its source template, or those its promoter item map sends them to).

## aserai

Folders: harad. Families: 4. Pieces per slot: Head 5, Body 5, Cape 4, Gloves 0, Leg 2.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | hr_st | `taom_lord_sk_hr_st_helmet_heavy_a` | 44/0/0/0 | none | harad_bat_template_medium_b |
| Head | hr_st | `taom_lord_sk_hr_st_helmet_heavy_b` | 44/0/0/0 | none | harad_bat_template_medium_d |
| Head | hr_st | `taom_lord_sk_hr_st_helmet_heavy_c` | 44/0/0/0 | none | harad_bat_template_medium_a |
| Head | hr_st | `taom_lord_sk_hr_st_helmet_heavy_d` | 44/0/0/0 | none | harad_bat_template_medium_c |
| Head | sg_st | `taom_lord_sk_sg_st_helmet_heavy_d` | 44/0/0/0 | none | harad_bat_template_medium_e |
| Body | hr_st_cav | `taom_lord_sk_hr_st_chest_cav_heavy_b` | 0/43/13/17 | none | harad_bat_template_medium_e |
| Body | hr_st_inf | `taom_lord_sk_hr_st_chest_inf_chest_heavy_b` | 0/43/13/17 | none | harad_bat_template_medium_d |
| Body | hr_st_inf | `taom_lord_sk_hr_st_chest_inf_chest_light_a` | 0/43/13/17 | none | harad_bat_template_medium_a |
| Body | hr_st_inf | `taom_lord_sk_hr_st_chest_inf_chest_med_a` | 0/43/13/17 | none | harad_bat_template_medium_b |
| Body | hr_st_inf | `taom_lord_sk_hr_st_chest_inf_chest_med_b` | 0/43/13/17 | none | harad_bat_template_medium_c |
| Cape | hr_st_cav | `sk_hr_st_pauld_cav_elite_a` | 0/12/10/0 | none | harad_bat_template_medium_e |
| Cape | hr_st_inf | `sk_hr_st_pauld_inf_elite_a` | 0/12/10/0 | none | harad_bat_template_medium_a |
| Cape | hr_st_inf | `sk_hr_st_pauld_inf_elite_b` | 0/11/9/0 | none | harad_bat_template_medium_d |
| Cape | hr_st_inf | `taom_lord_sk_hr_st_pauld_inf_med_a` | 0/12/11/0 | none | harad_bat_template_medium_b, harad_bat_template_medium_c |
| Leg | hr_st | `taom_lord_sk_hr_st_grvs_light_a` | 0/0/0/19 | none | harad_bat_template_medium_a, harad_bat_template_medium_e |
| Leg | hr_st | `taom_lord_sk_hr_st_grvs_med_a` | 0/0/0/19 | none | harad_bat_template_medium_b, harad_bat_template_medium_c, harad_bat_template_medium_d |

## bluecraig

Folders: mordor. Families: 1. Pieces per slot: Head 0, Body 1, Cape 0, Gloves 0, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Body | md_orc_inf | `sk_md_orc_inf_chest_heavy_a` | 0/32/10/0 | none | bluecraig_bat_template_medium_a, goblin_bat_template_medium_a, goblin_bat_template_medium_d, mistymountainorcs_bat_template_medium_a, mistymountainorcs_bat_template_medium_d |

## dolguldur

Folders: dol_guldur, rhun. Families: 5. Pieces per slot: Head 24, Body 4, Cape 3, Gloves 5, Leg 8.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | dg_khml_cav | `sk_dg_khml_helmet_cav_lord_a` | 43/0/0/0 | none | - |
| Head | dg_khml_cav | `sk_dg_khml_helmet_cav_lord_b` | 43/0/0/0 | none | - |
| Head | dg_khml_cav | `sk_dg_khml_helmet_cav_lord_c` | 43/0/0/0 | none | - |
| Head | dg_khml_cav | `sk_dg_khml_helmet_cav_lord_d` | 43/0/0/0 | none | - |
| Head | dg_khml_cav | `sk_dg_khml_helmet_cav_lord_e` | 43/0/0/0 | none | - |
| Head | dg_khml_cav | `sk_dg_khml_helmet_cav_lord_f` | 43/0/0/0 | none | - |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_a` | 46/0/0/0 | none | rhun_bat_template_medium_f |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_b` | 43/0/0/0 | none | - |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_c` | 43/0/0/0 | none | - |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_d` | 43/0/0/0 | none | - |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_a` | 46/0/0/0 | none | rhun_bat_template_medium_c |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_b` | 43/0/0/0 | none | - |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_c` | 43/0/0/0 | none | - |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_d` | 43/0/0/0 | none | - |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_e` | 43/0/0/0 | none | - |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_f` | 43/0/0/0 | none | - |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_a` | 46/0/0/0 | none | dolguldur_bat_template_medium_a, dolguldur_bat_template_medium_i |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_b` | 46/0/0/0 | none | dolguldur_bat_template_medium_b, dolguldur_bat_template_medium_j |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_c` | 46/0/0/0 | none | dolguldur_bat_template_medium_c |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_d` | 46/0/0/0 | none | dolguldur_bat_template_medium_d |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_e` | 46/0/0/0 | none | dolguldur_bat_template_medium_e |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_f` | 46/0/0/0 | none | dolguldur_bat_template_medium_f |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_g` | 46/0/0/0 | none | dolguldur_bat_template_medium_g |
| Head | dg_uruk | `sk_dg_uruk_helmet_lord_h` | 46/0/0/0 | none | dolguldur_bat_template_medium_h |
| Body | dg_khml | `sk_dg_khml_chest_lord_a` | 0/44/13/18 | none | rhun_bat_template_medium_c |
| Body | dg_khml | `sk_dg_khml_chest_lord_b` | 0/41/12/16 | none | - |
| Body | dg_uruk | `sk_dg_uruk_chest_elite_d` | 0/44/13/18 | none | dolguldur_bat_template_medium_d, dolguldur_bat_template_medium_j |
| Body | dg_uruk | `sk_dg_uruk_chest_elite_f` | 0/44/13/18 | none | dolguldur_bat_template_medium_f |
| Cape | dg_uruk | `sk_dg_uruk_pauldron_elite_cape_b` | 0/12/5/0 | none | dolguldur_bat_template_medium_b, dolguldur_bat_template_medium_h |
| Cape | dg_uruk | `sk_dg_uruk_pauldron_elite_cape_c` | 0/12/5/0 | none | dolguldur_bat_template_medium_c, dolguldur_bat_template_medium_i |
| Cape | dg_uruk | `sk_dg_uruk_pauldron_elite_cape_f` | 0/12/5/0 | none | dolguldur_bat_template_medium_f |
| Gloves | dg_uruk | `sk_dg_uruk_bracer_elite_c` | 0/0/22/0 | none | dolguldur_bat_template_medium_c |
| Gloves | dg_uruk | `sk_dg_uruk_bracer_elite_f` | 0/0/22/0 | none | dolguldur_bat_template_medium_f |
| Gloves | dg_uruk | `sk_dg_uruk_bracer_elite_h` | 0/0/22/0 | none | dolguldur_bat_template_medium_h |
| Gloves | dg_uruk | `sk_dg_uruk_bracer_elite_i` | 0/0/22/0 | none | dolguldur_bat_template_medium_i |
| Gloves | dg_uruk | `sk_dg_uruk_bracer_elite_j` | 0/0/22/0 | none | dolguldur_bat_template_medium_j |
| Leg | dg_uruk | `sk_dg_uruk_boots_elite_d` | 0/0/0/20 | none | dolguldur_bat_template_medium_d, dolguldur_bat_template_medium_j |
| Leg | dg_uruk | `sk_dg_uruk_boots_elite_f` | 0/0/0/20 | none | dolguldur_bat_template_medium_f |
| Leg | dg_uruk | `sk_dg_uruk_boots_lord_a` | 0/0/0/20 | none | dolguldur_bat_template_medium_a |
| Leg | dg_uruk | `sk_dg_uruk_boots_lord_b` | 0/0/0/20 | none | dolguldur_bat_template_medium_b |
| Leg | dg_uruk | `sk_dg_uruk_boots_lord_c` | 0/0/0/20 | none | dolguldur_bat_template_medium_c |
| Leg | dg_uruk | `sk_dg_uruk_boots_lord_d` | 0/0/0/20 | none | dolguldur_bat_template_medium_e |
| Leg | dg_uruk | `sk_dg_uruk_boots_lord_e` | 0/0/0/20 | none | dolguldur_bat_template_medium_g |
| Leg | dg_uruk | `sk_dg_uruk_boots_lord_f` | 0/0/0/20 | none | dolguldur_bat_template_medium_h |

## empire

Folders: dunland. Families: 4. Pieces per slot: Head 14, Body 8, Cape 0, Gloves 0, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_a` | 41/0/0/0 | none | dunland_bat_template_medium_a |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_b` | 41/0/0/0 | none | dunland_bat_template_medium_b |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_c` | 41/0/0/0 | none | dunland_bat_template_medium_c |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_d` | 41/0/0/0 | none | dunland_bat_template_medium_d |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_e` | 38/0/0/0 | none | - |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_f` | 38/0/0/0 | none | - |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_g` | 38/0/0/0 | none | - |
| Head | addlnnu_acdehr | `dunland_caerdh_helmet_lord_h` | 38/0/0/0 | none | - |
| Head | addlnnu_wulf | `dunland_wulf_helmet_lord_a` | 41/0/0/0 | none | dunland_bat_template_medium_e |
| Head | addlnnu_wulf | `dunland_wulf_helmet_lord_b` | 38/0/0/0 | none | - |
| Head | addlnnu_wulf | `dunland_wulf_helmet_lord_c` | 38/0/0/0 | none | - |
| Head | addlnnu_wulf | `dunland_wulf_helmet_lord_d` | 38/0/0/0 | none | - |
| Head | addlnnu_wulf | `dunland_wulf_helmet_lord_e` | 38/0/0/0 | none | - |
| Head | addlnnu_wulf | `dunland_wulf_helmet_lord_f` | 38/0/0/0 | none | - |
| Body | addlnnu_acdehr_aachiilmn | `dunland_caerdh_chainmail_lord_a` | 0/40/10/0 | none | dunland_bat_template_medium_a |
| Body | addlnnu_acdehr_aachiilmn | `dunland_caerdh_chainmail_lord_b` | 0/40/10/0 | none | dunland_bat_template_medium_b |
| Body | addlnnu_acdehr_aachiilmn | `dunland_caerdh_chainmail_lord_c` | 0/40/10/0 | none | dunland_bat_template_medium_c |
| Body | addlnnu_acdehr_aachiilmn | `dunland_caerdh_chainmail_lord_d` | 0/40/10/0 | none | dunland_bat_template_medium_d |
| Body | addlnnu_wulf_aaceillms | `dunland_wulf_scalemail_lord_a` | 0/40/10/0 | none | dunland_bat_template_medium_e |
| Body | addlnnu_wulf_aaceillms | `dunland_wulf_scalemail_lord_b` | 0/37/9/0 | none | - |
| Body | addlnnu_wulf_aaceillms | `dunland_wulf_scalemail_lord_c` | 0/37/9/0 | none | - |
| Body | addlnnu_wulf_aaceillms | `dunland_wulf_scalemail_lord_d` | 0/37/9/0 | none | - |

## erebor

Folders: erebor, iron_hills. Families: 2. Pieces per slot: Head 10, Body 0, Cape 0, Gloves 0, Leg 2.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_a` | 54/0/0/0 | erebor_noble_gate_warden L41 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_a2` | 54/0/0/0 | ironpass_ram_marshal L41 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_b` | 54/0/0/0 | erebor_noble_gate_warden L41 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_b2` | 54/0/0/0 | erebor_noble_royal_warden L46 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_c` | 54/0/0/0 | erebor_noble_gate_warden L41 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_c2` | 54/0/0/0 | erebor_noble_royal_warden L46 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_d` | 54/0/0/0 | erebor_noble_gate_warden L41 | - |
| Head | adfrw_beeorr_aelpt | `sk_dwarf_erebor_helmet_plate_lord_d2` | 54/0/0/0 | erebor_noble_royal_warden L46 | - |
| Head | adfrw_iron | `sk_dwarf_iron_helmet_lord_d1` | 54/0/0/0 | iron_hills_noble_royal_warden L46 | - |
| Head | adfrw_iron | `sk_dwarf_iron_helmet_lord_d2` | 54/0/0/0 | none | - |
| Leg | adfrw_iron | `sk_dwarf_iron_boots_lord_c1` | 0/0/0/23 | iron_hills_noble_royal_warden L46 | - |
| Leg | adfrw_iron | `sk_dwarf_iron_boots_lord_c2` | 0/0/0/23 | none | - |

## goblin

Folders: mordor. Families: 2. Pieces per slot: Head 0, Body 1, Cape 0, Gloves 1, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Body | md_orc_inf | `sk_md_orc_inf_chest_heavy_a` | 0/32/10/0 | none | bluecraig_bat_template_medium_a, goblin_bat_template_medium_a, goblin_bat_template_medium_d, mistymountainorcs_bat_template_medium_a, mistymountainorcs_bat_template_medium_d |
| Gloves | md_orc | `sk_md_orc_bracer_heavy_a` | 0/0/12/0 | none | goblin_bat_template_medium_c |

## gondor

Folders: gondor. Families: 22. Pieces per slot: Head 17, Body 14, Cape 11, Gloves 1, Leg 2.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | ehiiilnt | `ithilien_hood` | 15/0/0/0 | none | - |
| Head | gd_ith_belno | `sk_gd_ith_noble_helmet_heavy_a` | 48/0/0/0 | gondor_ith_moon_guard L46 | - |
| Head | gd_ith_belno | `sk_gd_ith_noble_helmet_heavy_b` | 48/0/0/0 | gondor_ith_captain L41 | - |
| Head | gd_lam_nob | `sk_gd_lam_nob_helmet_lord_a` | 51/0/0/0 | none | gondor_bat_template_medium_a |
| Head | gd_lam_nob | `sk_gd_lam_nob_helmet_lord_b` | 51/0/0/0 | none | gondor_bat_template_medium_b |
| Head | gd_lam_nob | `sk_gd_lam_nob_helmet_lord_c` | 48/0/0/0 | none | - |
| Head | gd_lam_nob | `sk_gd_lam_nob_helmet_lord_d` | 48/0/0/0 | none | - |
| Head | gd_lin | `sk_gd_lin_helmet_lord_a` | 51/0/0/0 | none | gondor_bat_template_medium_d |
| Head | gd_lin | `sk_gd_lin_helmet_lord_b` | 51/0/0/0 | none | - |
| Head | gd_lon | `sk_gd_lon_helmet_elite_a` | 48/0/0/0 | gondor_lg_haven_guard L41 | - |
| Head | gd_lon | `sk_gd_lon_helmet_elite_b` | 48/0/0/0 | gondor_lg_haven_guard L41 | - |
| Head | gd_mns_fnotu | `sk_gd_mns_fount_helmet_heavy_a` | 48/0/0/0 | gondor_mt_fountain_guard L46 | gondor_bat_template_medium_c |
| Head | gd_mns_fnotu | `sk_gd_mns_fount_helmet_heavy_b` | 48/0/0/0 | gondor_mt_fountain_guard L46 | - |
| Head | gd_pin_belno_cav | `sk_gd_pin_noble_cav_helmet_elite_a` | 48/0/0/0 | gondor_arn_hill_knight L41 | - |
| Head | gd_pin_belno_cav | `sk_gd_pin_noble_cav_helmet_elite_b` | 48/0/0/0 | gondor_arn_hill_knight L41 | - |
| Head | gd_rin_nob | `sk_gd_rin_nob_helmet_lord_a` | 48/0/0/0 | none | - |
| Head | gd_rin_nob | `sk_gd_rin_nob_helmet_lord_b` | 48/0/0/0 | none | - |
| Body | gd_cair_inf | `sk_gd_cair_inf_chest_elite_a` | 0/49/15/0 | none | - |
| Body | gd_lam_inf | `sk_gd_lam_inf_chest_lord_a` | 0/49/15/0 | none | gondor_bat_template_medium_a |
| Body | gd_lam_nob | `sk_gd_lam_nob_chest_lord_a` | 0/49/15/0 | none | gondor_bat_template_medium_b |
| Body | gd_lam_nob_aachiilmn | `sk_gd_lam_nob_chainmail_a` | 0/46/0/0 | gondor_cal_noble L21 | - |
| Body | gd_lin | `sk_gd_lin_chest_elite_a` | 0/49/15/0 | none | - |
| Body | gd_lon | `sk_gd_lon_chest_lord_a` | 0/49/15/0 | none | gondor_bat_template_medium_d |
| Body | gd_los_nob | `sk_gd_los_nob_chest_lord_a` | 0/49/15/0 | none | - |
| Body | gd_los_nob_aachiilmn | `sk_gd_los_nob_chainmail_a` | 0/31/0/0 | gondor_loss_noble L16 | - |
| Body | gd_mns_fnotu | `sk_gd_mns_fount_chest_elite_a` | 0/46/14/0 | gondor_mt_fountain_guard L46 | - |
| Body | gd_mns_fnotu | `sk_gd_mns_fount_chest_heavy_a` | 0/48/14/0 | none | gondor_bat_template_medium_c |
| Body | gd_osg_inf | `sk_gd_osg_inf_chest_elite_a` | 0/49/15/0 | none | - |
| Body | gd_pin_belno_aachiilmn | `sk_gd_pin_noble_chainmail_a` | 0/31/0/0 | gondor_arn_noble L16 | - |
| Body | gd_pin_nob | `sk_gd_pin_nob_chest_elite_a` | 0/46/14/0 | gondor_arn_hill_knight L41 | - |
| Body | gd_pin_nob | `sk_gd_pin_nob_chest_elite_b` | 0/49/15/0 | none | - |
| Cape | ehiiilnt_var | `ithilien_cloak_var` | 0/14/13/0 | none | - |
| Cape | gd_ano_belno | `sk_gd_ano_cape_noble_a` | 0/13/11/0 | gondor_ith_watcher L26 | - |
| Cape | gd_ano_belno | `sk_gd_ano_cape_noble_b` | 0/13/11/0 | gondor_mt_trainee L26 | - |
| Cape | gd_ano_belno | `sk_gd_ano_pauld_noble_elite_a` | 0/14/13/0 | none | - |
| Cape | gd_ano_belno | `sk_gd_ano_pauld_noble_elite_b` | 0/14/13/0 | none | - |
| Cape | gd_ano_belno | `sk_gd_ano_pauld_noble_heavy_a` | 0/14/12/0 | none | gondor_bat_template_medium_d |
| Cape | gd_ano_fnotu | `sk_gd_ano_pauld_cape_fount_elite_a` | 0/13/12/0 | gondor_mt_fountain_guard L46 | gondor_bat_template_medium_c |
| Cape | gd_lam_belno | `sk_gd_lam_cape_noble_a` | 0/13/11/0 | gondor_cal_noble L21 | gondor_bat_template_medium_a |
| Cape | gd_lam_belno | `sk_gd_lam_pauld_cape_noble_lord_a` | 0/14/13/0 | none | gondor_bat_template_medium_b |
| Cape | gd_lin_belno | `sk_gd_lin_pauld_cape_noble_elite_a` | 0/14/13/0 | none | - |
| Cape | gd_los_nob | `sk_gd_los_pauld_cape_nob_lord_a` | 0/14/13/0 | none | - |
| Gloves | gd_sere | `sk_gd_sere_bracer_lord_a` | 0/0/23/0 | gondor_ser_coastwarden L36 | gondor_bat_template_medium_a, gondor_bat_template_medium_b, gondor_bat_template_medium_d |
| Leg | ehiiilnt | `ithilien_boots_heavy` | 0/0/0/22 | none | - |
| Leg | gd_sere | `sk_gd_sere_grvs_lord_a` | 0/0/0/21 | gondor_ser_coastwarden L36 | gondor_bat_template_medium_a, gondor_bat_template_medium_b, gondor_bat_template_medium_d |

Pool pieces a troop under level 41 wears, so the restat prices them at that troop's band (no mesh ladder substitute: the ids carry no tier token):

| Piece | Troop | Level |
|---|---|---:|
| `sk_gd_ano_cape_noble_a` | gondor_ith_watcher | 26 |
| `sk_gd_ano_cape_noble_b` | gondor_ith_veteran | 31 |
| `sk_gd_ano_cape_noble_b` | gondor_mt_trainee | 26 |
| `sk_gd_ano_cape_noble_b` | gondor_mt_veteran | 31 |
| `sk_gd_lam_cape_noble_a` | gondor_cal_noble | 21 |
| `sk_gd_lam_cape_noble_a` | gondor_cal_swordsman | 26 |
| `sk_gd_lam_nob_chainmail_a` | gondor_cal_noble | 21 |
| `sk_gd_los_nob_chainmail_a` | gondor_loss_noble | 16 |
| `sk_gd_pin_noble_chainmail_a` | gondor_arn_noble | 16 |

## gundabad

Folders: gundabad, mordor. Families: 1. Pieces per slot: Head 1, Body 6, Cape 2, Gloves 0, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | gb_uruk | `sk_gb_uruk_helmet_elite_d` | 44/0/0/0 | gundabad_dread_rider_of_the_tower L41 | gundabad_bat_template_medium_d |
| Body | gb_uruk | `sk_gb_uruk_chest_lord_a` | 0/45/14/18 | none | gundabad_bat_template_medium_a, gundabad_bat_template_medium_g |
| Body | gb_uruk | `sk_gb_uruk_chest_lord_b` | 0/45/14/18 | none | gundabad_bat_template_medium_b, gundabad_bat_template_medium_h |
| Body | gb_uruk | `sk_gb_uruk_chest_lord_c` | 0/42/13/17 | gundabad_dread_rider_of_the_tower L41 | gundabad_bat_template_medium_c |
| Body | gb_uruk | `sk_gb_uruk_chest_lord_d` | 0/45/14/18 | none | gundabad_bat_template_medium_d |
| Body | gb_uruk | `sk_gb_uruk_chest_lord_e` | 0/45/14/18 | none | gundabad_bat_template_medium_e |
| Body | gb_uruk | `sk_gb_uruk_chest_lord_f` | 0/45/14/18 | none | gundabad_bat_template_medium_f |
| Cape | gb_uruk | `sk_gb_uruk_pauldron_cape_elite_b` | 0/13/7/0 | none | gundabad_bat_template_medium_b, gundabad_bat_template_medium_f |
| Cape | gb_uruk | `sk_gb_uruk_pauldron_cape_elite_d` | 0/13/7/0 | none | gundabad_bat_template_medium_d, gundabad_bat_template_medium_h |

## isengard

Folders: isengard, mordor. Families: 5. Pieces per slot: Head 3, Body 2, Cape 3, Gloves 3, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | uruk_hai_aacinpt | `sk_uruk_hai_helmet_captain_ar1` | 45/0/0/0 | none | isengard_bat_template_medium_d |
| Head | uruk_hai_bers | `sk_uruk_hai_helmet_bers_ar1` | 38/0/0/0 | none | isengard_bat_template_medium_e |
| Head | uruk_hai_nazg | `sk_uruk_hai_helmet_nazg_ar1` | 43/0/0/0 | none | isengard_bat_template_medium_f |
| Body | uruk_hai_aelpt | `sk_uruk_hai_plate_elite_a3` | 0/43/13/15 | none | isengard_bat_template_medium_a, isengard_bat_template_medium_c, isengard_bat_template_medium_e |
| Body | uruk_hai_aelpt | `sk_uruk_hai_plate_elite_b3` | 0/43/13/17 | none | isengard_bat_template_medium_b, isengard_bat_template_medium_d, isengard_bat_template_medium_f |
| Cape | uruk_hai | `sk_uruk_hai_pauldron_heavy_b3` | 0/12/3/0 | none | - |
| Cape | uruk_hai | `sk_uruk_hai_pauldron_heavy_br2` | 0/12/3/0 | none | isengard_bat_template_medium_a, isengard_bat_template_medium_c, isengard_bat_template_medium_e |
| Cape | uruk_hai | `sk_uruk_hai_pauldron_heavy_br3` | 0/12/3/0 | none | isengard_bat_template_medium_b, isengard_bat_template_medium_d, isengard_bat_template_medium_f |
| Gloves | uruk_hai | `sk_uruk_hai_bracer_elite_a3` | 0/0/20/0 | orthanc_bodyguard L41 | - |
| Gloves | uruk_hai | `sk_uruk_hai_bracer_elite_ar2` | 0/0/21/0 | none | isengard_bat_template_medium_a, isengard_bat_template_medium_c, isengard_bat_template_medium_e |
| Gloves | uruk_hai | `sk_uruk_hai_bracer_elite_br2` | 0/0/21/0 | none | isengard_bat_template_medium_b, isengard_bat_template_medium_d, isengard_bat_template_medium_f |

## khuzait

Folders: rhun. Families: 19. Pieces per slot: Head 55, Body 9, Cape 3, Gloves 2, Leg 2.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | dg_khml_arch | `sk_dg_khml_helmet_arch_lord_a` | 43/0/0/0 | none | - |
| Head | dg_khml_arch | `sk_dg_khml_helmet_arch_lord_b` | 43/0/0/0 | none | - |
| Head | dg_khml_arch | `sk_dg_khml_helmet_arch_lord_c` | 43/0/0/0 | none | - |
| Head | dg_khml_arch | `sk_dg_khml_helmet_arch_lord_d` | 43/0/0/0 | none | - |
| Head | dg_khml_arch | `sk_dg_khml_helmet_arch_lord_e` | 43/0/0/0 | none | - |
| Head | dg_khml_arch | `sk_dg_khml_helmet_arch_lord_f` | 43/0/0/0 | none | - |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_a` | 46/0/0/0 | none | rhun_bat_template_medium_f |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_b` | 43/0/0/0 | none | - |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_c` | 43/0/0/0 | none | - |
| Head | dg_khml_cult | `sk_dg_khml_helmet_cult_lord_d` | 43/0/0/0 | none | - |
| Head | dg_khml_inf | `sk_dg_khml_helmet_inf_lord_a` | 46/0/0/0 | none | rhun_bat_template_medium_c |
| Head | rh_drag_arch | `sk_rh_drag_helmet_arch_lord_a` | 48/0/0/0 | none | - |
| Head | rh_drag_arch | `sk_rh_drag_helmet_arch_lord_b` | 48/0/0/0 | none | - |
| Head | rh_drag_arch | `sk_rh_drag_helmet_arch_lord_c` | 48/0/0/0 | none | - |
| Head | rh_drag_arch | `sk_rh_drag_helmet_arch_lord_d` | 48/0/0/0 | none | - |
| Head | rh_drag_arch | `sk_rh_drag_helmet_arch_lord_e` | 48/0/0/0 | none | - |
| Head | rh_drag_arch | `sk_rh_drag_helmet_arch_lord_f` | 48/0/0/0 | none | - |
| Head | rh_drag_cav | `sk_rh_drag_helmet_cav_lord_a` | 51/0/0/0 | none | rhun_bat_template_medium_e |
| Head | rh_drag_cav | `sk_rh_drag_helmet_cav_lord_b` | 48/0/0/0 | none | - |
| Head | rh_drag_cav | `sk_rh_drag_helmet_cav_lord_c` | 48/0/0/0 | none | - |
| Head | rh_drag_cav | `sk_rh_drag_helmet_cav_lord_d` | 48/0/0/0 | none | - |
| Head | rh_drag_cav | `sk_rh_drag_helmet_cav_lord_e` | 48/0/0/0 | none | - |
| Head | rh_drag_cav | `sk_rh_drag_helmet_cav_lord_f` | 48/0/0/0 | none | - |
| Head | rh_drag_cult | `sk_rh_drag_helmet_cult_lord_a` | 48/0/0/0 | none | - |
| Head | rh_drag_cult | `sk_rh_drag_helmet_cult_lord_b` | 48/0/0/0 | none | - |
| Head | rh_drag_cult | `sk_rh_drag_helmet_cult_lord_c` | 48/0/0/0 | none | - |
| Head | rh_drag_cult | `sk_rh_drag_helmet_cult_lord_d` | 48/0/0/0 | none | - |
| Head | rh_drag_inf | `sk_rh_drag_helmet_inf_lord_a` | 51/0/0/0 | none | rhun_bat_template_medium_b |
| Head | rh_drag_inf | `sk_rh_drag_helmet_inf_lord_b` | 48/0/0/0 | none | - |
| Head | rh_drag_inf | `sk_rh_drag_helmet_inf_lord_c` | 48/0/0/0 | none | - |
| Head | rh_drag_inf | `sk_rh_drag_helmet_inf_lord_d` | 48/0/0/0 | none | - |
| Head | rh_drag_inf | `sk_rh_drag_helmet_inf_lord_e` | 48/0/0/0 | none | - |
| Head | rh_drag_inf | `sk_rh_drag_helmet_inf_lord_f` | 48/0/0/0 | none | - |
| Head | rh_loke_arch | `sk_rh_loke_helmet_arch_lord_a` | 48/0/0/0 | none | - |
| Head | rh_loke_arch | `sk_rh_loke_helmet_arch_lord_b` | 48/0/0/0 | none | - |
| Head | rh_loke_arch | `sk_rh_loke_helmet_arch_lord_c` | 48/0/0/0 | none | - |
| Head | rh_loke_arch | `sk_rh_loke_helmet_arch_lord_d` | 48/0/0/0 | none | - |
| Head | rh_loke_arch | `sk_rh_loke_helmet_arch_lord_e` | 48/0/0/0 | none | - |
| Head | rh_loke_arch | `sk_rh_loke_helmet_arch_lord_f` | 48/0/0/0 | none | - |
| Head | rh_loke_cav | `sk_rh_loke_helmet_cav_lord_a` | 51/0/0/0 | none | rhun_bat_template_medium_d |
| Head | rh_loke_cav | `sk_rh_loke_helmet_cav_lord_b` | 48/0/0/0 | none | - |
| Head | rh_loke_cav | `sk_rh_loke_helmet_cav_lord_c` | 48/0/0/0 | none | - |
| Head | rh_loke_cav | `sk_rh_loke_helmet_cav_lord_d` | 48/0/0/0 | none | - |
| Head | rh_loke_cav | `sk_rh_loke_helmet_cav_lord_e` | 48/0/0/0 | none | - |
| Head | rh_loke_cav | `sk_rh_loke_helmet_cav_lord_f` | 48/0/0/0 | none | - |
| Head | rh_loke_cult | `sk_rh_loke_helmet_cult_lord_a` | 48/0/0/0 | none | - |
| Head | rh_loke_cult | `sk_rh_loke_helmet_cult_lord_b` | 48/0/0/0 | none | - |
| Head | rh_loke_cult | `sk_rh_loke_helmet_cult_lord_c` | 48/0/0/0 | none | - |
| Head | rh_loke_cult | `sk_rh_loke_helmet_cult_lord_d` | 48/0/0/0 | none | - |
| Head | rh_loke_inf | `sk_rh_loke_helmet_inf_lord_a` | 51/0/0/0 | none | rhun_bat_template_medium_a |
| Head | rh_loke_inf | `sk_rh_loke_helmet_inf_lord_b` | 48/0/0/0 | none | - |
| Head | rh_loke_inf | `sk_rh_loke_helmet_inf_lord_c` | 48/0/0/0 | none | - |
| Head | rh_loke_inf | `sk_rh_loke_helmet_inf_lord_d` | 48/0/0/0 | none | - |
| Head | rh_loke_inf | `sk_rh_loke_helmet_inf_lord_e` | 48/0/0/0 | none | - |
| Head | rh_loke_inf | `sk_rh_loke_helmet_inf_lord_f` | 48/0/0/0 | none | - |
| Body | dg_khml | `sk_dg_khml_chest_lord_a` | 0/44/13/18 | none | rhun_bat_template_medium_c |
| Body | dg_khml | `sk_dg_khml_chest_lord_b` | 0/41/12/16 | none | - |
| Body | dg_khml_aaceillms | `sk_dg_khml_scalemail_lord_a` | 0/44/13/18 | none | rhun_bat_template_medium_f |
| Body | rh_drag | `sk_rh_drag_chest_lord_a` | 0/49/15/20 | none | rhun_bat_template_medium_b |
| Body | rh_drag | `sk_rh_drag_chest_lord_b` | 0/49/15/20 | none | rhun_bat_template_medium_e |
| Body | rh_drag_aaceillms | `sk_rh_drag_scalemail_lord_a` | 0/46/14/18 | none | - |
| Body | rh_loke | `sk_rh_loke_chest_lord_a` | 0/49/15/20 | none | rhun_bat_template_medium_a |
| Body | rh_loke | `sk_rh_loke_chest_lord_b` | 0/49/15/20 | none | rhun_bat_template_medium_d |
| Body | rh_loke_aaceillms | `sk_rh_loke_scalemail_lord_a` | 0/46/14/18 | none | - |
| Cape | dg_khml | `sk_dg_khml_cape_heavy_b` | 0/12/11/0 | none | rhun_bat_template_medium_f |
| Cape | dg_khml_aelpt | `sk_dg_khml_cape_plate_heavy_a` | 0/12/11/0 | none | rhun_bat_template_medium_c |
| Cape | rh_drag_aelpt | `sk_rh_drag_cape_plate_heavy_a` | 0/13/11/0 | dragon_wrath_obsidian_knight L46 | rhun_bat_template_medium_b |
| Gloves | dg_khml | `sk_dg_khml_bracer_heavy_a` | 0/0/21/0 | dg_khamul_veiled_marksman L41 | rhun_bat_template_medium_c |
| Gloves | dg_khml | `sk_dg_khml_bracer_heavy_c` | 0/0/21/0 | dg_khamul_veiled_marksman L41 | rhun_bat_template_medium_f |
| Leg | dg_khml_aelpt | `sk_dg_khml_grvs_plate_elite_a` | 0/0/0/18 | dg_khamul_shadow_guard L46 | rhun_bat_template_medium_c, rhun_bat_template_medium_f |
| Leg | rh_drag_aelpt | `sk_rh_drag_grvs_plate_elite_a` | 0/0/0/21 | dragon_wrath_obsidian_knight L46 | rhun_bat_template_medium_b, rhun_bat_template_medium_e |

## lindon

Folders: rivendell. Families: 1. Pieces per slot: Head 0, Body 9, Cape 0, Gloves 0, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Body | deeillnrv_lord | `rivendell_torso_lord1` | 0/55/16/22 | none | lindon_bat_template_medium_a, rivendell_bat_template_medium_a |
| Body | deeillnrv_lord | `rivendell_torso_lord1_silver` | 0/55/16/22 | none | rivendell_bat_template_medium_c |
| Body | deeillnrv_lord | `rivendell_torso_lord1_silvergold` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord2` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord2_silver` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord2_silvergold` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord3` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord3_silver` | 0/51/15/20 | lindon_noldorin_lancer L46 | - |
| Body | deeillnrv_lord | `rivendell_torso_lord3_silvergold` | 0/51/15/20 | lindon_noldorin_lancer L46 | - |

## lothlorien

Folders: lothlorien. Families: 3. Pieces per slot: Head 4, Body 3, Cape 3, Gloves 1, Leg 2.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | ll_ml | `sk_ll_ml_helmet_elite_a` | 52/0/0/0 | none | lothlorien_bat_template_medium_d |
| Head | ll_ml | `sk_ll_ml_helmet_elite_c` | 52/0/0/0 | none | - |
| Head | ll_mw | `sk_ll_mw_helmet_lord_a` | 52/0/0/0 | none | lothlorien_bat_template_medium_a |
| Head | ll_mw | `sk_ll_mw_helmet_lord_b` | 52/0/0/0 | none | lothlorien_bat_template_medium_e |
| Body | ll_gl | `sk_ll_gl_chest_elite_a` | 0/47/0/19 | lorien_gl_blademaster L41 | lothlorien_bat_template_medium_c |
| Body | ll_ml | `sk_ll_ml_chest_elite_a` | 0/50/0/20 | none | - |
| Body | ll_ml | `sk_ll_ml_chest_elite_b` | 0/50/0/20 | none | lothlorien_bat_template_medium_d |
| Cape | ll_gl | `sk_ll_gl_pauld_cape_heavy_a` | 0/13/12/0 | lorien_gl_blademaster L41 | lothlorien_bat_template_medium_b, lothlorien_bat_template_medium_c |
| Cape | ll_ml | `sk_ll_ml_pauld_cape_heavy_a` | 0/14/12/0 | lorien_gl_bowmaster L46 | lothlorien_bat_template_medium_d |
| Cape | ll_mw | `sk_ll_mw_pauld_elite_a` | 0/13/12/0 | lorien_mw_waystalker L41 | lothlorien_bat_template_medium_e |
| Gloves | ll_ml | `sk_ll_ml_bracer_heavy_a` | 0/0/25/0 | lorien_gl_highblade L46 | lothlorien_bat_template_medium_d |
| Leg | ll_ml | `sk_ll_ml_grvs_heavy_a` | 0/0/0/23 | lorien_gl_highblade L46 | lothlorien_bat_template_medium_d |
| Leg | ll_mw | `sk_ll_mw_grvs_heavy_a` | 0/0/0/21 | lorien_mw_dawnrider L41 | lothlorien_bat_template_medium_e |

## mirkwood

Folders: mirkwood. Families: 7. Pieces per slot: Head 5, Body 4, Cape 3, Gloves 3, Leg 4.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | mkwd_aacelp_t3 | `mkwd_palace_t3_helmet_green_hc` | 50/0/0/0 | mirkwood_palaceguard L51 | mirkwood_bat_template_medium_d |
| Head | mkwd_inf1_t3 | `mkwd_inf1_t3_helmet_gold_hc` | 50/0/0/0 | mirkwood_woodsman L41 | mirkwood_bat_template_medium_e |
| Head | mkwd_inf1_t4 | `mkwd_inf1_t4_helmet_gold_hc` | 50/0/0/0 | mirkwood_glaivesman L46 | mirkwood_bat_template_medium_c |
| Head | mkwd_inf3_t3 | `mkwd_inf3_t3_helmet_gold_hc` | 50/0/0/0 | mirkwood_guards L41 | mirkwood_bat_template_medium_a |
| Head | mkwd_inf3_t4 | `mkwd_inf3_t4_helmet_gold_hc` | 50/0/0/0 | mirkwood_swordsman L46 | mirkwood_bat_template_medium_b |
| Body | dikmoorw | `taom_lord_mirkwood_body` | 0/51/9/20 | none | mirkwood_bat_template_medium_c, mirkwood_bat_template_medium_d |
| Body | mkwd_inf3 | `mkwd_inf3_chest_silver` | 0/48/8/19 | mirkwood_rochenlas L41 | mirkwood_bat_template_medium_e |
| Body | mkwd_inf3 | `taom_lord_mkwd_inf3_chest` | 0/51/9/20 | none | mirkwood_bat_template_medium_a |
| Body | mkwd_inf3 | `taom_lord_mkwd_inf3_chest_bronze` | 0/51/9/20 | none | mirkwood_bat_template_medium_b |
| Cape | dikmoorw | `mirkwood_shoulder` | 0/13/11/0 | mirkwood_wardens L41 | mirkwood_bat_template_medium_c, mirkwood_bat_template_medium_d |
| Cape | mkwd_inf3 | `mkwd_inf3_pauldrons_silver` | 0/13/11/0 | mirkwood_rochenlas L41 | mirkwood_bat_template_medium_e |
| Cape | mkwd_inf3 | `taom_lord_mkwd_inf3_pauldrons_bronze` | 0/14/12/0 | none | mirkwood_bat_template_medium_b |
| Gloves | dikmoorw | `mirkwood_gloves` | 0/0/24/0 | mirkwood_wardens L41 | mirkwood_bat_template_medium_b, mirkwood_bat_template_medium_d |
| Gloves | mkwd_inf3 | `mkwd_inf3_vambraces_bronze` | 0/0/24/0 | mirkwood_guards L41 | mirkwood_bat_template_medium_a |
| Gloves | mkwd_inf3 | `mkwd_inf3_vambraces_silver` | 0/0/24/0 | mirkwood_rochenlas L41 | mirkwood_bat_template_medium_c, mirkwood_bat_template_medium_e |
| Leg | dikmoorw | `taom_lord_mirkwood_boots` | 0/0/0/23 | none | mirkwood_bat_template_medium_c, mirkwood_bat_template_medium_d |
| Leg | mkwd_inf3 | `mkwd_inf3_greaves_silver` | 0/0/0/22 | mirkwood_rochenlas L41 | mirkwood_bat_template_medium_e |
| Leg | mkwd_inf3 | `taom_lord_mkwd_inf3_greaves` | 0/0/0/23 | none | mirkwood_bat_template_medium_a |
| Leg | mkwd_inf3 | `taom_lord_mkwd_inf3_greaves_bronze` | 0/0/0/23 | none | mirkwood_bat_template_medium_b |

## mistymountainorcs

Folders: mordor. Families: 2. Pieces per slot: Head 0, Body 1, Cape 0, Gloves 1, Leg 0.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Body | md_orc_inf | `sk_md_orc_inf_chest_heavy_a` | 0/32/10/0 | none | bluecraig_bat_template_medium_a, goblin_bat_template_medium_a, goblin_bat_template_medium_d, mistymountainorcs_bat_template_medium_a, mistymountainorcs_bat_template_medium_d |
| Gloves | md_mor_arc | `sk_md_mor_arc_bracer_heavy_b` | 0/0/12/0 | none | mistymountainorcs_bat_template_medium_c |

## mordor

Folders: mordor. Families: 5. Pieces per slot: Head 6, Body 10, Cape 2, Gloves 2, Leg 1.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | md_num | `sk_md_num_hood_lord_a` | 48/0/0/0 | none | - |
| Head | md_num | `sk_md_num_hood_lord_b` | 51/0/0/0 | none | umbar_bat_template_medium_e |
| Head | md_num_cav | `sk_md_num_cav_helmet_lord_a` | 51/0/0/0 | none | umbar_bat_template_medium_c |
| Head | md_num_cav | `sk_md_num_cav_helmet_lord_b` | 51/0/0/0 | none | umbar_bat_template_medium_d |
| Head | md_num_inf | `sk_md_num_inf_helmet_lord_a` | 51/0/0/0 | none | umbar_bat_template_medium_a |
| Head | md_num_inf | `sk_md_num_inf_helmet_lord_b` | 51/0/0/0 | none | umbar_bat_template_medium_b |
| Body | md_num_cav | `sm_md_num_cav_chest_elite_a` | 0/46/14/0 | mordor_num_temple_knight L46 | umbar_bat_template_medium_c |
| Body | md_num_inf | `sm_md_num_inf_chest_elite_a` | 0/46/14/0 | mordor_num_temple_guard L46 | umbar_bat_template_medium_a |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_a1` | 0/42/13/0 | none | mordor_bat_template_medium_c |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_a2` | 0/6/0/0 | mordor_uruk_grunt L11 | - |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_b1` | 0/42/13/0 | none | mordor_bat_template_medium_d |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_b2` | 0/6/0/0 | mordor_uruk_grunt L11 | - |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_c1` | 0/42/13/0 | none | mordor_bat_template_medium_e |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_c2` | 0/6/0/0 | mordor_uruk_grunt L11 | - |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_d1` | 0/6/0/0 | mordor_uruk_grunt L11 | - |
| Body | uruk_dmoorr_aachiilmn_aacinpt | `sk_uruk_mordor_chainmail_captain_d2` | 0/6/0/0 | mordor_uruk_grunt L11 | - |
| Cape | md_num_cav | `sm_md_num_cav_pauld_cape_elite_a` | 0/13/11/0 | mordor_num_temple_knight L46 | umbar_bat_template_medium_c, umbar_bat_template_medium_d |
| Cape | md_num_inf | `sm_md_num_inf_pauld_cape_elite_a` | 0/13/11/0 | mordor_num_temple_guard L46 | umbar_bat_template_medium_a |
| Gloves | md_num_arc | `sk_md_num_arc_bracer_lord_a` | 0/0/25/0 | none | umbar_bat_template_medium_e |
| Gloves | md_num_inf | `sk_md_num_inf_bracer_lord_a` | 0/0/25/0 | none | umbar_bat_template_medium_a, umbar_bat_template_medium_b, umbar_bat_template_medium_c, umbar_bat_template_medium_d |
| Leg | md_num | `sm_md_num_grvs_elite_a` | 0/0/0/21 | mordor_num_shadowbow L46 | umbar_bat_template_medium_a, umbar_bat_template_medium_b, umbar_bat_template_medium_c, umbar_bat_template_medium_d, umbar_bat_template_medium_e |

Pool pieces a troop under level 41 wears, so the restat prices them at that troop's band (no mesh ladder substitute: the ids carry no tier token):

| Piece | Troop | Level |
|---|---|---:|
| `sk_uruk_mordor_chainmail_captain_a2` | mordor_uruk_grunt | 11 |
| `sk_uruk_mordor_chainmail_captain_a2` | mordor_uruk_grunt_merc | 11 |
| `sk_uruk_mordor_chainmail_captain_a2` | mordor_uruk_warrior | 21 |
| `sk_uruk_mordor_chainmail_captain_b2` | mordor_uruk_grunt | 11 |
| `sk_uruk_mordor_chainmail_captain_b2` | mordor_uruk_grunt_merc | 11 |
| `sk_uruk_mordor_chainmail_captain_b2` | mordor_uruk_warrior | 21 |
| `sk_uruk_mordor_chainmail_captain_c2` | mordor_uruk_grunt | 11 |
| `sk_uruk_mordor_chainmail_captain_c2` | mordor_uruk_grunt_merc | 11 |
| `sk_uruk_mordor_chainmail_captain_c2` | mordor_uruk_warrior | 21 |
| `sk_uruk_mordor_chainmail_captain_d1` | mordor_uruk_grunt | 11 |
| `sk_uruk_mordor_chainmail_captain_d1` | mordor_uruk_grunt_merc | 11 |
| `sk_uruk_mordor_chainmail_captain_d1` | mordor_uruk_warrior | 21 |
| `sk_uruk_mordor_chainmail_captain_d2` | mordor_uruk_grunt | 11 |
| `sk_uruk_mordor_chainmail_captain_d2` | mordor_uruk_grunt_merc | 11 |
| `sk_uruk_mordor_chainmail_captain_d2` | mordor_uruk_warrior | 21 |

## rivendell

Folders: rivendell. Families: 6. Pieces per slot: Head 6, Body 13, Cape 1, Gloves 1, Leg 1.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | deeillnrv | `rivendell_helmet_elite` | 53/0/0/0 | imladris_warden L41 | rivendell_bat_template_medium_a, rivendell_bat_template_medium_c |
| Head | deeillnrv | `rivendell_helmet_elite_silver` | 53/0/0/0 | lindon_rider_of_himring L46 | rivendell_bat_template_medium_b |
| Head | deeillnrv_acceiklmrst | `rivendell_helmet_lord_circletmask` | 57/0/0/0 | none | rivendell_bat_template_medium_e |
| Head | deeillnrv_acehrr_1eirt | `rivendell_helmet_archer_tier1_silver` | 40/0/0/0 | none | - |
| Head | deeillnrv_cceilrt | `rivendell_helmet_lord_circlet` | 57/0/0/0 | none | rivendell_bat_template_medium_d |
| Head | deeillnrv_cceilrt | `rivendell_helmet_lord_circlet_silver` | 53/0/0/0 | none | - |
| Body | deeillnrv | `rivendell_body_gold_c` | 0/50/15/0 | none | rivendell_bat_template_medium_e |
| Body | deeillnrv | `rivendell_body_silver_a` | 0/50/15/0 | none | rivendell_bat_template_medium_b |
| Body | deeillnrv | `rivendell_body_silver_b` | 0/50/15/0 | none | rivendell_bat_template_medium_d |
| Body | deeillnrv | `rivendell_body_silver_c` | 0/50/15/0 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord1` | 0/55/16/22 | none | lindon_bat_template_medium_a, rivendell_bat_template_medium_a |
| Body | deeillnrv_lord | `rivendell_torso_lord1_silver` | 0/55/16/22 | none | rivendell_bat_template_medium_c |
| Body | deeillnrv_lord | `rivendell_torso_lord1_silvergold` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord2` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord2_silver` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord2_silvergold` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord3` | 0/51/15/20 | none | - |
| Body | deeillnrv_lord | `rivendell_torso_lord3_silver` | 0/51/15/20 | lindon_noldorin_lancer L46 | - |
| Body | deeillnrv_lord | `rivendell_torso_lord3_silvergold` | 0/51/15/20 | lindon_noldorin_lancer L46 | - |
| Cape | deeillnrv | `rivendell_pauldron_elite_robe` | 0/14/12/0 | battlemaster_of_the_first_age L41 | rivendell_bat_template_medium_a, rivendell_bat_template_medium_b, rivendell_bat_template_medium_c, rivendell_bat_template_medium_d |
| Gloves | deeillnrv | `rivendell_gloves_silver` | 0/0/27/0 | none | rivendell_bat_template_medium_a, rivendell_bat_template_medium_b, rivendell_bat_template_medium_c |
| Leg | deeillnrv_1aeegrsv | `rivendell_boots_greaves1_silver` | 0/0/0/25 | none | rivendell_bat_template_medium_b |

## sturgia

Folders: dale. Families: 0. Pieces per slot: Head 0, Body 0, Cape 0, Gloves 0, Leg 0.

## umbar

Folders: harad, mordor. Families: 5. Pieces per slot: Head 6, Body 5, Cape 3, Gloves 2, Leg 1.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | md_num | `sk_md_num_hood_lord_a` | 48/0/0/0 | none | - |
| Head | md_num | `sk_md_num_hood_lord_b` | 51/0/0/0 | none | umbar_bat_template_medium_e |
| Head | md_num_cav | `sk_md_num_cav_helmet_lord_a` | 51/0/0/0 | none | umbar_bat_template_medium_c |
| Head | md_num_cav | `sk_md_num_cav_helmet_lord_b` | 51/0/0/0 | none | umbar_bat_template_medium_d |
| Head | md_num_inf | `sk_md_num_inf_helmet_lord_a` | 51/0/0/0 | none | umbar_bat_template_medium_a |
| Head | md_num_inf | `sk_md_num_inf_helmet_lord_b` | 51/0/0/0 | none | umbar_bat_template_medium_b |
| Body | ar_aadinnru | `ar_ardunian_elite_armour` | 0/43/13/17 | none | umbar_bat_template_medium_e |
| Body | md_num_cav | `sm_md_num_cav_chest_elite_a` | 0/46/14/0 | mordor_num_temple_knight L46 | umbar_bat_template_medium_c |
| Body | md_num_cav | `sm_md_num_cav_chest_elite_b` | 0/46/14/0 | mordor_num_temple_knight L46 | umbar_bat_template_medium_d |
| Body | md_num_inf | `sm_md_num_inf_chest_elite_a` | 0/46/14/0 | mordor_num_temple_guard L46 | umbar_bat_template_medium_a |
| Body | md_num_inf | `sm_md_num_inf_chest_elite_b` | 0/46/14/0 | mordor_num_temple_guard L46 | umbar_bat_template_medium_b |
| Cape | md_num_cav | `sm_md_num_cav_pauld_cape_elite_a` | 0/13/11/0 | mordor_num_temple_knight L46 | umbar_bat_template_medium_c, umbar_bat_template_medium_d |
| Cape | md_num_inf | `sm_md_num_inf_pauld_cape_elite_a` | 0/13/11/0 | mordor_num_temple_guard L46 | umbar_bat_template_medium_a |
| Cape | md_num_inf | `sm_md_num_inf_pauld_cape_elite_b` | 0/13/11/0 | mordor_num_temple_guard L46 | umbar_bat_template_medium_b, umbar_bat_template_medium_e |
| Gloves | md_num_arc | `sk_md_num_arc_bracer_lord_a` | 0/0/25/0 | none | umbar_bat_template_medium_e |
| Gloves | md_num_inf | `sk_md_num_inf_bracer_lord_a` | 0/0/25/0 | none | umbar_bat_template_medium_a, umbar_bat_template_medium_b, umbar_bat_template_medium_c, umbar_bat_template_medium_d |
| Leg | md_num | `sm_md_num_grvs_elite_a` | 0/0/0/21 | mordor_num_shadowbow L46 | umbar_bat_template_medium_a, umbar_bat_template_medium_b, umbar_bat_template_medium_c, umbar_bat_template_medium_d, umbar_bat_template_medium_e |

## vlandia

Folders: rohan. Families: 7. Pieces per slot: Head 3, Body 8, Cape 4, Gloves 5, Leg 4.

| Slot | Family | Piece | Armour | Lowest troop | Templates |
|---|---|---|---|---|---|
| Head | cts_ahnor_aacinpt | `cts_rohan_helmet_captain` | 38/0/0/0 | rohan_edoras_golden_hall_kings_own_rider L36 | rohan_bat_template_medium_a |
| Head | cts_ahnor_aacinpt | `cts_rohan_helmet_captain1` | 38/0/0/0 | rohan_edoras_golden_hall_kings_own_rider L36 | rohan_bat_template_medium_b |
| Head | roh_nbl | `roh_nbl_hlm_cf` | 32/0/0/0 | rohan_edoras_golden_hall_elite_rider L26 | rohan_bat_template_medium_d, rohan_bat_template_medium_e |
| Body | cts_ahnor_aacinpt | `cts_rohan_armor_captain` | 0/37/11/0 | rohan_edoras_golden_hall_kings_own_rider L36 | rohan_bat_template_medium_a |
| Body | cts_ahnor_aacinpt | `cts_rohan_armor_captain1` | 0/37/11/0 | rohan_edoras_golden_hall_kings_own_rider L36 | rohan_bat_template_medium_c |
| Body | ehinrtuw | `whiterun_armor` | 0/38/11/0 | none | rohan_bat_template_medium_d |
| Body | roh_nbl | `roh_nbl_arm_1` | 0/25/8/0 | rohan_edoras_golden_hall_elite_rider L26 | - |
| Body | roh_nbl | `roh_nbl_arm_b` | 0/25/7/0 | rohan_edoras_golden_hall_elite_rider L26 | - |
| Body | roh_nbl | `roh_nbl_arm_g` | 0/25/7/0 | rohan_edoras_golden_hall_elite_rider L26 | - |
| Body | roh_nbl | `roh_nbl_arm_r` | 0/40/8/0 | none | rohan_bat_template_medium_b |
| Body | roh_nbl | `roh_nbl_arm_y` | 0/25/8/0 | none | - |
| Cape | cts_ahnor_aacinpt | `cts_rohan_captain_shoulder` | 0/11/11/0 | none | rohan_bat_template_medium_a, rohan_bat_template_medium_c |
| Cape | cts_ahnor_aacinpt | `cts_rohan_shoulder_captain1` | 0/11/11/0 | none | - |
| Cape | ehinrtuw | `whiterun_cloak` | 0/11/11/0 | none | rohan_bat_template_medium_d |
| Cape | roh_nbl_dhlrs_cgs | `roh_nbl_shldr_cgs_2` | 0/10/10/0 | rohan_eastemnet_kings_own_lancer L36 | rohan_bat_template_medium_b, rohan_bat_template_medium_e |
| Gloves | cts_ahnor_aacinpt | `cts_rohan_captain_bracer` | 0/0/6/0 | rohan_edoras_golden_hall_rider L16 | - |
| Gloves | cts_ahnor_aacinpt_1abcerrs | `cts_rohan_captain_bracers1` | 0/0/6/0 | rohan_edoras_golden_hall_rider L16 | - |
| Gloves | ehinrtuw | `whiterun_bracers` | 0/0/20/0 | none | rohan_bat_template_medium_b |
| Gloves | jor_brcr_fgc3 | `jor_lord_brcr_fgc3` | 0/0/20/0 | none | rohan_bat_template_medium_c |
| Gloves | roh_nbl | `roh_nbl_glv` | 0/0/13/0 | rohan_edoras_golden_hall_elite_rider L26 | rohan_bat_template_medium_d, rohan_bat_template_medium_e |
| Leg | cts_ahnor_aacinpt | `cts_rohan_captain_boots` | 0/0/0/11 | rohan_edoras_veteran_swordsman L21 | - |
| Leg | cts_ahnor_aacinpt_1boost | `cts_rohan_captain_boots1` | 0/0/0/17 | rohan_edoras_golden_hall_eorlingas_rider L31 | rohan_bat_template_medium_a, rohan_bat_template_medium_c, rohan_bat_template_medium_e |
| Leg | ehinrtuw | `whiterun_boots` | 0/0/0/18 | none | rohan_bat_template_medium_b, rohan_bat_template_medium_d |
| Leg | roh_nbl | `roh_nbl_bts` | 0/0/0/11 | rohan_edoras_golden_hall_elite_rider L26 | - |

Pool pieces a troop under level 41 wears, so the restat prices them at that troop's band (no mesh ladder substitute: the ids carry no tier token):

| Piece | Troop | Level |
|---|---|---:|
| `cts_rohan_armor_captain` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_armor_captain` | southrhovanion_horse_lord | 36 |
| `cts_rohan_armor_captain1` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_armor_captain1` | southrhovanion_horse_lord | 36 |
| `cts_rohan_captain_boots` | rohan_edoras_golden_hall_eorlingas_rider | 31 |
| `cts_rohan_captain_boots` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_captain_boots` | rohan_edoras_veteran_swordsman | 21 |
| `cts_rohan_captain_boots` | southrhovanion_lancer | 21 |
| `cts_rohan_captain_boots1` | rohan_edoras_golden_hall_eorlingas_rider | 31 |
| `cts_rohan_captain_boots1` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_captain_boots1` | southrhovanion_horse_lord | 36 |
| `cts_rohan_captain_boots1` | southrhovanion_rider_captain | 31 |
| `cts_rohan_captain_bracer` | rohan_edoras_golden_hall_eorlingas_rider | 31 |
| `cts_rohan_captain_bracer` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_captain_bracer` | rohan_edoras_golden_hall_rider | 16 |
| `cts_rohan_captain_bracer` | rohan_edoras_golden_hall_veteran_rider | 21 |
| `cts_rohan_captain_bracer` | rohan_edoras_master_swordsman | 31 |
| `cts_rohan_captain_bracer` | rohan_edoras_meduseld_veteran_spearman | 21 |
| `cts_rohan_captain_bracer` | rohan_edoras_swordsman | 16 |
| `cts_rohan_captain_bracer` | rohan_edoras_veteran_swordsman | 21 |
| `cts_rohan_captain_bracer` | southrhovanion_ford_spearman | 16 |
| `cts_rohan_captain_bracer` | southrhovanion_lancer | 21 |
| `cts_rohan_captain_bracer` | southrhovanion_rider | 16 |
| `cts_rohan_captain_bracers1` | rohan_edoras_golden_hall_eorlingas_rider | 31 |
| `cts_rohan_captain_bracers1` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_captain_bracers1` | rohan_edoras_golden_hall_rider | 16 |
| `cts_rohan_captain_bracers1` | rohan_edoras_golden_hall_veteran_rider | 21 |
| `cts_rohan_captain_bracers1` | rohan_edoras_master_swordsman | 31 |
| `cts_rohan_captain_bracers1` | rohan_edoras_meduseld_veteran_spearman | 21 |
| `cts_rohan_captain_bracers1` | rohan_edoras_swordsman | 16 |
| `cts_rohan_captain_bracers1` | rohan_edoras_veteran_swordsman | 21 |
| `cts_rohan_helmet_captain` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_helmet_captain` | southrhovanion_horse_lord | 36 |
| `cts_rohan_helmet_captain1` | rohan_edoras_golden_hall_kings_own_rider | 36 |
| `cts_rohan_helmet_captain1` | southrhovanion_horse_lord | 36 |
| `roh_nbl_arm_1` | rohan_edoras_golden_hall_elite_rider | 26 |
| `roh_nbl_arm_1` | rohan_edoras_meduseld_helmingas_spearman | 26 |
| `roh_nbl_arm_b` | rohan_edoras_golden_hall_elite_rider | 26 |
| `roh_nbl_arm_b` | rohan_edoras_helmingas_swordsman | 26 |
| `roh_nbl_arm_g` | rohan_edoras_golden_hall_elite_rider | 26 |
| `roh_nbl_arm_g` | rohan_edoras_helmingas_swordsman | 26 |
| `roh_nbl_arm_g` | rohan_edoras_meduseld_helmingas_spearman | 26 |
| `roh_nbl_arm_g` | rohan_wold_kings_own_horse_archer | 36 |
| `roh_nbl_arm_g` | southrhovanion_horse_thane | 26 |
| `roh_nbl_bts` | rohan_edoras_golden_hall_elite_rider | 26 |
| `roh_nbl_bts` | rohan_edoras_helmingas_swordsman | 26 |
| `roh_nbl_bts` | southrhovanion_horse_thane | 26 |
| `roh_nbl_bts` | southrhovanion_southwood_marksman | 26 |
| `roh_nbl_bts` | southrhovanion_veteran_ford_guard | 26 |
| `roh_nbl_bts` | southrhovanion_veteran_horse_archer | 26 |
| `roh_nbl_glv` | rohan_edoras_golden_hall_elite_rider | 26 |
| `roh_nbl_glv` | rohan_edoras_helmingas_swordsman | 26 |
| `roh_nbl_glv` | rohan_edoras_meduseld_helmingas_spearman | 26 |
| `roh_nbl_glv` | rohan_edoras_meduseld_master_spearman | 31 |
| `roh_nbl_glv` | rohan_wold_kings_own_horse_archer | 36 |
| `roh_nbl_glv` | southrhovanion_horse_thane | 26 |
| `roh_nbl_glv` | southrhovanion_southwood_marksman | 26 |
| `roh_nbl_glv` | southrhovanion_veteran_ford_guard | 26 |
| `roh_nbl_glv` | southrhovanion_veteran_horse_archer | 26 |
| `roh_nbl_hlm_cf` | rohan_eastemnet_kings_own_lancer | 36 |
| `roh_nbl_hlm_cf` | rohan_edoras_golden_hall_elite_rider | 26 |
| `roh_nbl_hlm_cf` | rohan_edoras_golden_hall_eorlingas_rider | 31 |
| `roh_nbl_hlm_cf` | rohan_edoras_meduseld_master_spearman | 31 |
| `roh_nbl_hlm_cf` | rohan_westemnet_kings_own_rider | 36 |
| `roh_nbl_hlm_cf` | rohan_wold_kings_own_horse_archer | 36 |
| `roh_nbl_hlm_cf` | southrhovanion_horse_thane | 26 |
| `roh_nbl_hlm_cf` | southrhovanion_veteran_ford_guard | 26 |
| `roh_nbl_hlm_cf` | southrhovanion_veteran_horse_archer | 26 |
| `roh_nbl_shldr_cgs_2` | rohan_eastemnet_kings_own_lancer | 36 |
| `roh_nbl_shldr_cgs_2` | rohan_westemnet_kings_own_rider | 36 |
| `roh_nbl_shldr_cgs_2` | rohan_wold_kings_own_horse_archer | 36 |
| `roh_nbl_shldr_cgs_2` | southrhovanion_horse_lord | 36 |

