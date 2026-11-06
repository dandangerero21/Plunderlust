using System;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Ship
{
    /// <summary>
    /// Governs authentic 17th-century naval ship classifications, random christened ship names,
    /// and player ship naming persistence across voyages and shipyard upgrades.
    /// </summary>
    public class ShipIdentitySystem : MonoBehaviour
    {
        public static ShipIdentitySystem PlayerIdentity { get; private set; }

        [Header("Identity State")]
        [SerializeField] private ShipTier _currentTier = ShipTier.Tier1_LeakyRowboat;
        [SerializeField] private string _christenedName = "The Barnacle";
        [SerializeField] private bool _isPlayer = false;

        public event Action<string, string> OnShipRenamed; // (christenedName, className)

        // Authentic Class Names by Tier
        public const string ClassTier1 = "Harbor Skiff";
        public const string ClassTier2 = "Jamaican Sloop";
        public const string ClassTier3 = "Corsair Brigantine";
        public const string ClassTier4 = "Royal Frigate / Galleon";
        public const string ClassTier5 = "Cursed Ghost Galleon";

        // Curated Authentic Christened Names
        private static readonly string[] NamesTier1 =
        {
            "The Barnacle", "Dead Man's Drift", "Bilge Rat", "Salt Sprite",
            "Mud Lark", "Drunken Seagull", "Gallows Plank", "Tide Skimmer",
            "Rusty Anchor", "Barnacle Biter", "Wharf Rat", "Scuttlebutt"
        };

        private static readonly string[] NamesTier2 =
        {
            "Sea Viper", "Wicked Wench", "Cutlass", "Riptide",
            "Siren's Call", "Fortune's Favor", "Black Dog", "Sea Dart",
            "Wind Dancer", "Corsair's Fang", "Swift Current", "Gilded Gull"
        };

        private static readonly string[] NamesTier3 =
        {
            "Revenge's Shadow", "Crimson Wake", "Storm Crow", "Kraken's Maw",
            "Bloodhound", "Sea Wolf", "Marauder", "Ghost Corsair",
            "Black Tide", "Iron Maiden", "Bounty Hunter", "Dread Falcon"
        };

        private static readonly string[] NamesTier4 =
        {
            "Sovereign of the Seas", "Leviathan", "King's Wrath", "Royal Fortune",
            "Neptune's Trident", "Invincible", "Dreadnought", "Grand Monarch",
            "Vanguard of Glory", "Imperial Pride", "HMS Defiance"
        };

        private static readonly string[] NamesTier5 =
        {
            "Flying Dutchman", "Soul Collector", "Phantom Wanderer",
            "Specter of the Deep", "Banshee's Wail", "Cursed Revenant",
            "Doom's Herald", "The Netherwake", "Pale Harvester"
        };

        public ShipTier CurrentTier => _currentTier;
        public string ChristenedName => _christenedName;
        public string ShipClassName => GetAuthenticClassName(_currentTier);
        public string FullShipTitle => $"{_christenedName} ({ShipClassName})";

        private void Awake()
        {
            if (gameObject.CompareTag("Player") || GetComponent<PlayerShipInputBridge>() != null)
            {
                _isPlayer = true;
                PlayerIdentity = this;

                if (PlayerPrefs.HasKey("Plunderlust_NewVoyageName"))
                {
                    string savedName = PlayerPrefs.GetString("Plunderlust_NewVoyageName", "");
                    if (!string.IsNullOrWhiteSpace(savedName))
                    {
                        _christenedName = savedName;
                        PlayerPrefs.DeleteKey("Plunderlust_NewVoyageName");
                    }
                }
            }

            if (string.IsNullOrEmpty(_christenedName))
            {
                _christenedName = GenerateRandomShipName(_currentTier);
            }
        }

        private void OnDestroy()
        {
            if (_isPlayer && PlayerIdentity == this)
            {
                PlayerIdentity = null;
            }
        }

        public void SetTier(ShipTier tier)
        {
            _currentTier = tier;
            OnShipRenamed?.Invoke(_christenedName, ShipClassName);
        }

        public void ChristenShip(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                newName = GenerateRandomShipName(_currentTier);
            }

            _christenedName = newName.Trim();
            gameObject.name = $"Ship_{_christenedName.Replace(' ', '_')}";
            OnShipRenamed?.Invoke(_christenedName, ShipClassName);

            if (_isPlayer && Plunderlust.Core.Persistence.ShipSaveManager.Instance != null)
            {
                var save = Plunderlust.Core.Persistence.ShipSaveManager.Instance.ActiveSave;
                if (save != null)
                {
                    save.shipCustomName = _christenedName;
                }
            }
        }

        public void RerollName()
        {
            ChristenShip(GenerateRandomShipName(_currentTier));
        }

        public static string GetAuthenticClassName(ShipTier tier)
        {
            return tier switch
            {
                ShipTier.Tier1_LeakyRowboat => ClassTier1,
                ShipTier.Tier2_CoastalCutter => ClassTier2,
                ShipTier.Tier3_PirateBrigantine => ClassTier3,
                ShipTier.Tier4_RoyalGalleon => ClassTier4,
                ShipTier.Tier5_GhostGalleon => ClassTier5,
                _ => "Pirate Vessel"
            };
        }

        public static string GenerateRandomShipName(ShipTier tier)
        {
            string[] pool = tier switch
            {
                ShipTier.Tier1_LeakyRowboat => NamesTier1,
                ShipTier.Tier2_CoastalCutter => NamesTier2,
                ShipTier.Tier3_PirateBrigantine => NamesTier3,
                ShipTier.Tier4_RoyalGalleon => NamesTier4,
                ShipTier.Tier5_GhostGalleon => NamesTier5,
                _ => NamesTier1
            };

            int index = UnityEngine.Random.Range(0, pool.Length);
            return pool[index];
        }
    }
}
