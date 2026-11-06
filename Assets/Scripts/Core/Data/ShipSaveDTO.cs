using System;
using UnityEngine;

namespace Plunderlust.Core.Data
{
    /// <summary>
    /// Explicit Data Transfer Object for persisting ship state, subsystems, crew, and cargo.
    /// Free of Unity Engine lifecycle dependencies and suitable for JSON serialization.
    /// </summary>
    [Serializable]
    public class ShipSaveDTO
    {
        public string shipInstanceId = Guid.NewGuid().ToString();
        public string shipCustomName = "Jackdaw";
        public ShipTier tier = ShipTier.Tier1_LeakyRowboat;

        // Spatial coordinates
        public float posX;
        public float posY;
        public float posZ;
        public float rotY;

        // Subsystem current health values [0 .. Max]
        public float hullHealth;
        public float sailsHealth;
        public float rudderHealth;
        public float gunDeckHealth;
        public float currentBilgeWater;

        // Manpower and Officers
        public int currentDeckhands;
        public OfficerSaveDTO[] assignedOfficers = Array.Empty<OfficerSaveDTO>();

        // Economy & Cargo
        public int currentGold;
        public CargoSlotDTO[] cargoInventory = Array.Empty<CargoSlotDTO>();

        // Factions, Infamy & Discovery
        public int imperialNavyReputation;
        public int merchantGuildReputation;
        public int corsairBrethrenReputation;
        public int freeportTradersReputation;
        public int activeBounty;
        public int discoveredIslandsCount;

        public Vector3 GetWorldPosition()
        {
            return new Vector3(posX, posY, posZ);
        }

        public Quaternion GetWorldRotation()
        {
            return Quaternion.Euler(0f, rotY, 0f);
        }

        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            posX = position.x;
            posY = position.y;
            posZ = position.z;
            rotY = rotation.eulerAngles.y;
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static ShipSaveDTO FromJson(string json)
        {
            return JsonUtility.FromJson<ShipSaveDTO>(json);
        }
    }

    [Serializable]
    public class CargoSlotDTO
    {
        public CargoItemType itemType;
        public int quantity;

        public CargoSlotDTO() { }

        public CargoSlotDTO(CargoItemType type, int count)
        {
            itemType = type;
            quantity = count;
        }
    }

    [Serializable]
    public class OfficerSaveDTO
    {
        public string officerId;
        public string officerName;
        public OfficerSpecialtyType specialty;
        public SpecialistTier tier;
        public float passiveValue;

        public OfficerSaveDTO() { }

        public OfficerSaveDTO(string id, string name, OfficerSpecialtyType spec, SpecialistTier t, float val)
        {
            officerId = id;
            officerName = name;
            specialty = spec;
            tier = t;
            passiveValue = val;
        }
    }
}
