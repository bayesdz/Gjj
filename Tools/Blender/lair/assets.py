"""Asset list for Floor 1: the Goblin Lair. All Poly Haven assets are CC0."""

TEXTURES = {
    # material name: (poly haven id, tile size in metres)
    "Lair_Rock": ("rock_wall_10", 3.0),
    "Lair_Mud": ("brown_mud_03", 2.5),
    "Lair_Wood": ("rough_wood", 1.5),
    "Lair_Straw": ("reed_roof_04", 1.2),
    "Lair_Rust": ("rusty_metal_03", 1.0),
    "Lair_Cloth": ("hessian_230", 0.8),
}

# Scanned props. Layer: "both" = same in illusion and truth, "illusion" / "truth" = one look only.
MODELS = {
    "wooden_barrels_01": "both",
    "wooden_crate_01": "both",
    "wooden_crate_02": "both",
    "wooden_bucket_01": "both",
    "wooden_bucket_02": "both",
    "stone_fire_pit": "both",
    "wooden_lantern_01": "both",
    "rock_moss_set_01": "both",
    "rock_moss_set_02": "both",
    "boulder_01": "both",
    "namaqualand_boulder_02": "both",
    "namaqualand_boulder_05": "both",
    "root_cluster_01": "both",
    "root_cluster_02": "both",
    "dry_branches_medium_01": "both",
    "tree_stump_01": "both",
    "wooden_stool_02": "both",
    # everyday life: what the Eye of Truth shows
    "carved_wooden_elephant": "truth",
    "wicker_basket_01": "truth",
    "wicker_basket_02": "truth",
    "wooden_bowl_01": "truth",
    "wooden_bowl_02": "truth",
    "wooden_spoon": "truth",
    "jug_01": "truth",
    "ceramic_pot": "truth",
    "spinning_wheel_01": "truth",
    "wooden_cutting_board": "truth",
    "yellow_onion": "truth",
    "sweet_potato": "truth",
    # weapons and loot: what the Voice wants you to see
    "machete": "illusion",
    "hatchet": "illusion",
    "wooden_axe": "illusion",
    "treasure_chest": "illusion",
}
