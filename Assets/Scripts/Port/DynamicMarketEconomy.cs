using System;
using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.World;
using UnityEngine;

namespace Plunderlust.Port
{
    [Serializable]
    public class CommodityMarketState
    {
        public CargoItemType itemType;
        public int basePrice;
        public float baselineDemandMultiplier; // 0.6x (Surplus), 1.0x (Standard), 1.5x (Demanded)
        public float currentDemandMultiplier;  // Dynamic multiplier influenced by sales saturation
        public int initialStock;
    }

    /// <summary>
    /// Master Regional Dynamic Supply & Demand Market Economy.
    /// Simulates authentic 17th-century trade route economics across regional archetypes:
    /// - Fort St. George: Surplus Iron Shot & Timber (0.6x); Demands Rum & Spices (1.5x).
    /// - Port Marigold: Surplus Silk, Sugar & Canvas (0.6x); Demands Iron Shot & Rare Contraband (1.5x).
    /// - Isla de la Libre: Surplus Rum & Pillaged Contraband (0.6x); Demands Timber, Canvas & Sugar (1.5x).
    /// 
    /// Market Saturation Math:
    /// Selling N units reduces that port's local demand multiplier by 2% per unit (clamped to 0.4x),
    /// which smoothly recovers toward baseline over 10 game-minutes (600s).
    /// </summary>
    [RequireComponent(typeof(PortCommodityMarket))]
    public class DynamicMarketEconomy : MonoBehaviour
    {
        [Header("Port Identity & Archetype")]
        [SerializeField] private PortArchetype _portArchetype = PortArchetype.MerchantGuildHub;

        [Header("Economy Parameters")]
        [Tooltip("Seconds required for a saturated commodity market to fully recover to baseline (10 game-minutes = 600s).")]
        [SerializeField] private float _marketRecoveryDuration = 600f;
        [Tooltip("Saturation penalty: 2% reduction per unit sold.")]
        [SerializeField] private float _saturationPenaltyPerUnit = 0.02f;
        [Tooltip("Absolute minimum demand multiplier clamp.")]
        [SerializeField] private float _minDemandMultiplier = 0.40f;

        private PortCommodityMarket _commodityMarket;
        private readonly List<CommodityMarketState> _commodityStates = new List<CommodityMarketState>();

        public IReadOnlyList<CommodityMarketState> CommodityStates => _commodityStates;
        public PortArchetype Archetype => _portArchetype;

        private void Awake()
        {
            _commodityMarket = GetComponent<PortCommodityMarket>();
            if (_commodityStates.Count == 0)
            {
                InitializeEconomy(_portArchetype, _commodityMarket);
            }
        }

        private void Start()
        {
            if (_commodityMarket != null)
            {
                _commodityMarket.OnCommoditySold += HandleCommoditySold;
            }
        }

        private void OnDestroy()
        {
            if (_commodityMarket != null)
            {
                _commodityMarket.OnCommoditySold -= HandleCommoditySold;
            }
        }

        public void InitializeEconomy(PortArchetype archetype, PortCommodityMarket market)
        {
            _portArchetype = archetype;
            _commodityMarket = market != null ? market : GetComponent<PortCommodityMarket>();
            _commodityStates.Clear();

            // Register standard 17th-century trade goods
            // (Spices, Rum, Silk, Sugar, Iron Shot/Cannonballs, Timber, Canvas, TreasureMap)
            RegisterCommodity(CargoItemType.Spices, basePrice: 55, GetMultiplier(CargoItemType.Spices, archetype), initialStock: 15);
            RegisterCommodity(CargoItemType.Rum, basePrice: 40, GetMultiplier(CargoItemType.Rum, archetype), initialStock: 20);
            RegisterCommodity(CargoItemType.Silk, basePrice: 65, GetMultiplier(CargoItemType.Silk, archetype), initialStock: 12);
            RegisterCommodity(CargoItemType.Sugar, basePrice: 32, GetMultiplier(CargoItemType.Sugar, archetype), initialStock: 25);
            RegisterCommodity(CargoItemType.Cannonballs, basePrice: 10, GetMultiplier(CargoItemType.Cannonballs, archetype), initialStock: 50);
            RegisterCommodity(CargoItemType.Timber, basePrice: 15, GetMultiplier(CargoItemType.Timber, archetype), initialStock: 35);
            RegisterCommodity(CargoItemType.Canvas, basePrice: 20, GetMultiplier(CargoItemType.Canvas, archetype), initialStock: 30);
            RegisterCommodity(CargoItemType.TreasureMap, basePrice: 250, GetMultiplier(CargoItemType.TreasureMap, archetype), initialStock: 2);

            PushPricesToPortMarket();
        }

