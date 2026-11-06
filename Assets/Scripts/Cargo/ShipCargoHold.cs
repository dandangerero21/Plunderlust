using System;
using System.Collections.Generic;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Cargo
{
    /// <summary>
    /// Zero-GC ship cargo inventory and treasury manager.
    /// Manages physical cargo hold volume (Timber, Canvas, Cannonballs, Trade Goods, Treasure Maps)
    /// and liquid gold currency.
    /// Provides thread-safe, allocation-free resource querying and consumption for Phase 3 Officers
    /// (Carpenter consuming Timber, Sailmaster consuming Canvas).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ShipCargoHold : MonoBehaviour
    {
        [Header("Hull Data Profile")]
        [SerializeField] private ShipHullDataSO _hullData;

        [Header("Capacity Limits")]
        [SerializeField] private int _maxCargoCapacity = 20;

        [Header("Treasury")]
        [SerializeField] private int _currentGold = 100;

        // Internal inventory slot table indexed by enum for O(1) zero-allocation lookup
        private readonly int[] _inventoryQuantities = new int[16];

        // Events for UI telemetry, audio, and gameplay triggers
        public event Action<CargoItemType, int, int> OnCargoChanged; // (itemType, delta, newTotal)
        public event Action<int, int> OnGoldChanged;                 // (delta, newTotal)
        public event Action OnCargoHoldFull;

        public int CurrentGold => _currentGold;
        public int MaxCargoCapacity => _maxCargoCapacity;
        public int TotalCargoUnits => CalculateTotalPhysicalUnits();
        public bool IsHoldFull => TotalCargoUnits >= _maxCargoCapacity;
        public int RemainingSpace => Mathf.Max(0, _maxCargoCapacity - TotalCargoUnits);

        private void Awake()
        {
            ApplyHullData();
        }

        public void SetHullData(ShipHullDataSO hullData)
        {
            _hullData = hullData;
            ApplyHullData();
        }

        private void ApplyHullData()
        {
            if (_hullData == null) return;
            _maxCargoCapacity = Mathf.Max(5, _hullData.CargoCapacity);
        }

        /// <summary>
        /// Adds commodities or gold into the ship's hold.
        /// Gold bypasses physical volume limits. Physical goods are capped by remaining hold space.
        /// Zero heap allocations.
        /// </summary>
        public bool AddCargo(CargoItemType type, int count, out int addedAmount)
        {
            addedAmount = 0;
            if (count <= 0) return false;

            // 1. Currency (Gold) does not consume physical cargo hold space
            if (type == CargoItemType.Gold)
            {
                _currentGold += count;
                addedAmount = count;
                OnGoldChanged?.Invoke(count, _currentGold);
                OnCargoChanged?.Invoke(type, count, _currentGold);
                return true;
            }

            // 2. Physical Goods (Timber, Canvas, Rum, Sugar, etc.)
            int spaceLeft = RemainingSpace;
            if (spaceLeft <= 0)
            {
                OnCargoHoldFull?.Invoke();
                return false;
            }

            addedAmount = Mathf.Min(count, spaceLeft);
            int itemIndex = (int)type;

            if (itemIndex >= 0 && itemIndex < _inventoryQuantities.Length)
            {
                _inventoryQuantities[itemIndex] += addedAmount;
                OnCargoChanged?.Invoke(type, addedAmount, _inventoryQuantities[itemIndex]);

                if (IsHoldFull)
                {
                    OnCargoHoldFull?.Invoke();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Consumes specified commodity units from the cargo hold (e.g. Carpenter using Timber for repairs).
        /// Returns true if sufficient quantity was present and consumed.
        /// </summary>
        public bool ConsumeCargo(CargoItemType type, int count)
        {
            if (count <= 0) return true;

            // Gold spending
            if (type == CargoItemType.Gold)
            {
                if (_currentGold < count) return false;
                _currentGold -= count;
                OnGoldChanged?.Invoke(-count, _currentGold);
                OnCargoChanged?.Invoke(type, -count, _currentGold);
                return true;
            }

            int itemIndex = (int)type;
            if (itemIndex >= 0 && itemIndex < _inventoryQuantities.Length)
            {
                if (_inventoryQuantities[itemIndex] < count) return false;

                _inventoryQuantities[itemIndex] -= count;
                OnCargoChanged?.Invoke(type, -count, _inventoryQuantities[itemIndex]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if hold possesses at least the requested quantity of commodity or gold.
        /// </summary>
        public bool HasCargo(CargoItemType type, int count)
        {
            if (count <= 0) return true;
            if (type == CargoItemType.Gold) return _currentGold >= count;

            int itemIndex = (int)type;
            if (itemIndex >= 0 && itemIndex < _inventoryQuantities.Length)
            {
                return _inventoryQuantities[itemIndex] >= count;
            }
            return false;
        }

        /// <summary>
        /// Returns current stored quantity for a specific cargo item type.
        /// </summary>
        public int GetItemCount(CargoItemType type)
        {
            if (type == CargoItemType.Gold) return _currentGold;

            int itemIndex = (int)type;
            if (itemIndex >= 0 && itemIndex < _inventoryQuantities.Length)
            {
                return _inventoryQuantities[itemIndex];
            }
            return 0;
        }

        /// <summary>
        /// Calculates total physical cargo units occupying hold volume.
        /// Excludes gold.
        /// </summary>
        private int CalculateTotalPhysicalUnits()
        {
            int total = 0;
            // Iterate all physical items (skipping Gold at index 0)
            for (int i = 1; i < _inventoryQuantities.Length; i++)
            {
                total += _inventoryQuantities[i];
            }
            return total;
        }

        /// <summary>
        /// Exports active inventory into serializable DTO array for save games.
        /// </summary>
        public CargoSlotDTO[] ExportCargoDTO()
        {
            List<CargoSlotDTO> slots = new List<CargoSlotDTO>();
            for (int i = 1; i < _inventoryQuantities.Length; i++)
            {
                if (_inventoryQuantities[i] > 0)
                {
                    slots.Add(new CargoSlotDTO((CargoItemType)i, _inventoryQuantities[i]));
                }
            }
            return slots.ToArray();
        }

        /// <summary>
        /// Restores cargo inventory from save DTO.
        /// </summary>
        public void ImportCargoDTO(CargoSlotDTO[] slots, int savedGold)
        {
            Array.Clear(_inventoryQuantities, 0, _inventoryQuantities.Length);
            _currentGold = savedGold;

            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    int idx = (int)slot.itemType;
                    if (idx >= 0 && idx < _inventoryQuantities.Length)
                    {
                        _inventoryQuantities[idx] = slot.quantity;
                    }
                }
            }
        }
    }
}
