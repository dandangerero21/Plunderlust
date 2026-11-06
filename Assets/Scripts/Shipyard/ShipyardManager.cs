using System;
using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Combat;
using Plunderlust.Core.Data;
using Plunderlust.Core.Persistence;
using Plunderlust.Crew;
using Plunderlust.Ocean;
using Plunderlust.Ship;
using Plunderlust.Subsystems;
using Plunderlust.UI;
using Plunderlust.World;
using UnityEngine;

namespace Plunderlust.Shipyard
{
    /// <summary>
    /// Phase 5 Shipyard Refit & Hull Transformation Controller.
    /// Manages drydock hull upgrades (Rowboat -> Coastal Cutter -> Pirate Brigantine),
    /// preserving cargo hold contents, treasury gold, active officers, and deckhands
    /// while reconstructing colliders, buoyancy probes, and broadside hardpoints.
    /// </summary>
    public class ShipyardManager : MonoBehaviour
    {
        public static ShipyardManager Instance { get; private set; }

        public event Action<ShipTier> OnShipUpgraded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool CanUpgradeTo(GameObject playerGo, ShipTier targetTier, out string missingReason)
        {
            missingReason = string.Empty;
            if (playerGo == null)
            {
                missingReason = "No ship found";
                return false;
            }

            ShipCargoHold cargo = playerGo.GetComponent<ShipCargoHold>();
            ShipSubsystemController sub = playerGo.GetComponent<ShipSubsystemController>();

            if (sub == null || sub.HullData == null)
            {
                missingReason = "Missing hull data";
                return false;
            }

            ShipTier currentTier = sub.HullData.Tier;
            if (currentTier >= targetTier)
            {
                missingReason = "Already at or above this hull tier";
                return false;
            }

            GetUpgradeCost(targetTier, out int reqGold, out int reqTimber, out int reqCanvas);

            if (cargo.CurrentGold < reqGold)
            {
                missingReason = $"Requires {reqGold} Gold (Have: {cargo.CurrentGold})";
                return false;
            }

            if (!cargo.HasCargo(CargoItemType.Timber, reqTimber))
            {
                missingReason = $"Requires {reqTimber}x Timber (Have: {cargo.GetItemCount(CargoItemType.Timber)})";
                return false;
            }

            if (!cargo.HasCargo(CargoItemType.Canvas, reqCanvas))
            {
                missingReason = $"Requires {reqCanvas}x Canvas (Have: {cargo.GetItemCount(CargoItemType.Canvas)})";
                return false;
            }

            return true;
        }

        public void GetUpgradeCost(ShipTier targetTier, out int gold, out int timber, out int canvas)
        {
            switch (targetTier)
            {
                case ShipTier.Tier2_CoastalCutter:
                    gold = 350;
                    timber = 8;
                    canvas = 4;
                    break;
                case ShipTier.Tier3_PirateBrigantine:
                    gold = 900;
                    timber = 20;
                    canvas = 12;
                    break;
                case ShipTier.Tier4_RoyalGalleon:
                    gold = 2200;
                    timber = 45;
                    canvas = 25;
                    break;
                default:
                    gold = 0;
                    timber = 0;
                    canvas = 0;
                    break;
            }
        }

        public bool ExecuteHullUpgrade(GameObject playerGo, ShipTier targetTier)
        {
            if (!CanUpgradeTo(playerGo, targetTier, out string reason))
            {
                Debug.LogWarning($"[ShipyardManager] Cannot refit hull: {reason}");
                return false;
            }

            GetUpgradeCost(targetTier, out int reqGold, out int reqTimber, out int reqCanvas);

            ShipCargoHold cargo = playerGo.GetComponent<ShipCargoHold>();
            cargo.ConsumeCargo(CargoItemType.Gold, reqGold);
            cargo.ConsumeCargo(CargoItemType.Timber, reqTimber);
            cargo.ConsumeCargo(CargoItemType.Canvas, reqCanvas);

            bool success = TransformShipHull(playerGo, targetTier, notifyHUD: true);

            if (ShipSaveManager.Instance != null)
            {
                ShipSaveManager.Instance.SaveGame(playerGo);
            }

            if (success)
            {
                OnShipUpgraded?.Invoke(targetTier);
            }
            return success;
        }

