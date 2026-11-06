using System;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Crew
{
    /// <summary>
    /// ScriptableObject defining an officer specialist profile for hire in port taverns and smuggler coves.
    /// Drives passive subsystem repairs, bilge pumping, broadside cooldown reductions, and flotsam salvage bonuses.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCrewSpecialist", menuName = "Plunderlust/Crew/Specialist Profile", order = 30)]
    public class CrewSpecialistSO : ScriptableObject
    {
        [Header("Officer Identity")]
        [SerializeField] private string _officerName = "Barnaby Cross";
        [SerializeField] private OfficerSpecialtyType _specialty = OfficerSpecialtyType.Carpenter;
        [SerializeField] private SpecialistTier _tier = SpecialistTier.Common;
        [TextArea(2, 4)]
        [SerializeField] private string _loreDescription = "Seasoned shipwright capable of plugging hull breaches at sea.";

        [Header("Hiring Costs")]
        [Tooltip("Standard Gold hiring fee in formal port taverns.")]
        [SerializeField] private int _baseGoldCost = 150;
        [Tooltip("Discounted Gold fee in smuggler coves.")]
        [SerializeField] private int _smugglerGoldCost = 75;
        [Tooltip("Barter material required in smuggler coves alongside gold.")]
        [SerializeField] private CargoItemType _barterMaterialType = CargoItemType.Timber;
        [SerializeField] private int _barterMaterialCount = 4;

        [Header("Passive Specialty Modifiers")]
        [Tooltip("Primary passive value (e.g. Carpenter: HP/sec repaired; Bosun: bilge pumped/sec; Gunner: reload % reduction).")]
        [SerializeField] private float _primaryPassiveValue = 4.0f;
        [Tooltip("Secondary passive value (e.g. Bosun: % casualty reduction; Sailmaster: close-hauled wind boost).")]
        [SerializeField] private float _secondaryPassiveValue = 0.20f;

        public string OfficerName => _officerName;
        public OfficerSpecialtyType Specialty => _specialty;
        public SpecialistTier Tier => _tier;
        public string LoreDescription => _loreDescription;
        public int BaseGoldCost => _baseGoldCost;
        public int SmugglerGoldCost => _smugglerGoldCost;
        public CargoItemType BarterMaterialType => _barterMaterialType;
        public int BarterMaterialCount => _barterMaterialCount;
        public float PrimaryPassiveValue => _primaryPassiveValue;
        public float SecondaryPassiveValue => _secondaryPassiveValue;

        public void InitializeRuntime(string name, OfficerSpecialtyType spec, SpecialistTier tier, int goldCost, int smugglerGold, CargoItemType barterType, int barterCount, float primary, float secondary, string lore)
        {
            _officerName = name;
            _specialty = spec;
            _tier = tier;
            _baseGoldCost = goldCost;
            _smugglerGoldCost = smugglerGold;
            _barterMaterialType = barterType;
            _barterMaterialCount = barterCount;
            _primaryPassiveValue = primary;
            _secondaryPassiveValue = secondary;
            _loreDescription = lore;
        }

        public string GetEffectSummary()
        {
            switch (_specialty)
            {
                case OfficerSpecialtyType.Carpenter:
                    return $"Consumes Timber to repair Hull/Rudder (+{_primaryPassiveValue:F1} HP/s)";
                case OfficerSpecialtyType.Boatswain:
                    return $"Pumps Bilge (-{_primaryPassiveValue:F0}/s) & protects crew (-{_secondaryPassiveValue * 100:F0}% casualties)";
                case OfficerSpecialtyType.MasterGunner:
                    return $"Speeds reload by -{_primaryPassiveValue * 100:F0}% & tightens spread";
                case OfficerSpecialtyType.Sailmaster:
                    return $"Consumes Canvas to mend Sails (+{_primaryPassiveValue:F1} HP/s) & boosts wind";
                case OfficerSpecialtyType.Quartermaster:
                    return $"Boosts flotsam loot by +{(_primaryPassiveValue - 1.0f) * 100:F0}% & port discounts";
                default:
                    return "Naval Specialist";
            }
        }
    }
}
