#if UNITY_EDITOR
using System.IO;
using Plunderlust.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Plunderlust.Editor
{
    public static class ShipHullDataCreator
    {
        // [MenuItem("Plunderlust/Generate Ship Hull Profiles")]
        public static void GenerateDefaultShipProfiles()
        {
            string folderPath = "Assets/Settings/ShipData";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }
                AssetDatabase.CreateFolder("Assets/Settings", "ShipData");
            }

            CreateOrUpdateHull(
                path: $"{folderPath}/ShipHull_Tier1_LeakyRowboat.asset",
                tier: ShipTier.Tier1_LeakyRowboat,
                name: "Leaky Rowboat",
                desc: "Fragile wooden rowboat with slow oar propulsion and high vulnerability to ocean leaks.",
                baseMass: 350f,
                maxFloodingMass: 700f,
                probeBuoyancy: 3400f,
                submersionDepth: 0.38f,
                waterlineOffset: 0.28f,
                reserveBuoyancy: 3.5f,
                surfaceAlignment: 16.0f,
                linDrag: 1.2f,
                angDrag: 2.8f,
                comOffset: new Vector3(0f, -0.3f, 0f),
                fullCanvasSpeed: 5.5f,
                battleSailsSpeed: 4.8f,
                rowingSpeed: 5.8f,
                accel: 7.0f,
                rudderTorque: 8.5f,
                banking: 0.15f,
                supportsRowing: true,
                supportsSails: false,
                maxHull: 150f,
                maxSails: 80f,
                maxRudder: 100f,
                maxGunDeck: 0f,
                maxBilge: 450f,
                leakRate: 30f,
                minCrew: 1,
                maxCrew: 2,
                portCannons: 0,
                starboardCannons: 0,
                cargo: 25
            );

            CreateOrUpdateHull(
                path: $"{folderPath}/ShipHull_Tier2_CoastalCutter.asset",
                tier: ShipTier.Tier2_CoastalCutter,
                name: "Coastal Cutter",
                desc: "Swift fore-and-aft rigged sloop with agile helm authority for coastal raiding.",
                baseMass: 1400f,
                maxFloodingMass: 2400f,
                probeBuoyancy: 8500f,
                submersionDepth: 1.1f,
                waterlineOffset: 0.15f,
                reserveBuoyancy: 2.2f,
                surfaceAlignment: 8.0f,
                linDrag: 0.85f,
                angDrag: 2.0f,
                comOffset: new Vector3(0f, -0.6f, 0f),
                fullCanvasSpeed: 13.0f,
                battleSailsSpeed: 6.8f,
                rowingSpeed: 2.5f,
                accel: 4.8f,
                rudderTorque: 7.5f,
                banking: 0.35f,
                supportsRowing: false,
                supportsSails: true,
                maxHull: 450f,
                maxSails: 280f,
                maxRudder: 240f,
                maxGunDeck: 320f,
                maxBilge: 950f,
                leakRate: 22f,
                minCrew: 4,
                maxCrew: 12,
                portCannons: 2,
                starboardCannons: 2,
                cargo: 45
            );

            CreateOrUpdateHull(
                path: $"{folderPath}/ShipHull_Tier3_PirateBrigantine.asset",
                tier: ShipTier.Tier3_PirateBrigantine,
                name: "Pirate Brigantine",
                desc: "Two-masted pirate warship balancing formidable broadsides with superior downwind speed.",
                baseMass: 3200f,
                maxFloodingMass: 5500f,
                probeBuoyancy: 18000f,
                submersionDepth: 1.6f,
                waterlineOffset: 0.1f,
                reserveBuoyancy: 1.8f,
                surfaceAlignment: 4.5f,
                linDrag: 0.95f,
                angDrag: 2.2f,
                comOffset: new Vector3(0f, -0.9f, 0f),
                fullCanvasSpeed: 15.0f,
                battleSailsSpeed: 7.8f,
                rowingSpeed: 0f,
                accel: 4.2f,
                rudderTorque: 9.0f,
                banking: 0.45f,
                supportsRowing: false,
                supportsSails: true,
                maxHull: 900f,
                maxSails: 550f,
                maxRudder: 450f,
                maxGunDeck: 650f,
                maxBilge: 1800f,
                leakRate: 18f,
                minCrew: 10,
                maxCrew: 32,
                portCannons: 4,
                starboardCannons: 4,
                cargo: 100
            );

            CreateOrUpdateHull(
                path: $"{folderPath}/ShipHull_Tier4_RoyalGalleon.asset",
                tier: ShipTier.Tier4_RoyalGalleon,
                name: "Royal Galleon",
                desc: "Three-decked naval fortress capable of devastating double broadsides with massive cargo hold.",
                baseMass: 7500f,
                maxFloodingMass: 12000f,
                probeBuoyancy: 38000f,
                submersionDepth: 2.2f,
                waterlineOffset: 0.05f,
                reserveBuoyancy: 1.5f,
                surfaceAlignment: 2.0f,
                linDrag: 1.1f,
                angDrag: 2.8f,
                comOffset: new Vector3(0f, -1.2f, 0f),
                fullCanvasSpeed: 12.0f,
                battleSailsSpeed: 6.0f,
                rowingSpeed: 0f,
                accel: 2.8f,
                rudderTorque: 12.0f,
                banking: 0.30f,
                supportsRowing: false,
                supportsSails: true,
                maxHull: 2000f,
                maxSails: 1100f,
                maxRudder: 900f,
                maxGunDeck: 1400f,
                maxBilge: 3800f,
                leakRate: 14f,
                minCrew: 25,
                maxCrew: 80,
                portCannons: 8,
                starboardCannons: 8,
                cargo: 250
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Plunderlust] All 4 Ship Hull Data Profiles successfully generated at Assets/Settings/ShipData/");
        }

        private static void CreateOrUpdateHull(
            string path,
            ShipTier tier,
            string name,
            string desc,
            float baseMass,
            float maxFloodingMass,
            float probeBuoyancy,
            float submersionDepth,
            float waterlineOffset,
            float reserveBuoyancy,
            float surfaceAlignment,
            float linDrag,
            float angDrag,
            Vector3 comOffset,
            float fullCanvasSpeed,
            float battleSailsSpeed,
            float rowingSpeed,
            float accel,
            float rudderTorque,
            float banking,
            bool supportsRowing,
            bool supportsSails,
            float maxHull,
            float maxSails,
            float maxRudder,
            float maxGunDeck,
            float maxBilge,
            float leakRate,
            int minCrew,
            int maxCrew,
            int portCannons,
            int starboardCannons,
            int cargo)
        {
            ShipHullDataSO asset = AssetDatabase.LoadAssetAtPath<ShipHullDataSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ShipHullDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject so = new SerializedObject(asset);
            so.FindProperty("_tier").enumValueIndex = (int)tier - 1;
            so.FindProperty("_shipClassName").stringValue = name;
            so.FindProperty("_description").stringValue = desc;

            so.FindProperty("_baseMass").floatValue = baseMass;
            so.FindProperty("_maxFloodingMass").floatValue = maxFloodingMass;
            so.FindProperty("_buoyantForcePerProbe").floatValue = probeBuoyancy;
            so.FindProperty("_probeSubmersionDepth").floatValue = submersionDepth;
            so.FindProperty("_waterlineOffset").floatValue = waterlineOffset;
            so.FindProperty("_reserveBuoyancyScale").floatValue = reserveBuoyancy;
            so.FindProperty("_surfaceAlignmentTorque").floatValue = surfaceAlignment;

            so.FindProperty("_linearDrag").floatValue = linDrag;
            so.FindProperty("_angularDrag").floatValue = angDrag;
            so.FindProperty("_centerOfMassOffset").vector3Value = comOffset;

            so.FindProperty("_fullCanvasMaxSpeed").floatValue = fullCanvasSpeed;
            so.FindProperty("_battleSailsMaxSpeed").floatValue = battleSailsSpeed;
            so.FindProperty("_rowingSpeed").floatValue = rowingSpeed;
            so.FindProperty("_forwardAcceleration").floatValue = accel;
            so.FindProperty("_rudderTorque").floatValue = rudderTorque;
            so.FindProperty("_turnBankingFactor").floatValue = banking;

            so.FindProperty("_supportsRowing").boolValue = supportsRowing;
            so.FindProperty("_supportsSails").boolValue = supportsSails;

            so.FindProperty("_maxHullHealth").floatValue = maxHull;
            so.FindProperty("_maxSailsHealth").floatValue = maxSails;
            so.FindProperty("_maxRudderHealth").floatValue = maxRudder;
            so.FindProperty("_maxGunDeckHealth").floatValue = maxGunDeck;
            so.FindProperty("_maxBilgeWaterCapacity").floatValue = maxBilge;
            so.FindProperty("_baseLeakRatePerSec").floatValue = leakRate;

            so.FindProperty("_minRequiredCrew").intValue = minCrew;
            so.FindProperty("_maxCrewCapacity").intValue = maxCrew;
            so.FindProperty("_portCannonsCount").intValue = portCannons;
            so.FindProperty("_starboardCannonsCount").intValue = starboardCannons;
            so.FindProperty("_cargoCapacity").intValue = cargo;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
#endif
