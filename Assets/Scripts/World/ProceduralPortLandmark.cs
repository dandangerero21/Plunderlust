using Plunderlust.Core.Data;
using Plunderlust.Port;
using UnityEngine;

namespace Plunderlust.World
{
    /// <summary>
    /// Phase 5 Procedural Port Landmark.
    /// Manages dynamically spawned ports across the infinite world archipelago.
    /// Configures faction identity, solid mesh colliders, tavern recruitment, and commodity markets.
    /// </summary>
    public class ProceduralPortLandmark : MonoBehaviour
    {
        [SerializeField] private FactionType _rulingFaction = FactionType.MerchantGuild;
        [SerializeField] private string _portName = "Port Fortune";
        [SerializeField] private bool _isContrabandDen = false;

        public FactionType RulingFaction => _rulingFaction;
        public string PortName => _portName;

        public void InitializePort(string name, FactionType faction, bool isContrabandDen)
        {
            _portName = name;
            _rulingFaction = faction;
            _isContrabandDen = isContrabandDen;

            // Ensure Port Haven or Haven-like functionality
            PortIslandHaven haven = GetComponent<PortIslandHaven>();
            if (haven == null) haven = gameObject.AddComponent<PortIslandHaven>();

            // Ensure Commodity Market configured for this faction
            PortCommodityMarket market = GetComponent<PortCommodityMarket>();
            if (market == null) market = gameObject.AddComponent<PortCommodityMarket>();
            market.ConfigureMarket(faction, isContrabandDen);

            // Add solid mesh colliders to all spawned pieces
            MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                }
            }
        }
    }
}
