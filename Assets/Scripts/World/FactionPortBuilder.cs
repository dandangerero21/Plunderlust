using Plunderlust.Core.Data;
using Plunderlust.Port;
using Plunderlust.Shipyard;
using UnityEngine;

namespace Plunderlust.World
{
    public enum PortArchetype
    {
        ImperialNavyBastion,    // Fort St. George
        MerchantGuildHub,       // Port Marigold / Port Royal
        CorsairHavenFreeport    // Isla de la Libre / Tortuga / Smuggler's Cove
    }

    /// <summary>
    /// Master Procedural Architectural Port Builder.
    /// Assembles 3 distinct historical 17th-century architectural port archetypes
    /// using modular Kenney Pirate Kit models with solid physics colliders,
    /// dynamic market economics, shipyards, and tavern recruitment.
    /// </summary>
    public static class FactionPortBuilder
    {
        /// <summary>
        /// Builds a fully realized 3D port structure in the world.
        /// </summary>
        public static GameObject BuildPort(Transform parent, Vector3 localPos, Quaternion localRot, PortArchetype archetype, string portName)
        {
            GameObject portRoot = new GameObject(portName.Replace(" ", "_"));
            if (parent != null)
            {
                portRoot.transform.SetParent(parent, false);
            }
            portRoot.transform.localPosition = localPos;
            portRoot.transform.localRotation = localRot;

            switch (archetype)
            {
                case PortArchetype.ImperialNavyBastion:
                    BuildImperialNavyBastion(portRoot.transform, portName);
                    break;
                case PortArchetype.MerchantGuildHub:
                    BuildMerchantGuildHub(portRoot.transform, portName);
                    break;
                case PortArchetype.CorsairHavenFreeport:
                    BuildCorsairHavenFreeport(portRoot.transform, portName);
                    break;
            }

            // Attach solid primitive island barrier colliders and coastal repulsion trigger
            IslandLandmassBarrier.AttachToPort(portRoot.transform);

            return portRoot;
        }

        // =====================================================================
        // 1. IMPERIAL NAVY BASTION (FORT ST. GEORGE)
        // High stone bluffs, castle battlements, blue towers, coastal cannons, navy banners
        // Grounded with submerged sand/rock shelf extending under water.
        // =====================================================================
        private static void BuildImperialNavyBastion(Transform root, string portName)
        {
            var registry = PrefabRegistrySO.Instance;

            // 1. Submerged Seabed & Stone Foundation (plunges into sea, eliminates air gap)
            registry.Spawn("patch-sand", root, new Vector3(0f, -0.40f, 0f), Quaternion.identity, new Vector3(6.0f, 1.5f, 6.0f));
            registry.Spawn("rocks-b", root, new Vector3(0f, -0.60f, -1.0f), Quaternion.identity, new Vector3(3.8f, 2.0f, 3.8f));

            // 2. Shoreline Beach Perimeter (gently touches waterline)
            registry.Spawn("patch-sand", root, new Vector3(0f, -0.05f, 0f), Quaternion.identity, new Vector3(4.8f, 0.8f, 4.8f));

            // 3. Elevated Cliff Foundation Bluff
            registry.Spawn("rocks-b", root, new Vector3(0f, 0.20f, -0.5f), Quaternion.identity, new Vector3(3.2f, 1.6f, 3.2f));

            // 4. Castle Fortifications on elevated bluff
            registry.Spawn("castle-wall", root, new Vector3(-2.4f, 1.0f, 2.8f), Quaternion.identity, Vector3.one * 1.2f);
            registry.Spawn("castle-gate", root, new Vector3(0f, 1.0f, 2.8f), Quaternion.identity, Vector3.one * 1.2f);
            registry.Spawn("castle-wall", root, new Vector3(2.4f, 1.0f, 2.8f), Quaternion.identity, Vector3.one * 1.2f);

            // Grand Citadel Keep & Roof
            registry.Spawn("tower-base-door", root, new Vector3(0f, 1.2f, -1.2f), Quaternion.identity, Vector3.one * 1.2f);
            registry.Spawn("tower-middle-windows", root, new Vector3(0f, 3.4f, -1.2f), Quaternion.identity, Vector3.one * 1.2f);
            registry.Spawn("tower-roof", root, new Vector3(0f, 5.6f, -1.2f), Quaternion.identity, Vector3.one * 1.2f);

            // Corner Watchtowers
            registry.Spawn("tower-watch", root, new Vector3(-3.8f, 1.0f, 2.8f), Quaternion.identity, Vector3.one);
            registry.Spawn("tower-watch", root, new Vector3(3.8f, 1.0f, 2.8f), Quaternion.identity, Vector3.one);

            // Seaward Coastal Defense Cannons
            registry.Spawn("cannon", root, new Vector3(-3.0f, 1.0f, 3.6f), Quaternion.identity, Vector3.one * 1.1f);
            registry.Spawn("cannon", root, new Vector3(3.0f, 1.0f, 3.6f), Quaternion.identity, Vector3.one * 1.1f);

            // High Imperial Navy Banners
            registry.Spawn("flag-high", root, new Vector3(0f, 7.2f, -1.2f), Quaternion.identity, Vector3.one * 1.3f);
            registry.Spawn("flag-high-pennant", root, new Vector3(-3.8f, 3.2f, 2.8f), Quaternion.identity, Vector3.one * 1.1f);
            registry.Spawn("flag-high-pennant", root, new Vector3(3.8f, 3.2f, 2.8f), Quaternion.identity, Vector3.one * 1.1f);

            // Stone / Timber Supply Quay & Landing Pier (extends seaward into water)
            registry.Spawn("structure-platform-dock", root, new Vector3(0f, 0.15f, 5.5f), Quaternion.identity, new Vector3(2.0f, 1.0f, 2.4f));
            registry.Spawn("barrel", root, new Vector3(-1.2f, 0.25f, 5.2f), Quaternion.identity, Vector3.one);
            registry.Spawn("crate", root, new Vector3(1.2f, 0.25f, 5.2f), Quaternion.identity, Vector3.one);

            // Wire Components
            PortIslandHaven haven = root.gameObject.AddComponent<PortIslandHaven>();
            haven.ConfigurePort(portName, FactionType.ImperialNavy, 40);

            PortCommodityMarket market = root.gameObject.AddComponent<PortCommodityMarket>();
            market.ConfigureMarket(FactionType.ImperialNavy, false);

            DynamicMarketEconomy economy = root.gameObject.AddComponent<DynamicMarketEconomy>();
            economy.InitializeEconomy(PortArchetype.ImperialNavyBastion, market);

            root.gameObject.AddComponent<ShipyardDrydock>();
        }