        private float GetMultiplier(CargoItemType item, PortArchetype archetype)
        {
            switch (archetype)
            {
                case PortArchetype.ImperialNavyBastion: // Fort St. George
                    // Surplus: Iron Shot & Timber (0.6x)
                    if (item == CargoItemType.Cannonballs || item == CargoItemType.Timber) return 0.60f;
                    // Demands: Rum & Spices (1.5x)
                    if (item == CargoItemType.Rum || item == CargoItemType.Spices) return 1.50f;
                    return 1.0f;

                case PortArchetype.MerchantGuildHub: // Port Marigold / Port Royal
                    // Surplus: Silk, Sugar & Canvas (0.6x)
                    if (item == CargoItemType.Silk || item == CargoItemType.Sugar || item == CargoItemType.Canvas) return 0.60f;
                    // Demands: Iron Shot & Rare Contraband (Rum, Spices, TreasureMap) (1.5x)
                    if (item == CargoItemType.Cannonballs || item == CargoItemType.Rum || item == CargoItemType.Spices || item == CargoItemType.TreasureMap) return 1.50f;
                    return 1.0f;

                case PortArchetype.CorsairHavenFreeport: // Isla de la Libre / Tortuga
                    // Surplus: Rum & Pillaged Contraband (0.6x)
                    if (item == CargoItemType.Rum || item == CargoItemType.TreasureMap) return 0.60f;
                    // Demands: Timber, Canvas & Sugar (1.5x)
                    if (item == CargoItemType.Timber || item == CargoItemType.Canvas || item == CargoItemType.Sugar) return 1.50f;
                    return 1.0f;

                default:
                    return 1.0f;
            }
        }

        private void RegisterCommodity(CargoItemType type, int basePrice, float multiplier, int initialStock)
        {
            _commodityStates.Add(new CommodityMarketState
            {
                itemType = type,
                basePrice = basePrice,
                baselineDemandMultiplier = multiplier,
                currentDemandMultiplier = multiplier,
                initialStock = initialStock
            });
        }

        private void Update()
        {
            // Smoothly recover saturated commodity multipliers toward baseline
            float delta = Time.deltaTime;
            bool pricesChanged = false;

            for (int i = 0; i < _commodityStates.Count; i++)
            {
                CommodityMarketState state = _commodityStates[i];
                if (!Mathf.Approximately(state.currentDemandMultiplier, state.baselineDemandMultiplier))
                {
                    // Rate: 1.0 / duration per second
                    float recoverySpeed = 1.0f / Mathf.Max(60f, _marketRecoveryDuration);
                    state.currentDemandMultiplier = Mathf.MoveTowards(
                        state.currentDemandMultiplier,
                        state.baselineDemandMultiplier,
                        recoverySpeed * delta
                    );
                    pricesChanged = true;
                }
            }

            if (pricesChanged)
            {
                PushPricesToPortMarket();
            }
        }

        /// <summary>
        /// Applies the market saturation penalty when the player sells N units of a good.
        /// Reduces demand multiplier by 2% per unit (clamped to 0.4x).
        /// </summary>
        public void HandleCommoditySold(CargoItemType type, int amount, int totalEarnings)
        {
            int index = _commodityStates.FindIndex(c => c.itemType == type);
            if (index < 0) return;

            CommodityMarketState state = _commodityStates[index];
            float penalty = state.currentDemandMultiplier * (_saturationPenaltyPerUnit * amount);
            state.currentDemandMultiplier = Mathf.Max(_minDemandMultiplier, state.currentDemandMultiplier - penalty);

            Debug.Log($"<color=orange>[MarketEconomy]</color> Market saturated for {type}. Demand Multiplier: {state.currentDemandMultiplier:F2}x (Baseline: {state.baselineDemandMultiplier:F2}x)");
            PushPricesToPortMarket();
        }

        /// <summary>
        /// Recalculates buy/sell prices and synchronizes them with the PortCommodityMarket catalog.
        /// </summary>
        public void PushPricesToPortMarket()
        {
            if (_commodityMarket == null) return;

            for (int i = 0; i < _commodityStates.Count; i++)
            {
                CommodityMarketState state = _commodityStates[i];
                int buyPrice = Mathf.Max(2, Mathf.RoundToInt(state.basePrice * state.currentDemandMultiplier * 1.25f));
                int sellPrice = Mathf.Max(1, Mathf.RoundToInt(state.basePrice * state.currentDemandMultiplier * 0.85f));
                bool isHighDemand = state.currentDemandMultiplier >= 1.25f;

                _commodityMarket.UpdateCommodityPrice(state.itemType, buyPrice, sellPrice, state.initialStock, isHighDemand);
            }
        }
    }
}
