# Chappal Chor - Setup (from scratch)

## 1. Project
1. Install Unity Hub, then Unity 6 LTS (or 2022.3 LTS). Add "Windows/Mac Build Support".
2. New project: **3D (Built-in or URP)**. Name it ChappalChor.
3. Edit > Project Settings > Player > Other Settings > **Active Input Handling = Both**
   (these scripts use the classic Input class).

## 2. Player
1. Window > Package Manager > Unity Registry > **Starter Assets - ThirdPerson** > Import.
2. Drag `PlayerArmature` + `PlayerFollowCamera` + `MainCamera` prefabs into a scene.
3. Tag the player **Player**.
4. Add `ChappalThrower` to the player, drag Main Camera into `Cam`.

## 3. Chappal prefab
1. Create a flattened Cube (scale 0.3, 0.05, 0.12) for now. Add Rigidbody.
2. **Tag: Chappal** (Tags > Add Tag). Save as prefab, assign to ChappalThrower.

## 4. Scene (grey-box)
- Plane for the ground (bake NavMesh later).
- **Temple**: a few cubes plus a "rack" cube with trigger collider + `ChappalPickup`.
  Put 1-2 empty objects with `Witness` (rotate them so forward faces the rack).
- **Gurudwara**: cube + trigger zone + `SafeZone` (Langar).
- **Masjid**: cube + trigger zone + `SafeZone` (Sleep).
- **Kabadi dealer**: cube + trigger zone + `SafeZone` (Dealer).
- **Targets**: Quads with `Target` (Neta poster +50), capsules for police (+30, tick Is Police Target),
  capsules for kid/animal/woman (-100). Colliders needed.

## 5. Managers and UI
1. Empty object "GameManager" + `GameManager`.
2. GameObject > UI > Canvas, add 2 TextMeshPro texts, empty object with `HUD` and assign both.

## 6. Controls
LMB throw, hold E steal, F interact (eat / sleep+save / sell).

## 7. Next milestones
- Police AI: NavMesh Agent chasing when `heat >= 3`.
- Protest zone: trigger that adds +5 score per second while inside.
- Missions: ScriptableObject list (steal from pandit, give to poor NPC, help vendor).
- Art: Synty / Kenney / Sketchfab free packs; Mixamo animations.
- Audio: "Arre arre arre!" on negative hits.