        // =====================================================================
        // 2. MERCHANT GUILD HUB (PORT MARIGOLD / PORT ROYAL)
        // Sprawling wooden piers, thatched warehouses, stacked trade cargo crates/barrels
        // Grounded with graduated seabed shelf, natural shoreline, and embedded bluffs.
        // =====================================================================
        private static void BuildMerchantGuildHub(Transform root, string portName)
        {
            var registry = PrefabRegistrySO.Instance;

            // 1. Submerged Seabed & Reef Foundation (plunges into sea, eliminates air gap)
            registry.Spawn("patch-sand", root, new Vector3(0f, -0.40f, 0f), Quaternion.identity, new Vector3(5.6f, 1.5f, 5.6f));
            registry.Spawn("rocks-sand-b", root, new Vector3(0.5f, -0.70f, -2.5f), Quaternion.identity, new Vector3(3.5f, 2.0f, 3.5f));

            // 2. Shoreline Beach Perimeter (outer rim touches waterline with zero air gap)
            registry.Spawn("patch-sand", root, new Vector3(0f, -0.05f, 0f), Quaternion.identity, new Vector3(4.5f, 0.8f, 4.5f));

            // 3. Inner Elevated Dunes (dry land plateau for buildings & palm groves)
            registry.Spawn("patch-sand-foliage", root, new Vector3(0f, 0.14f, -0.5f), Quaternion.identity, new Vector3(3.5f, 0.8f, 3.5f));

            // 4. Grounded Sea-Cliff Formations (embedded naturally at back of island)
            registry.Spawn("rocks-sand-c", root, new Vector3(-2.8f, -0.20f, -2.2f), Quaternion.Euler(0f, 45f, 0f), new Vector3(2.2f, 1.8f, 2.2f));
            registry.Spawn("rocks-sand-b", root, new Vector3(2.8f, -0.20f, -2.5f), Quaternion.Euler(0f, -30f, 0f), new Vector3(2.2f, 1.8f, 2.2f));

            // 5. Tropical Palm Grove (firmly rooted in elevated sand dunes)
            registry.Spawn("palm-detailed-straight", root, new Vector3(2.2f, 0.20f, -1.8f), Quaternion.identity, Vector3.one * 1.3f);
            registry.Spawn("palm-detailed-bend", root, new Vector3(-2.0f, 0.20f, -1.5f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 1.3f);
            registry.Spawn("palm-straight", root, new Vector3(0.8f, 0.20f, -3.2f), Quaternion.Euler(0f, -30f, 0f), Vector3.one * 1.2f);

            // 6. Thatched Roof Trading Warehouses & Customs Watchtower
            registry.Spawn("structure", root, new Vector3(-2.0f, 0.20f, 0.8f), Quaternion.identity, Vector3.one * 1.3f);
            registry.Spawn("structure-roof", root, new Vector3(-2.0f, 1.80f, 0.8f), Quaternion.identity, Vector3.one * 1.3f);

            registry.Spawn("structure", root, new Vector3(2.0f, 0.20f, 0.8f), Quaternion.identity, Vector3.one * 1.3f);
            registry.Spawn("structure-roof", root, new Vector3(2.0f, 1.80f, 0.8f), Quaternion.identity, Vector3.one * 1.3f);

            registry.Spawn("tower-complete-small", root, new Vector3(0f, 0.20f, -2.2f), Quaternion.identity, Vector3.one * 1.2f);
            registry.Spawn("flag-pennant", root, new Vector3(0f, 4.4f, -2.2f), Quaternion.identity, Vector3.one * 1.3f);

            // 7. Wooden Pier Network extending seaward into deep water
            registry.Spawn("structure-platform-dock", root, new Vector3(0f, 0.15f, 4.2f), Quaternion.identity, new Vector3(2.0f, 1.0f, 2.4f));
            registry.Spawn("platform-planks", root, new Vector3(-2.6f, 0.18f, 4.2f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.4f, 1.0f, 1.4f));
            registry.Spawn("platform-planks", root, new Vector3(2.6f, 0.18f, 4.2f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.4f, 1.0f, 1.4f));

            // 8. Densely Stacked Trade Cargo (Crates, Bottles, Barrels)
            registry.Spawn("crate", root, new Vector3(-1.2f, 0.25f, 3.8f), Quaternion.identity, Vector3.one);
            registry.Spawn("crate-bottles", root, new Vector3(-1.8f, 0.25f, 4.2f), Quaternion.identity, Vector3.one * 1.1f);
            registry.Spawn("barrel", root, new Vector3(1.4f, 0.25f, 3.8f), Quaternion.identity, Vector3.one);
            registry.Spawn("barrel", root, new Vector3(1.8f, 0.25f, 4.2f), Quaternion.identity, Vector3.one);
            registry.Spawn("bottle-large", root, new Vector3(0.8f, 0.25f, 4.8f), Quaternion.identity, Vector3.one * 1.2f);

            // Wire Components
            PortIslandHaven haven = root.gameObject.AddComponent<PortIslandHaven>();
            haven.ConfigurePort(portName, FactionType.MerchantGuild, 25);

            PortCommodityMarket market = root.gameObject.AddComponent<PortCommodityMarket>();
            market.ConfigureMarket(FactionType.MerchantGuild, false);

            DynamicMarketEconomy economy = root.gameObject.AddComponent<DynamicMarketEconomy>();
            economy.InitializeEconomy(PortArchetype.MerchantGuildHub, market);

            root.gameObject.AddComponent<ShipyardDrydock>();
        }

        // =====================================================================
        // 3. CORSAIR HAVEN / FREEPORT (ISLA DE LA LIBRE / TORTUGA)
        // Jagged rock cove, half-sunken shipwrecks as taverns, pirate flags, beach campfires
        // Grounded with submerged reef foundation, shoreline beaches, and rooted breakwaters.
        // =====================================================================
        private static void BuildCorsairHavenFreeport(Transform root, string portName)
        {
            var registry = PrefabRegistrySO.Instance;

            // 1. Submerged Reef Foundation (plunges into sea, eliminates air gap)
            registry.Spawn("patch-sand", root, new Vector3(0f, -0.40f, 0f), Quaternion.identity, new Vector3(5.6f, 1.5f, 5.6f));
            registry.Spawn("rocks-a", root, new Vector3(0f, -0.70f, -2.5f), Quaternion.Euler(0f, 180f, 0f), new Vector3(3.5f, 2.0f, 3.5f));

            // 2. Shoreline Beach Perimeter (enters water smoothly)
            registry.Spawn("patch-sand", root, new Vector3(0f, -0.05f, 0f), Quaternion.identity, new Vector3(4.5f, 0.8f, 4.5f));

            // 3. Inner Dry Sand Terracing
            registry.Spawn("patch-sand-foliage", root, new Vector3(0f, 0.14f, 0f), Quaternion.identity, new Vector3(3.5f, 0.8f, 3.5f));

            // 4. Jagged Sea Cove Enclosure (Breakwater rocks rooted at back/sides)
            registry.Spawn("rocks-sand-b", root, new Vector3(-3.2f, -0.20f, 1.2f), Quaternion.Euler(0f, 135f, 0f), new Vector3(2.4f, 1.8f, 2.4f));
            registry.Spawn("rocks-b", root, new Vector3(3.2f, -0.20f, -1.0f), Quaternion.Euler(0f, -45f, 0f), new Vector3(2.4f, 1.8f, 2.4f));
            registry.Spawn("rocks-a", root, new Vector3(0f, -0.20f, -3.2f), Quaternion.Euler(0f, 180f, 0f), new Vector3(2.6f, 1.8f, 2.6f));

            // 5. Half-sunken tilted Shipwreck Hull as Makeshift Pirate Shanty Tavern
            registry.Spawn("ship-wreck", root, new Vector3(1.8f, 0.10f, 0.8f), Quaternion.Euler(14f, 115f, -8f), Vector3.one * 1.2f);
            registry.Spawn("mast-ropes", root, new Vector3(1.2f, 0.70f, 0.2f), Quaternion.Euler(-6f, 30f, 10f), Vector3.one * 1.1f);

            // Pirate Jolly Roger Banners
            registry.Spawn("flag-pirate-high", root, new Vector3(1.2f, 4.5f, 0.2f), Quaternion.identity, Vector3.one * 1.4f);
            registry.Spawn("flag-pirate-pennant", root, new Vector3(-3.2f, 2.5f, 1.2f), Quaternion.identity, Vector3.one * 1.2f);

            // Rustic Pirate Jetty & Stilt Platforms (extends seaward into water)
            registry.Spawn("structure-platform-dock-small", root, new Vector3(-1.8f, 0.15f, 3.8f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1.6f, 1.0f, 1.8f));
            registry.Spawn("platform-planks", root, new Vector3(0.5f, 0.15f, 3.2f), Quaternion.identity, Vector3.one * 1.2f);

            // Mobile Coastal Cannons protecting the cove
            registry.Spawn("cannon-mobile", root, new Vector3(-2.8f, 0.20f, 2.5f), Quaternion.Euler(0f, -60f, 0f), Vector3.one * 1.1f);
            registry.Spawn("cannon", root, new Vector3(2.8f, 0.20f, 2.0f), Quaternion.Euler(0f, 45f, 0f), Vector3.one);

            // Pirate Booty, Rum Barrels & Beach Props
            registry.Spawn("chest", root, new Vector3(0.6f, 0.18f, 1.8f), Quaternion.Euler(0f, 35f, 0f), Vector3.one * 1.1f);
            registry.Spawn("barrel", root, new Vector3(0.2f, 0.18f, 1.2f), Quaternion.Euler(75f, 20f, 0f), Vector3.one);
            registry.Spawn("bottle-large", root, new Vector3(1.0f, 0.18f, 2.2f), Quaternion.identity, Vector3.one * 1.1f);

            // Bent tropical palms
            registry.Spawn("palm-bend", root, new Vector3(-1.8f, 0.20f, -2.0f), Quaternion.Euler(0f, 60f, 0f), Vector3.one * 1.2f);
            registry.Spawn("palm-detailed-bend", root, new Vector3(2.0f, 0.20f, -2.8f), Quaternion.Euler(0f, -80f, 0f), Vector3.one * 1.2f);

            // Wire Components
            PortIslandHaven haven = root.gameObject.AddComponent<PortIslandHaven>();
            haven.ConfigurePort(portName, FactionType.CorsairBrethren, 15);

            PortCommodityMarket market = root.gameObject.AddComponent<PortCommodityMarket>();
            market.ConfigureMarket(FactionType.CorsairBrethren, isContrabandDen: true);

            DynamicMarketEconomy economy = root.gameObject.AddComponent<DynamicMarketEconomy>();
            economy.InitializeEconomy(PortArchetype.CorsairHavenFreeport, market);

            root.gameObject.AddComponent<ShipyardDrydock>();
        }
    }
}
