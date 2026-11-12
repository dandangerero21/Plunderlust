using System.Collections.Generic;
using Plunderlust.AI;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.UI;
using Plunderlust.World;
using UnityEngine;

namespace Plunderlust.Quests
{
    [System.Serializable]
    public class BountyContract
    {
        public string BountyId;
        public string CaptainName;
        public string ShipName;
        public ShipTier TargetTier;
        public bool IsHardContract;
        public FactionType Faction;
        public int GoldReward;
        public Vector3 TargetSpawnLocation;
        public string SectorDescription;
        public bool IsActive;
        public bool IsFulfilled;
    }

    /// <summary>
    /// Phase 7 Port Haven Bounty Noticeboard & High Seas Hunting Quests Coordinator.
    /// Manages active contracts, guarantees distant boss spawns, and awards gold upon target defeat.
    /// Operates with zero heap allocations during gameplay loops.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class BountyQuestManager : MonoBehaviour
    {
        private static BountyQuestManager _instance;
        public static BountyQuestManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<BountyQuestManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("BountyQuestManager");
                        _instance = go.AddComponent<BountyQuestManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public BountyContract ActiveBounty { get; private set; }
        public readonly List<BountyContract> AvailableContracts = new List<BountyContract>();

        private GameObject _spawnedTargetGo;

        // Authentic 17th-Century Caribbean Pirate & Rogue Captain Name Pools
        private static readonly string[] s_PirateCaptains = new string[]
        {
            "Captain 'Bloodtide' Vane", "Blackbeard 'One-Eye' Drake", "Edward 'Gallows' Thorne",
            "Mad Dog Higgins", "Calico Jack Bonny", "Redbeard O'Malley",
            "Captain Morgan Ironhand", "Bartholomew 'Reaper' Flint", "Barnaby 'Cutlass' Black",
            "Anne 'Sea Wolf' Read", "Cutpurse Pete", "Drunken Barnaby"
        };

        private static readonly string[] s_NavyDefectors = new string[]
        {
            "Commodore Sterling (Rogue)", "Captain Hawksley (Mutineer)",
            "Commander Blackwood", "Admiral Vance (Turncoat)",
            "Lieutenant Drake (Privateer)", "Captain Dauntless Thorne"
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        public void RefreshAvailableBounties(Vector3 portPos)
        {
            AvailableContracts.Clear();

            ShipTier playerTier = NavalAIManager.Instance != null 
                ? NavalAIManager.Instance.GetPlayerShipTier() 
                : ShipTier.Tier1_LeakyRowboat;

            // 1. Normal Bounty A (Player Tier)
            AvailableContracts.Add(GenerateContract(portPos, playerTier, false, 0));

            // 2. Normal Bounty B (Player Tier)
            AvailableContracts.Add(GenerateContract(portPos, playerTier, false, 1));

            // 3. HARD Bounty (+1 Tier Higher, High Risk & High Reward!)
            ShipTier hardTier = (ShipTier)Mathf.Min((int)playerTier + 1, (int)ShipTier.Tier5_GhostGalleon);
            AvailableContracts.Add(GenerateContract(portPos, hardTier, true, 2));
        }

        private BountyContract GenerateContract(Vector3 portPos, ShipTier tier, bool isHard, int index)
        {
            string bountyId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            FactionType faction = (index % 2 == 0) ? FactionType.CorsairBrethren : FactionType.ImperialNavy;

            string captainName = faction == FactionType.CorsairBrethren
                ? s_PirateCaptains[Random.Range(0, s_PirateCaptains.Length)]
                : s_NavyDefectors[Random.Range(0, s_NavyDefectors.Length)];

            string shipName = NavalAIManager.GeneratePirateShipName(faction);

            // Reward scaling based on tier and difficulty
            int baseReward = (int)tier * 180 + Random.Range(30, 80);
            if (isHard)
            {
                baseReward = Mathf.RoundToInt(baseReward * 1.85f + 160);
            }

            // Guaranteed open-sea hunting spawn position (160m to 250m away from accepting port)
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(160f, 250f);
            Vector3 rawPos = portPos + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            // Clamp inside playable map perimeter (MapBoundaryBarrier is 550m half size)
            float maxExtent = 480f;
            rawPos.x = Mathf.Clamp(rawPos.x, -maxExtent, maxExtent);
            rawPos.z = Mathf.Clamp(rawPos.z, -maxExtent, maxExtent);

            Vector3 safePos = NavalAIManager.FindSafeSpawnPoint(rawPos);

            // Sector description
            string sector = (safePos.z > 80f ? "North" : safePos.z < -80f ? "South" : "Central") + " " +
                           (safePos.x > 80f ? "East" : safePos.x < -80f ? "West" : "Open") + " Waters";

            return new BountyContract
            {
                BountyId = bountyId,
                CaptainName = captainName,
                ShipName = shipName,
                TargetTier = tier,
                IsHardContract = isHard,
                Faction = faction,
                GoldReward = baseReward,
                TargetSpawnLocation = safePos,
                SectorDescription = sector,
                IsActive = false,
                IsFulfilled = false
            };
        }

        public bool AcceptBounty(BountyContract contract)
        {
            if (contract == null) return false;

            // Only one active bounty held at a time!
            if (ActiveBounty != null && !ActiveBounty.IsFulfilled)
            {
                Debug.LogWarning("[BountyQuestManager] Already holding an active bounty contract!");
                return false;
            }

            // Clean up previous target if any
            if (_spawnedTargetGo != null)
            {
                Destroy(_spawnedTargetGo);
                _spawnedTargetGo = null;
            }

            ActiveBounty = contract;
            ActiveBounty.IsActive = true;

            // Guarantee the target ship spawns far away!
            if (NavalAIManager.Instance != null)
            {
                AIShipBrain bossBrain = NavalAIManager.Instance.SpawnBountyTargetShip(
                    contract.CaptainName,
                    contract.TargetTier,
                    contract.Faction,
                    contract.TargetSpawnLocation,
                    contract.BountyId
                );

                if (bossBrain != null)
                {
                    _spawnedTargetGo = bossBrain.gameObject;
                }
            }

            // Lock horizon waypoint beacon to target position
            if (WaypointBeacon.Instance != null)
            {
                WaypointBeacon.Instance.SetWaypoint(contract.TargetSpawnLocation, $"BOUNTY: {contract.CaptainName}");
            }

            Debug.Log($"<color=gold>[Bounty Accepted]</color> Hunting {contract.CaptainName} ({contract.TargetTier}) in {contract.SectorDescription}! Reward: {contract.GoldReward}g");
            return true;
        }

        public void AbandonBounty()
        {
            if (ActiveBounty == null) return;

            if (_spawnedTargetGo != null)
            {
                Destroy(_spawnedTargetGo);
                _spawnedTargetGo = null;
            }

            ActiveBounty = null;

            if (WaypointBeacon.Instance != null)
            {
                WaypointBeacon.Instance.ClearWaypoint();
            }

            Debug.Log("[BountyQuestManager] Active bounty abandoned.");
        }

        public void OnBountyTargetDefeated(string bountyId)
        {
            if (ActiveBounty == null) return;
            if (ActiveBounty.BountyId != bountyId) return;
            if (ActiveBounty.IsFulfilled) return;

            ActiveBounty.IsFulfilled = true;
            int reward = ActiveBounty.GoldReward;
            string captain = ActiveBounty.CaptainName;

            // Deposit gold reward directly to player's cargo hold
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                ShipCargoHold hold = playerGo.GetComponent<ShipCargoHold>();
                if (hold != null)
                {
                    hold.AddCargo(CargoItemType.Gold, reward, out _);
                }
            }

            // Trigger HUD fanfare banner
            if (PirateCombatHUD.Instance != null)
            {
                PirateCombatHUD.Instance.ShowBanner($"BOUNTY CLAIMED: +{reward} GOLD! ({captain} Defeated)", new Color(1f, 0.85f, 0.2f), 4.5f);
            }

            // Clear Waypoint Beacon
            if (WaypointBeacon.Instance != null)
            {
                WaypointBeacon.Instance.ClearWaypoint();
            }

            ActiveBounty = null;
            _spawnedTargetGo = null;
            Debug.Log($"<color=green><b>[BOUNTY FULFILLED!]</b></color> Awarded {reward} Gold for {captain}!");
        }
    }
}
