using System;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Factions
{
    /// <summary>
    /// Phase 4 Global Faction & Reputation Manager.
    /// Manages diplomatic standing (-100 to +100) across the four core naval factions:
    /// - Imperial Navy
    /// - Merchant Guild
    /// - Corsair Brethren
    /// - Freeport Traders
    /// Calculates relational matrix shifts when ships are attacked or plundered,
    /// and tracks the player's active Bounty with bounty hunter dispatch triggers.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class FactionManager : MonoBehaviour
    {
        public static FactionManager Instance { get; private set; }

        [Header("Starting Standings (-100 to +100)")]
        [SerializeField] private int _imperialNavyRep = 10;
        [SerializeField] private int _merchantGuildRep = 25;
        [SerializeField] private int _corsairBrethrenRep = -15;
        [SerializeField] private int _freeportTradersRep = 15;

        [Header("Pirate Bounty")]
        [SerializeField] private int _activeBounty = 0;
        [SerializeField] private int _hunterDispatchBountyThreshold = 400;

        // Diplomatic Matrix lookup
        private readonly int[] _reputations = new int[4];

        // Events
        public event Action<FactionType, int, FactionStandingTier> OnReputationChanged;
        public event Action<int> OnBountyChanged;
        public event Action<FactionType> OnHunterSquadronDispatched;

        public int ActiveBounty => _activeBounty;
        public int HunterThreshold => _hunterDispatchBountyThreshold;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _reputations[(int)FactionType.ImperialNavy] = Mathf.Clamp(_imperialNavyRep, -100, 100);
            _reputations[(int)FactionType.MerchantGuild] = Mathf.Clamp(_merchantGuildRep, -100, 100);
            _reputations[(int)FactionType.CorsairBrethren] = Mathf.Clamp(_corsairBrethrenRep, -100, 100);
            _reputations[(int)FactionType.FreeportTraders] = Mathf.Clamp(_freeportTradersRep, -100, 100);
        }

        public int GetReputation(FactionType faction)
        {
            return _reputations[(int)faction];
        }

        public FactionStandingTier GetStandingTier(FactionType faction)
        {
            int rep = _reputations[(int)faction];
            if (rep <= -60) return FactionStandingTier.Hunted;
            if (rep <= -20) return FactionStandingTier.Hostile;
            if (rep <= 19) return FactionStandingTier.Neutral;
            if (rep <= 59) return FactionStandingTier.Friendly;
            return FactionStandingTier.PrivateerAlly;
        }

        public bool IsHostileToPlayer(FactionType faction)
        {
            FactionStandingTier tier = GetStandingTier(faction);
            return tier == FactionStandingTier.Hostile || tier == FactionStandingTier.Hunted;
        }

        public void ModifyReputation(FactionType faction, int delta)
        {
            int index = (int)faction;
            int oldRep = _reputations[index];
            _reputations[index] = Mathf.Clamp(_reputations[index] + delta, -100, 100);

            if (_reputations[index] != oldRep)
            {
                FactionStandingTier tier = GetStandingTier(faction);
                OnReputationChanged?.Invoke(faction, _reputations[index], tier);
                Debug.Log($"<color=cyan>[Faction Standing]</color> {faction}: {_reputations[index]} ({tier}) [{(delta >= 0 ? "+" : "")}{delta}]");

                if (tier == FactionStandingTier.Hunted && oldRep > -60)
                {
                    Debug.LogWarning($"<color=red><b>[HUNTED!]</b></color> You are now HUNTED by {faction}! Hunter squadrons mobilized!");
                    OnHunterSquadronDispatched?.Invoke(faction);
                }
            }
        }

        public void AddBounty(int amount)
        {
            if (amount <= 0) return;
            int oldBounty = _activeBounty;
            _activeBounty += amount;
            OnBountyChanged?.Invoke(_activeBounty);
            Debug.Log($"<color=yellow>[Bounty Raised]</color> Current Bounty: {_activeBounty} Gold (+{amount}g)");

            if (oldBounty < _hunterDispatchBountyThreshold && _activeBounty >= _hunterDispatchBountyThreshold)
            {
                Debug.LogWarning("<color=red>[WANTED DEAD OR ALIVE]</color> Naval hunters deployed to collect your head!");
                OnHunterSquadronDispatched?.Invoke(FactionType.ImperialNavy);
            }
        }

        public bool PayBounty(int goldPaid)
        {
            if (goldPaid <= 0 || _activeBounty <= 0) return false;
            int deduction = Mathf.Min(goldPaid, _activeBounty);
            _activeBounty -= deduction;
            OnBountyChanged?.Invoke(_activeBounty);
            Debug.Log($"<color=green>[Bounty Cleared]</color> Paid {deduction}g. Remaining Bounty: {_activeBounty}g");
            return true;
        }

        /// <summary>
        /// Applies relational matrix ripples when the player attacks a vessel of a given faction.
        /// </summary>
        public void RecordShipAttacked(FactionType victimFaction)
        {
            switch (victimFaction)
            {
                case FactionType.MerchantGuild:
                    ModifyReputation(FactionType.MerchantGuild, -15);
                    ModifyReputation(FactionType.ImperialNavy, -10);
                    ModifyReputation(FactionType.CorsairBrethren, +8);
                    AddBounty(75);
                    break;

                case FactionType.ImperialNavy:
                    ModifyReputation(FactionType.ImperialNavy, -25);
                    ModifyReputation(FactionType.CorsairBrethren, +15);
                    ModifyReputation(FactionType.MerchantGuild, -5);
                    AddBounty(150);
                    break;

                case FactionType.CorsairBrethren:
                    ModifyReputation(FactionType.CorsairBrethren, -20);
                    ModifyReputation(FactionType.ImperialNavy, +12);
                    ModifyReputation(FactionType.MerchantGuild, +8);
                    // Fighting pirates slightly reduces navy hostility
                    break;

                case FactionType.FreeportTraders:
                    ModifyReputation(FactionType.FreeportTraders, -20);
                    ModifyReputation(FactionType.MerchantGuild, -10);
                    AddBounty(50);
                    break;
            }
        }

        /// <summary>
        /// Records when a ship is plundered or sunk, heavily penalizing standing or yielding infamy.
        /// </summary>
        public void RecordShipPlunderedOrSunk(FactionType victimFaction, bool wasSunk)
        {
            int penalty = wasSunk ? -20 : -10;
            int bountyReward = wasSunk ? 120 : 60;

            ModifyReputation(victimFaction, penalty);
            if (victimFaction != FactionType.CorsairBrethren)
            {
                AddBounty(bountyReward);
            }
        }
    }
}
