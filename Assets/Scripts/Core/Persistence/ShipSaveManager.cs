using System;
using System.IO;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Factions;
using Plunderlust.Ship;
using Plunderlust.Shipyard;
using Plunderlust.Subsystems;
using UnityEngine;

namespace Plunderlust.Core.Persistence
{
    /// <summary>
    /// Phase 5 JSON Game Persistence Manager.
    /// Serializes complete ship hull tier, subsystem integrity, cargo inventory,
    /// treasury gold, crew roster, and faction standings to disk.
    /// Auto-saves on harbor mooring and auto-loads on session start.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class ShipSaveManager : MonoBehaviour
    {
        public static ShipSaveManager Instance { get; private set; }

        public ShipSaveDTO ActiveSave { get; private set; }

        public static string PrimarySaveFilePath => Path.Combine(Application.persistentDataPath, "plunderlust_save.json");
        public static string FallbackSaveFilePath => Path.Combine(Application.dataPath, "../SaveData/plunderlust_save.json");
        public static string SaveFilePath => ResolvedSaveFilePath;

        public static string ResolvedSaveFilePath
        {
            get
            {
                try
                {
                    bool primaryExists = false;
                    try { primaryExists = File.Exists(PrimarySaveFilePath); } catch { }

                    bool fallbackExists = false;
                    try { fallbackExists = File.Exists(FallbackSaveFilePath); } catch { }

                    if (primaryExists && fallbackExists)
                    {
                        DateTime primaryTime = File.GetLastWriteTimeUtc(PrimarySaveFilePath);
                        DateTime fallbackTime = File.GetLastWriteTimeUtc(FallbackSaveFilePath);
                        return (fallbackTime > primaryTime) ? FallbackSaveFilePath : PrimarySaveFilePath;
                    }
                    if (primaryExists) return PrimarySaveFilePath;
                    if (fallbackExists) return FallbackSaveFilePath;
                }
                catch { }
                return PrimarySaveFilePath;
            }
        }

        public static bool SaveExists()
        {
            try
            {
                if (File.Exists(PrimarySaveFilePath)) return true;
            }
            catch { }

            try
            {
                if (File.Exists(FallbackSaveFilePath)) return true;
            }
            catch { }

            return false;
        }

        public event Action OnGameSaved;
        public event Action OnGameLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool HasSaveFile()
        {
            return SaveExists();
        }

        public void DeleteSave()
        {
            try
            {
                if (File.Exists(PrimarySaveFilePath)) File.Delete(PrimarySaveFilePath);
                if (File.Exists(FallbackSaveFilePath)) File.Delete(FallbackSaveFilePath);
                Debug.Log("[ShipSaveManager] Save files deleted.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShipSaveManager] DeleteSave exception: {ex.Message}");
            }
        }

        public bool SaveGame(GameObject playerShip)
        {
            if (playerShip == null) return false;

            try
            {
                ShipSaveDTO dto = new ShipSaveDTO();

                // 1. Transform Coordinates
                dto.SetTransform(playerShip.transform.position, playerShip.transform.rotation);

                // Identity & Naming
                ShipIdentitySystem identity = playerShip.GetComponent<ShipIdentitySystem>();
                if (identity != null)
                {
                    dto.shipCustomName = identity.ChristenedName;
                    dto.tier = identity.CurrentTier;
                }

                // 2. Subsystems & Hull Integrity
                ShipSubsystemController sub = playerShip.GetComponent<ShipSubsystemController>();
                if (sub != null)
                {
                    dto.hullHealth = sub.HullHealth;
                    dto.sailsHealth = sub.SailsHealth;
                    dto.rudderHealth = sub.RudderHealth;
                    dto.gunDeckHealth = sub.GunDeckHealth;
                    dto.currentBilgeWater = sub.CurrentBilgeWater;
                    if (sub.HullData != null && sub.HullData.Tier > dto.tier)
                    {
                        dto.tier = sub.HullData.Tier;
                    }
                }

                // 3. Cargo Hold & Treasury
                ShipCargoHold cargo = playerShip.GetComponent<ShipCargoHold>();
                if (cargo != null)
                {
                    dto.currentGold = cargo.CurrentGold;
                    dto.cargoInventory = cargo.ExportCargoDTO();
                }

                // 4. Crew Manpower & Officers
                CrewManager crew = playerShip.GetComponent<CrewManager>();
                if (crew != null)
                {
                    dto.currentDeckhands = crew.CurrentDeckhands;
                    var officers = crew.GetAssignedOfficers();
                    var offList = new OfficerSaveDTO[officers.Count];
                    for (int i = 0; i < officers.Count; i++)
                    {
                        var off = officers[i];
                        offList[i] = new OfficerSaveDTO(off.name, off.OfficerName, off.Specialty, off.Tier, off.PrimaryPassiveValue);
                    }
                    dto.assignedOfficers = offList;
                }

                // 5. Faction Standings & Bounty
                if (FactionManager.Instance != null)
                {
                    dto.imperialNavyReputation = FactionManager.Instance.GetReputation(FactionType.ImperialNavy);
                    dto.merchantGuildReputation = FactionManager.Instance.GetReputation(FactionType.MerchantGuild);
                    dto.corsairBrethrenReputation = FactionManager.Instance.GetReputation(FactionType.CorsairBrethren);
                    dto.freeportTradersReputation = FactionManager.Instance.GetReputation(FactionType.FreeportTraders);
                    dto.activeBounty = FactionManager.Instance.ActiveBounty;
                }

                string json = dto.ToJson(true);
                bool wrotePrimary = false;
                bool wroteFallback = false;

                // 1. Write Primary Path (Application.persistentDataPath)
                try
                {
                    string primaryDir = Path.GetDirectoryName(PrimarySaveFilePath);
                    if (!string.IsNullOrEmpty(primaryDir) && !Directory.Exists(primaryDir))
                    {
                        Directory.CreateDirectory(primaryDir);
                    }
                    File.WriteAllText(PrimarySaveFilePath, json);
                    wrotePrimary = true;
                    Debug.Log($"<color=green>[ShipSaveManager]</color> Saved game to primary path: {PrimarySaveFilePath}");
                }
                catch (Exception pEx)
                {
                    Debug.LogWarning($"[ShipSaveManager] Could not write primary save ({PrimarySaveFilePath}): {pEx.Message}");
                }

                // 2. Write Fallback Path (Application.dataPath/../SaveData/)
                try
                {
                    string fallbackDir = Path.GetDirectoryName(FallbackSaveFilePath);
                    if (!string.IsNullOrEmpty(fallbackDir) && !Directory.Exists(fallbackDir))
                    {
                        Directory.CreateDirectory(fallbackDir);
                    }
                    File.WriteAllText(FallbackSaveFilePath, json);
                    wroteFallback = true;
                    Debug.Log($"<color=green>[ShipSaveManager]</color> Saved game to fallback path: {FallbackSaveFilePath}");
                }
                catch (Exception fEx)
                {
                    Debug.LogWarning($"[ShipSaveManager] Could not write fallback save ({FallbackSaveFilePath}): {fEx.Message}");
                }

                if (wrotePrimary || wroteFallback)
                {
                    ActiveSave = dto;
                    OnGameSaved?.Invoke();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShipSaveManager] Failed to save game: {ex.Message}");
                return false;
            }
        }

        public ShipSaveDTO LoadSaveData()
        {
            if (!SaveExists()) return null;

            try
            {
                string path = ResolvedSaveFilePath;
                Debug.Log($"[ShipSaveManager] Loading save data from: {path}");
                string json = File.ReadAllText(path);
                return ShipSaveDTO.FromJson(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShipSaveManager] Failed to read save file from {ResolvedSaveFilePath}: {ex.Message}. Attempting alternate path...");
                try
                {
                    string altPath = (ResolvedSaveFilePath == PrimarySaveFilePath) ? FallbackSaveFilePath : PrimarySaveFilePath;
                    if (File.Exists(altPath))
                    {
                        string json = File.ReadAllText(altPath);
                        return ShipSaveDTO.FromJson(json);
                    }
                }
                catch { }
                return null;
            }
        }

        public bool ApplySaveToPlayer(GameObject playerShip, ShipSaveDTO dto)
        {
            if (playerShip == null || dto == null) return false;

            try
            {
                ActiveSave = dto;

                // 1. Transform Hull to Saved Tier (Swaps 3D model, colliders, buoyancy probes, and HullData on subsystems)
                if (dto.tier > ShipTier.Tier1_LeakyRowboat)
                {
                    ShipyardManager.TransformShipHull(playerShip, dto.tier, notifyHUD: false);
                }

                // 2. Restore Spatial Coordinates & Heading
                Vector3 savedPos = dto.GetWorldPosition();
                if (savedPos.sqrMagnitude > 0.01f)
                {
                    playerShip.transform.position = savedPos;
                    playerShip.transform.rotation = dto.GetWorldRotation();
                    Rigidbody rb = playerShip.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.position = savedPos;
                        rb.rotation = dto.GetWorldRotation();
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                // 3. Restore Identity & Christened Name
                ShipIdentitySystem identity = playerShip.GetComponent<ShipIdentitySystem>();
                if (identity != null)
                {
                    if (!string.IsNullOrEmpty(dto.shipCustomName))
                    {
                        identity.ChristenShip(dto.shipCustomName);
                    }
                    identity.SetTier(dto.tier);
                }

                // 4. Restore Subsystems Health & Integrity (Now operating with the correct HullData max health!)
                ShipSubsystemController sub = playerShip.GetComponent<ShipSubsystemController>();
                if (sub != null)
                {
                    sub.RepairSubsystem(ShipSubsystemType.Hull, dto.hullHealth);
                    sub.RepairSubsystem(ShipSubsystemType.SailsRigging, dto.sailsHealth);
                    sub.RepairSubsystem(ShipSubsystemType.RudderHelm, dto.rudderHealth);
                    sub.RepairSubsystem(ShipSubsystemType.GunDeck, dto.gunDeckHealth);
                    sub.PumpBilgeWater(sub.CurrentBilgeWater);
                }

                // 5. Restore Cargo Hold & Treasury
                ShipCargoHold cargo = playerShip.GetComponent<ShipCargoHold>();
                if (cargo != null)
                {
                    cargo.ConsumeCargo(CargoItemType.Gold, cargo.CurrentGold); // clear starter gold
                    cargo.AddCargo(CargoItemType.Gold, dto.currentGold, out _);

                    if (dto.cargoInventory != null)
                    {
                        foreach (var slot in dto.cargoInventory)
                        {
                            if (slot.itemType == CargoItemType.Gold) continue;
                            cargo.AddCargo(slot.itemType, slot.quantity, out _);
                        }
                    }
                }

                // 6. Restore Crew Roster & Deckhands
                CrewManager crew = playerShip.GetComponent<CrewManager>();
                if (crew != null)
                {
                    crew.RecruitDeckhands(dto.currentDeckhands - crew.CurrentDeckhands);
                }

                // 7. Restore Factions & Bounty
                if (FactionManager.Instance != null)
                {
                    FactionManager.Instance.ModifyReputation(FactionType.ImperialNavy, dto.imperialNavyReputation - FactionManager.Instance.GetReputation(FactionType.ImperialNavy));
                    FactionManager.Instance.ModifyReputation(FactionType.MerchantGuild, dto.merchantGuildReputation - FactionManager.Instance.GetReputation(FactionType.MerchantGuild));
                    FactionManager.Instance.ModifyReputation(FactionType.CorsairBrethren, dto.corsairBrethrenReputation - FactionManager.Instance.GetReputation(FactionType.CorsairBrethren));
                    FactionManager.Instance.ModifyReputation(FactionType.FreeportTraders, dto.freeportTradersReputation - FactionManager.Instance.GetReputation(FactionType.FreeportTraders));
                    FactionManager.Instance.AddBounty(dto.activeBounty - FactionManager.Instance.ActiveBounty);
                }

                Debug.Log($"<color=cyan>[ShipSaveManager]</color> Successfully restored saved voyage for '{dto.shipCustomName}' (Tier: {dto.tier}, Gold: {dto.currentGold})!");
                OnGameLoaded?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShipSaveManager] Error applying save state: {ex.Message}");
                return false;
            }
        }
    }
}
