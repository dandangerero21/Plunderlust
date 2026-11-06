using System;
using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Port
{
    [Serializable]
    public struct CommodityPrice
    {
        public CargoItemType itemType;
        public int buyPrice;   // Cost for player to purchase from port
        public int sellPrice;  // Gold paid to player when selling to port
        public int stockCount; // Current available port inventory
        public bool isHighDemand; // Displays green premium tag
    }

    /// <summary>
    /// Phase 5 Regional Commodity Market & Trading Engine.
    /// Drives dynamic regional supply and demand between naval strongholds and smuggler coves:
    /// - Naval Ports: cheap shipwright materials (Timber, Canvas), high demand for luxuries (Sugar, Spices).
    /// - Smuggler Coves: massive gold payouts for contraband (Rum, Sugar, Spices, Treasure Maps).
    /// </summary>
    public class PortCommodityMarket : MonoBehaviour
    {
        [Header("Market Specialization")]
        [SerializeField] private FactionType _marketFaction = FactionType.MerchantGuild;
        [SerializeField] private bool _isContrabandDen = false;

        private readonly List<CommodityPrice> _commodities = new List<CommodityPrice>();

        public event Action<CargoItemType, int, int> OnCommoditySold;   // (type, amount, totalEarnings)
        public event Action<CargoItemType, int, int> OnCommodityBought; // (type, amount, totalCost)

        public IReadOnlyList<CommodityPrice> Commodities => _commodities;
        public FactionType MarketFaction => _marketFaction;
        public bool IsContrabandDen => _isContrabandDen;

        private void Awake()
        {
            InitializeMarketCatalog();
        }

        public void ConfigureMarket(FactionType faction, bool isContrabandDen)
        {
            _marketFaction = faction;
            _isContrabandDen = isContrabandDen;
            InitializeMarketCatalog();
        }

        public void InitializeMarketCatalog()
        {
            _commodities.Clear();

            if (_isContrabandDen)
            {
                // Smuggler Cove / Pirate Den Economics (Buys contraband at premium, sells cheap black market munitions)
                AddCommodity(CargoItemType.Timber, 24, 12, 10, false);
                AddCommodity(CargoItemType.Canvas, 14, 8, 15, false);
                AddCommodity(CargoItemType.Cannonballs, 6, 3, 40, false);
                AddCommodity(CargoItemType.Sugar, 52, 44, 5, true);        // High demand contraband
                AddCommodity(CargoItemType.Rum, 60, 52, 6, true);          // Premium price for rum!
                AddCommodity(CargoItemType.Spices, 68, 58, 4, true);       // High demand luxury
                AddCommodity(CargoItemType.Silk, 75, 62, 5, true);         // Black market luxury
                AddCommodity(CargoItemType.TreasureMap, 260, 190, 2, true); // Black market treasure map
            }
            else if (_marketFaction == FactionType.ImperialNavy)
            {
                // Imperial Naval Base (Abundant munitions/shipwright goods, low demand for pirate rum)
                AddCommodity(CargoItemType.Timber, 10, 6, 45, false);
                AddCommodity(CargoItemType.Canvas, 16, 9, 30, false);
                AddCommodity(CargoItemType.Cannonballs, 7, 4, 60, false);
                AddCommodity(CargoItemType.Sugar, 36, 26, 12, false);
                AddCommodity(CargoItemType.Rum, 22, 14, 8, false);
                AddCommodity(CargoItemType.Spices, 45, 34, 10, true);
                AddCommodity(CargoItemType.Silk, 58, 45, 8, false);
                AddCommodity(CargoItemType.TreasureMap, 300, 150, 1, false);
            }
            else // Merchant Guild / Freeport Haven (Balanced mercantile trade hub)
            {
                AddCommodity(CargoItemType.Timber, 12, 7, 35, false);
                AddCommodity(CargoItemType.Canvas, 18, 11, 25, false);
                AddCommodity(CargoItemType.Cannonballs, 8, 5, 50, false);
                AddCommodity(CargoItemType.Sugar, 40, 30, 20, true);
                AddCommodity(CargoItemType.Rum, 38, 28, 18, false);
                AddCommodity(CargoItemType.Spices, 50, 38, 15, true);
                AddCommodity(CargoItemType.Silk, 50, 35, 20, false);
                AddCommodity(CargoItemType.TreasureMap, 280, 175, 1, false);
            }
        }

        private void AddCommodity(CargoItemType type, int buyPrice, int sellPrice, int stock, bool isHighDemand)
        {
            _commodities.Add(new CommodityPrice
            {
                itemType = type,
                buyPrice = buyPrice,
                sellPrice = sellPrice,
                stockCount = stock,
                isHighDemand = isHighDemand
            });
        }

        public bool BuyCommodity(ShipCargoHold playerHold, CargoItemType type, int amount, out string failReason)
        {
            failReason = string.Empty;
            if (playerHold == null || amount <= 0)
            {
                failReason = "Invalid transaction";
                return false;
            }

            int index = _commodities.FindIndex(c => c.itemType == type);
            if (index < 0)
            {
                failReason = "Commodity not sold here";
                return false;
            }

            CommodityPrice price = _commodities[index];
            if (price.stockCount < amount)
            {
                failReason = $"Only {price.stockCount} in stock";
                return false;
            }

            int totalCost = price.buyPrice * amount;
            if (playerHold.CurrentGold < totalCost)
            {
                failReason = $"Not enough Gold (Requires {totalCost}g)";
                return false;
            }

            if (playerHold.TotalCargoUnits + amount > playerHold.MaxCargoCapacity)
            {
                failReason = "Cargo hold is full!";
                return false;
            }

            // Deduct gold & port stock, add cargo
            playerHold.ConsumeCargo(CargoItemType.Gold, totalCost);
            playerHold.AddCargo(type, amount, out _);

            price.stockCount -= amount;
            _commodities[index] = price;

            OnCommodityBought?.Invoke(type, amount, totalCost);
            Debug.Log($"<color=green>[Market]</color> Purchased {amount}x {type} for {totalCost} Gold.");
            return true;
        }

        public bool SellCommodity(ShipCargoHold playerHold, CargoItemType type, int amount, out string failReason)
        {
            failReason = string.Empty;
            if (playerHold == null || amount <= 0)
            {
                failReason = "Invalid transaction";
                return false;
            }

            int availablePlayerCargo = playerHold.GetItemCount(type);
            if (availablePlayerCargo < amount)
            {
                failReason = $"You only have {availablePlayerCargo}x {type}";
                return false;
            }

            int index = _commodities.FindIndex(c => c.itemType == type);
            if (index < 0)
            {
                failReason = "Port does not accept this cargo";
                return false;
            }

            CommodityPrice price = _commodities[index];
            int totalEarnings = price.sellPrice * amount;

            // Remove cargo from player, award gold, increment port stock
            playerHold.ConsumeCargo(type, amount);
            playerHold.AddCargo(CargoItemType.Gold, totalEarnings, out _);

            price.stockCount += amount;
            _commodities[index] = price;

            OnCommoditySold?.Invoke(type, amount, totalEarnings);
            Debug.Log($"<color=gold>[Market]</color> Sold {amount}x {type} for {totalEarnings} Gold.");
            return true;
        }

        public void UpdateCommodityPrice(CargoItemType type, int buyPrice, int sellPrice, int defaultStock, bool isHighDemand)
        {
            int index = _commodities.FindIndex(c => c.itemType == type);
            if (index >= 0)
            {
                var item = _commodities[index];
                item.buyPrice = buyPrice;
                item.sellPrice = sellPrice;
                item.isHighDemand = isHighDemand;
                _commodities[index] = item;
            }
            else
            {
                AddCommodity(type, buyPrice, sellPrice, defaultStock, isHighDemand);
            }
        }
    }
}
