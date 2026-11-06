using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Procedural Physical Treasure Atoll Builder.
    /// Assembles dedicated shallow-water coral sandbars, swaying tropical palm groves,
    /// sea-worn rock stacks, and an interactive beach excavation site with pre-placed
    /// Kenney shovel, dug hole, and buried treasure chest.
    /// </summary>
    public static class TreasureAtollBuilder
    {
        public static GameObject BuildAtoll(Transform parent, Vector3 localPos, Quaternion localRot, string atollName, int goldReward)
        {
            GameObject atollRoot = new GameObject(atollName.Replace(" ", "_"));
            if (parent != null)
            {
                atollRoot.transform.SetParent(parent, false);
            }
            atollRoot.transform.localPosition = localPos;
            atollRoot.transform.localRotation = localRot;

            var registry = PrefabRegistrySO.Instance;

            // 1. Submerged Coral Reef & Shoal Foundation (plunges into sea, eliminates air gap)
            registry.Spawn("patch-sand", atollRoot.transform, new Vector3(0f, -0.40f, 0f), Quaternion.identity, new Vector3(5.5f, 1.5f, 5.5f));
            registry.Spawn("rocks-b", atollRoot.transform, new Vector3(0.5f, -0.70f, 0.5f), Quaternion.identity, new Vector3(3.2f, 1.8f, 3.2f));

            // 2. Shoreline Sandbar Perimeter (gently enters sea with zero air gap)
            registry.Spawn("patch-sand", atollRoot.transform, new Vector3(0f, -0.05f, 0f), Quaternion.identity, new Vector3(4.5f, 0.8f, 4.5f));

            // 3. Inner Elevated Dunes & Tropical Palm Foliage
            registry.Spawn("patch-sand-foliage", atollRoot.transform, new Vector3(0f, 0.14f, 0f), Quaternion.identity, new Vector3(3.5f, 0.8f, 3.5f));
            registry.Spawn("patch-grass-foliage", atollRoot.transform, new Vector3(1.2f, 0.18f, -1.2f), Quaternion.Euler(0f, -45f, 0f), new Vector3(2.0f, 0.8f, 2.0f));

            // 4. Grounded Framing Natural Sea Rocks (embedded at Y = -0.20m, safely within island perimeter)
            registry.Spawn("rocks-sand-b", atollRoot.transform, new Vector3(-2.2f, -0.20f, -1.8f), Quaternion.Euler(0f, 60f, 0f), new Vector3(2.2f, 1.8f, 2.2f));
            registry.Spawn("rocks-sand-c", atollRoot.transform, new Vector3(2.2f, -0.20f, 1.5f), Quaternion.Euler(0f, -75f, 0f), new Vector3(2.0f, 1.8f, 2.0f));

            // 5. Swaying Tropical Palm Grove (firmly planted in sand dunes)
            registry.Spawn("palm-detailed-bend", atollRoot.transform, new Vector3(2.5f, 0.22f, -2.0f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 1.5f);
            registry.Spawn("palm-straight", atollRoot.transform, new Vector3(-2.2f, 0.22f, -2.5f), Quaternion.Euler(0f, -60f, 0f), Vector3.one * 1.3f);
            registry.Spawn("palm-detailed-straight", atollRoot.transform, new Vector3(3.5f, 0.22f, 1.2f), Quaternion.Euler(0f, 120f, 0f), Vector3.one * 1.4f);
            registry.Spawn("palm-bend", atollRoot.transform, new Vector3(-3.5f, 0.22f, 1.0f), Quaternion.Euler(0f, -140f, 0f), Vector3.one * 1.3f);

            // 6. Atmospheric Island Props
            registry.Spawn("barrel", atollRoot.transform, new Vector3(-1.4f, 0.22f, -1.5f), Quaternion.Euler(65f, 30f, 0f), Vector3.one * 1.2f);
            registry.Spawn("mast-ropes", atollRoot.transform, new Vector3(-4.2f, 0.18f, -0.8f), Quaternion.Euler(-18f, 45f, 15f), Vector3.one * 1.2f);
            registry.Spawn("grass-plant", atollRoot.transform, new Vector3(1.0f, 0.22f, 2.8f), Quaternion.identity, Vector3.one * 1.5f);

            // 7. Interactive Beach Dig Site
            GameObject digSite = new GameObject("ExcavationSite");
            digSite.transform.SetParent(atollRoot.transform, false);
            digSite.transform.localPosition = new Vector3(1.2f, 0.24f, 0.6f);

            // Buried Chest (initially sunk beneath sand)
            GameObject chestGo = registry.Spawn("chest", digSite.transform, new Vector3(0f, -0.45f, 0f), Quaternion.Euler(0f, 25f, 0f), Vector3.one * 1.35f);
            if (chestGo != null) chestGo.name = "Chest";

            // Dug Hole (swapped in during digging cutscene)
            GameObject holeGo = registry.Spawn("hole", digSite.transform, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(1.6f, 1.0f, 1.6f), addMeshCollider: false);
            if (holeGo != null)
            {
                holeGo.name = "Hole";
                holeGo.SetActive(false);
            }

            // Pirate Shovel (animated during cutscene)
            GameObject shovelGo = registry.Spawn("tool-shovel", digSite.transform, new Vector3(-0.85f, 0.45f, 0.2f), Quaternion.Euler(-35f, 75f, 20f), Vector3.one * 1.4f, addMeshCollider: false);
            if (shovelGo != null)
            {
                shovelGo.name = "Shovel";
                shovelGo.SetActive(false); // Enabled during excavation sequence
            }

            // Wire Treasure Island Landmark component
            TreasureIslandLandmark landmark = atollRoot.AddComponent<TreasureIslandLandmark>();
            landmark.InitializeIsland(atollName, goldReward);

            // Attach solid primitive island barrier colliders and coastal repulsion trigger
            IslandLandmassBarrier.AttachToAtoll(atollRoot.transform);

            return atollRoot;
        }
    }
}