        /// <summary>
        /// Transforms the given ship GameObject into the target hull tier.
        /// Swaps Kenney 3D mesh, removes rowboat paddles if applicable, reconfigures BoxCollider,
        /// Rigidbody mass, Buoyancy probes, and assigns target ShipHullDataSO to all subsystems.
        /// Does NOT deduct gold or materials (safe for save loading and shipyard refits).
        /// </summary>
        public static bool TransformShipHull(GameObject playerGo, ShipTier targetTier, bool notifyHUD = false)
        {
            if (playerGo == null) return false;

            // Determine Target Hull Profile & Model Path
            string profilePath;
            string modelName;
            Vector3 colCenter;
            Vector3 colSize;
            float mass;
            float probeFwd;
            float probeLat;
            float targetScale;

            if (targetTier == ShipTier.Tier1_LeakyRowboat)
            {
                profilePath = "Assets/Settings/ShipData/ShipHull_Tier1_LeakyRowboat.asset";
                modelName = "boat-row-small";
                targetScale = 1.0f;
                colCenter = new Vector3(0f, 0.4f, 0f);
                colSize = new Vector3(1.2f, 0.8f, 2.4f);
                mass = 200f;
                probeFwd = 1.0f;
                probeLat = 0.5f;
            }
            else if (targetTier == ShipTier.Tier2_CoastalCutter)
            {
                profilePath = "Assets/Settings/ShipData/ShipHull_Tier2_CoastalCutter.asset";
                modelName = "ship-small";
                targetScale = 0.55f; // Sleek single-mast sloop: length ~4.8m (2x rowboat)
                colCenter = new Vector3(0f, 0.9f, 0f);
                colSize = new Vector3(2.0f, 2.0f, 4.8f);
                mass = 650f;
                probeFwd = 2.1f;
                probeLat = 0.95f;
            }
            else if (targetTier == ShipTier.Tier3_PirateBrigantine)
            {
                profilePath = "Assets/Settings/ShipData/ShipHull_Tier3_PirateBrigantine.asset";
                modelName = "ship-medium";
                targetScale = 0.85f; // Twin-mast warship: length ~9.0m
                colCenter = new Vector3(0f, 1.2f, 0f);
                colSize = new Vector3(3.0f, 2.6f, 8.8f);
                mass = 1400f;
                probeFwd = 3.8f;
                probeLat = 1.4f;
            }
            else // Tier 4 Royal Galleon
            {
                profilePath = "Assets/Settings/ShipData/ShipHull_Tier4_RoyalGalleon.asset";
                modelName = "ship-large";
                targetScale = 1.15f; // Triple-deck naval fortress: length ~15.0m
                colCenter = new Vector3(0f, 1.6f, 0f);
                colSize = new Vector3(4.0f, 3.2f, 13.5f);
                mass = 2800f;
                probeFwd = 5.8f;
                probeLat = 2.0f;
            }

            ShipHullDataSO targetProfile = null;
#if UNITY_EDITOR
            targetProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<ShipHullDataSO>(profilePath);
#endif
            if (targetProfile == null)
            {
                string profFileName = System.IO.Path.GetFileNameWithoutExtension(profilePath);
                targetProfile = Resources.Load<ShipHullDataSO>("ShipData/" + profFileName);
            }
            if (targetProfile == null)
            {
                targetProfile = Resources.Load<ShipHullDataSO>(targetTier.ToString());
            }

            // 1. Remove existing visual model children (boat-row-small, BoatModel, ShipModel, Turntable models, etc.)
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < playerGo.transform.childCount; i++)
            {
                Transform child = playerGo.transform.GetChild(i);
                string cName = child.name;

                // Preserve functional systems & persistent anchors
                if (cName.Equals("BuoyancyProbes", StringComparison.OrdinalIgnoreCase) ||
                    cName.StartsWith("Port_Hardpoint", StringComparison.OrdinalIgnoreCase) ||
                    cName.StartsWith("Starboard_Hardpoint", StringComparison.OrdinalIgnoreCase) ||
                    cName.Contains("Pennant") || cName.Contains("HealthBar") || cName.Contains("HUD"))
                {
                    continue;
                }

                if (cName.Equals("BoatModel", StringComparison.OrdinalIgnoreCase) ||
                    cName.Equals("ShipModel", StringComparison.OrdinalIgnoreCase) ||
                    cName.StartsWith("boat-row", StringComparison.OrdinalIgnoreCase) ||
                    cName.StartsWith("ship-", StringComparison.OrdinalIgnoreCase) ||
                    cName.StartsWith("Turntable_", StringComparison.OrdinalIgnoreCase))
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (var go in toDestroy)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(go);
                else
                    Destroy(go);
#else
                Destroy(go);
#endif
            }

            // Also remove or disable BoatPaddleAnimator when moving beyond rowboat
            BoatPaddleAnimator paddles = playerGo.GetComponent<BoatPaddleAnimator>();
            if (targetTier > ShipTier.Tier1_LeakyRowboat)
            {
                if (paddles != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(paddles);
                    else
                        Destroy(paddles);
#else
                    Destroy(paddles);
#endif
                }
            }

            // 2. Instantiate new Kenney ship model using PrefabRegistrySO or direct AssetDatabase load
            GameObject newModel = null;
            if (PrefabRegistrySO.Instance != null)
            {
                newModel = PrefabRegistrySO.Instance.Spawn(modelName, playerGo.transform, Vector3.zero, Quaternion.identity, Vector3.one * targetScale, null, false);
            }

            if (newModel == null)
            {
#if UNITY_EDITOR
                string fbxPath = $"Assets/kenney_pirate-kit/Models/FBX format/{modelName}.fbx";
                GameObject modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (modelPrefab != null)
                {
                    newModel = Instantiate(modelPrefab, playerGo.transform);
                    newModel.transform.localPosition = Vector3.zero;
                    newModel.transform.localRotation = Quaternion.identity;
                    newModel.transform.localScale = Vector3.one * targetScale;

                    Material paletteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                    if (paletteMat != null)
                    {
                        Renderer[] rends = newModel.GetComponentsInChildren<Renderer>();
                        foreach (var r in rends) r.sharedMaterial = paletteMat;
                    }
                }
#endif
            }

