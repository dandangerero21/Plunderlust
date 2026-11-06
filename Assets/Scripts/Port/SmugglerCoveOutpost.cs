using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Crew;
using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.Port
{
    /// <summary>
    /// Offshore Smuggler Cove Outpost located in open waters.
    /// Operates with the Barter Twist:
    /// Specialists and deckhands are heavily discounted in Gold, but require
    /// valuable cargo materials you own (Timber, Canvas, Rum, Spices) to seal the deal.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class SmugglerCoveOutpost : MonoBehaviour
    {
        [Header("Outpost Identity")]
        [SerializeField] private string _coveName = "Dead Man's Reef Smuggler Cove";
        [SerializeField] private int _deckhandGoldCost = 12;
        [SerializeField] private CargoItemType _deckhandBarterType = CargoItemType.Rum;
        [SerializeField] private int _deckhandBarterCount = 1;

        [Header("Mooring Zone")]
        [SerializeField] private float _mooringRadius = 35.0f;
        [SerializeField] private Vector3 _covePromptOffset = new Vector3(0f, 3.0f, 0f);

        private SphereCollider _harborTrigger;
        private bool _isPlayerMoored = false;
        private Transform _playerTransform;
        private ShipLocomotionController _playerLocomotion;

        private readonly List<CrewSpecialistSO> _smugglerRoster = new List<CrewSpecialistSO>();

        public string CoveName => _coveName;
        public int DeckhandGoldCost => _deckhandGoldCost;
        public CargoItemType DeckhandBarterType => _deckhandBarterType;
        public int DeckhandBarterCount => _deckhandBarterCount;
        public IReadOnlyList<CrewSpecialistSO> SmugglerRoster => _smugglerRoster;

        private void Awake()
        {
            _harborTrigger = GetComponent<SphereCollider>();
            if (_harborTrigger == null)
            {
                _harborTrigger = gameObject.AddComponent<SphereCollider>();
            }
            _harborTrigger.isTrigger = true;
            _harborTrigger.radius = _mooringRadius;

            EnsurePhysicalColliders();
            EnsureCommodityMarket();
            GenerateSmugglerRoster();
        }

        private void EnsureCommodityMarket()
        {
            PortCommodityMarket market = GetComponent<PortCommodityMarket>();
            if (market == null) market = gameObject.AddComponent<PortCommodityMarket>();
            market.ConfigureMarket(FactionType.CorsairBrethren, true); // Black market contraband den
        }

        private void EnsurePhysicalColliders()
        {
            MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        private void GenerateSmugglerRoster()
        {
            _smugglerRoster.Clear();

            // 1. Sailmaster (Cheap gold, requires Canvas)
            CrewSpecialistSO sails = ScriptableObject.CreateInstance<CrewSpecialistSO>();
            sails.InitializeRuntime("Jonas 'Vane' Calder", OfficerSpecialtyType.Sailmaster, SpecialistTier.Veteran, 90, 70, CargoItemType.Canvas, 4, 7.0f, 0.22f, "Smuggler rigger who weaves sails with black market canvas.");
            _smugglerRoster.Add(sails);

            // 2. Outlaw Carpenter (Cheap gold, requires Timber)
            CrewSpecialistSO carp = ScriptableObject.CreateInstance<CrewSpecialistSO>();
            carp.InitializeRuntime("Caleb 'Splinter' Vance", OfficerSpecialtyType.Carpenter, SpecialistTier.Common, 75, 60, CargoItemType.Timber, 4, 4.5f, 0.15f, "Repairs hulks in secret coves using salvaged ship timber.");
            _smugglerRoster.Add(carp);

            // 3. Quartermaster (Requires Rum and modest Gold)
            CrewSpecialistSO qm = ScriptableObject.CreateInstance<CrewSpecialistSO>();
            qm.InitializeRuntime("Madame Coraline Vane", OfficerSpecialtyType.Quartermaster, SpecialistTier.Veteran, 110, 85, CargoItemType.Rum, 2, 1.35f, 0.20f, "Black market fences who extracts 35% higher loot from pillaged flotsam.");
            _smugglerRoster.Add(qm);
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.transform.root;
            if (other.CompareTag("Player") || root.CompareTag("Player"))
            {
                _isPlayerMoored = true;
                _playerTransform = root;
                _playerLocomotion = root.GetComponent<ShipLocomotionController>();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Transform root = other.transform.root;
            if (other.CompareTag("Player") || root.CompareTag("Player"))
            {
                if (_playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) > (_mooringRadius * 1.25f))
                {
                    _isPlayerMoored = false;
                    _playerTransform = null;
                    _playerLocomotion = null;

                    if (PortTavernView.Instance != null && PortTavernView.Instance.IsOpen)
                    {
                        PortTavernView.Instance.CloseView();
                    }
                }
            }
        }

        private void Update()
        {
            // Fallback player distance verification so mooring is rock solid
            if (_playerTransform == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                {
                    _playerTransform = playerGo.transform;
                    _playerLocomotion = playerGo.GetComponent<ShipLocomotionController>();
                }
            }

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                _isPlayerMoored = dist <= (_mooringRadius * 1.25f);
            }

            if (!_isPlayerMoored)
            {
                if (PortTavernView.Instance != null && PortTavernView.Instance.IsOpen && PortTavernView.Instance.ActiveCove == this)
                {
                    PortTavernView.Instance.CloseView();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleSmugglerView();
            }
        }

        public void ToggleSmugglerView()
        {
            if (PortTavernView.Instance == null) return;

            if (PortTavernView.Instance.IsOpen)
            {
                PortTavernView.Instance.CloseView();
            }
            else
            {
                if (_playerLocomotion != null)
                {
                    _playerLocomotion.SetSailState(SailState.Anchored);
                }
                PortTavernView.Instance.OpenSmugglerCove(this);
            }
        }

        private void OnGUI()
        {
            if (!_isPlayerMoored) return;
            if (PortTavernView.Instance != null && PortTavernView.Instance.IsOpen) return;

            // 1. Fixed, prominent screen-space dock button at bottom
            float btnWidth = 360f;
            float btnHeight = 40f;
            float btnX = (Screen.width - btnWidth) * 0.5f;
            float btnY = Screen.height - 86f;

            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), $"💀 TIE OFF AT SMUGGLER COVE [T]"))
            {
                ToggleSmugglerView();
            }

            // 2. 3D In-World clickable floating badge above dock
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 promptPos = transform.position + _covePromptOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(promptPos);

            if (screenPos.z < 0.2f) return;

            float screenY = Screen.height - screenPos.y;
            float badgeWidth = 340f;
            float badgeHeight = 32f;
            float startX = screenPos.x - (badgeWidth * 0.5f);

            if (GUI.Button(new Rect(startX, screenY, badgeWidth, badgeHeight), $"💀 [T] TIE OFF AT COVE"))
            {
                ToggleSmugglerView();
            }
        }
    }
}