            if (newModel != null)
            {
                newModel.name = "ShipModel";
            }

            // 3. Update BoxCollider and Rigidbody
            BoxCollider col = playerGo.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.center = colCenter;
                col.size = colSize;
            }

            Rigidbody rb = playerGo.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = mass;
            }

            // 4. Update Buoyancy Probes
            Transform probesRoot = playerGo.transform.Find("BuoyancyProbes");
            if (probesRoot != null)
            {
                Transform bow = probesRoot.Find("Probe_Bow");
                if (bow != null) bow.localPosition = new Vector3(0f, 0f, probeFwd);

                Transform stern = probesRoot.Find("Probe_Stern");
                if (stern != null) stern.localPosition = new Vector3(0f, 0f, -probeFwd);

                Transform port = probesRoot.Find("Probe_Port");
                if (port != null) port.localPosition = new Vector3(-probeLat, 0f, 0f);

                Transform stbd = probesRoot.Find("Probe_Starboard");
                if (stbd != null) stbd.localPosition = new Vector3(probeLat, 0f, 0f);
            }

            // 5. Update Profile on All Subsystems
            if (targetProfile != null)
            {
                ShipSubsystemController sub = playerGo.GetComponent<ShipSubsystemController>();
                if (sub != null) sub.SetHullData(targetProfile);

                ShipLocomotionController loco = playerGo.GetComponent<ShipLocomotionController>();
                if (loco != null) loco.SetHullData(targetProfile);

                NavalCombatController combat = playerGo.GetComponent<NavalCombatController>();
                if (combat != null) combat.SetHullData(targetProfile);

                ShipCargoHold hold = playerGo.GetComponent<ShipCargoHold>();
                if (hold != null) hold.SetHullData(targetProfile);

                BuoyancyBody buoyancy = playerGo.GetComponent<BuoyancyBody>();
                if (buoyancy != null) buoyancy.SetHullData(targetProfile);

                CrewManager crew = playerGo.GetComponent<CrewManager>();
                if (crew != null) crew.SetHullData(targetProfile);
            }

            // 5b. Update Identity System to New Authentic Tier
            ShipIdentitySystem identity = playerGo.GetComponent<ShipIdentitySystem>();
            if (identity != null)
            {
                identity.SetTier(targetTier);
            }

            if (MobileInputBridge.Instance != null)
            {
                MobileInputBridge.Instance.UpdateWeaponTriggerLayout();
            }

            // 6. Refresh Pennant & Health Bar
            Factions.FactionPennantVisual pennant = playerGo.GetComponent<Factions.FactionPennantVisual>();
            if (pennant != null) pennant.RefreshPennants();

            // 7. Notification (if requested, e.g. drydock refit)
            if (notifyHUD && PirateCombatHUD.Instance != null)
            {
                string upgradedName = targetTier switch
                {
                    ShipTier.Tier2_CoastalCutter => "COASTAL CUTTER",
                    ShipTier.Tier3_PirateBrigantine => "PIRATE BRIGANTINE",
                    ShipTier.Tier4_RoyalGalleon => "ROYAL GALLEON",
                    _ => targetTier.ToString().ToUpper()
                };
                PirateCombatHUD.Instance.ShowBanner($"DRYDOCK REFIT COMPLETE: {upgradedName}!", Color.green, 4.0f);
            }

            Debug.Log($"<color=green>[ShipyardManager]</color> Successfully transformed flagship to {targetTier}!");
            return true;
        }

        public bool RepairAllSubsystems(GameObject playerGo, int goldCost)
        {
            if (playerGo == null) return false;
            ShipCargoHold cargo = playerGo.GetComponent<ShipCargoHold>();
            ShipSubsystemController sub = playerGo.GetComponent<ShipSubsystemController>();

            if (cargo == null || sub == null) return false;

            if (cargo.CurrentGold < goldCost)
            {
                Debug.LogWarning("[Shipyard] Not enough gold for complete overhaul!");
                return false;
            }

            cargo.ConsumeCargo(CargoItemType.Gold, goldCost);
            sub.RepairSubsystem(ShipSubsystemType.Hull, sub.MaxHullHealth);
            sub.RepairSubsystem(ShipSubsystemType.SailsRigging, sub.MaxSailsHealth);
            sub.RepairSubsystem(ShipSubsystemType.RudderHelm, sub.MaxRudderHealth);
            sub.RepairSubsystem(ShipSubsystemType.GunDeck, sub.MaxGunDeckHealth);
            sub.PumpBilgeWater(sub.CurrentBilgeWater); // pump completely dry

            if (PirateCombatHUD.Instance != null)
            {
                PirateCombatHUD.Instance.ShowBanner("SHIP OVERHAUL COMPLETE: 100% HEALTH!", Color.green, 3.5f);
            }

            return true;
        }
    }
}
