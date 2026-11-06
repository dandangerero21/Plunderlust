using System.Collections.Generic;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Core.Persistence;
using Plunderlust.Crew;
using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.Port
{
    /// <summary>
    /// Physical 3D Island Port Haven location on the sea.
    /// Built with modular Kenney Pirate Kit assets (Rocks, Docks, Watchtower).
    /// Provides an in-world sheltered harbor with a mooring zone.
    /// Dropping anchor opens the Port Haven Tavern to recruit deckhands and hire officers with Gold.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PortIslandHaven : MonoBehaviour
    {
        [Header("Port Identity")]
        [SerializeField] private string _portName = "Port Royal Haven";
        [SerializeField] private FactionType _rulingFaction = FactionType.MerchantGuild;
        [SerializeField] private int _deckhandGoldCost = 25;

        [Header("Mooring Harbor")]
        [SerializeField] private float _mooringRadius = 35.0f;
        [SerializeField] private Vector3 _dockPromptOffset = new Vector3(0f, 3.5f, 0f);

        private SphereCollider _harborTrigger;
        private bool _isPlayerMoored = false;
        private Transform _playerTransform;
        private ShipLocomotionController _playerLocomotion;

        // Hireable specialists currently at the tavern
        private readonly List<CrewSpecialistSO> _tavernRoster = new List<CrewSpecialistSO>();

        public string PortName => _portName;
        public FactionType RulingFaction => _rulingFaction;
        public int DeckhandGoldCost => _deckhandGoldCost;
        public IReadOnlyList<CrewSpecialistSO> TavernRoster => _tavernRoster;

        public void ConfigurePort(string name, FactionType faction, int deckhandCost)
        {
            _portName = name;
            _rulingFaction = faction;
            _deckhandGoldCost = deckhandCost;
        }

        private void Awake()
        {
            _harborTrigger = GetComponent<SphereCollider>();
            if (_harborTrigger == null)
            {
                _harborTrigger = gameObject.AddComponent<SphereCollider>();
            }
            _harborTrigger.isTrigger = true;
            _harborTrigger.radius = _mooringRadius;

            // Ensure solid primitive colliders and coastal repulsion trigger exist
            Plunderlust.World.IslandLandmassBarrier.AttachToPort(transform);

            EnsurePhysicalColliders();
            EnsureCommodityMarket();
            GenerateTavernRoster();
        }

        private void EnsureCommodityMarket()
        {
            PortCommodityMarket market = GetComponent<PortCommodityMarket>();
            if (market == null) market = gameObject.AddComponent<PortCommodityMarket>();
            market.ConfigureMarket(_rulingFaction, false);
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

        private void GenerateTavernRoster()
        {
            _tavernRoster.Clear();

            // 1. Veteran Carpenter
            CrewSpecialistSO carp = ScriptableObject.CreateInstance<CrewSpecialistSO>();
            carp.InitializeRuntime("Silas 'Ironwood' Thorne", OfficerSpecialtyType.Carpenter, SpecialistTier.Veteran, 260, 110, CargoItemType.Timber, 5, 6.5f, 0.25f, "Former navy shipwright known for timber hull patches.");
            _tavernRoster.Add(carp);

            // 2. Master Gunner
            CrewSpecialistSO gunner = ScriptableObject.CreateInstance<CrewSpecialistSO>();
            gunner.InitializeRuntime("Captain Bartholomew Flint", OfficerSpecialtyType.MasterGunner, SpecialistTier.Veteran, 290, 120, CargoItemType.Cannonballs, 12, 0.22f, 0.35f, "Ordnance officer who doubles broadside reload rhythm.");
            _tavernRoster.Add(gunner);

            // 3. Legendary Bosun
            CrewSpecialistSO bosun = ScriptableObject.CreateInstance<CrewSpecialistSO>();
            bosun.InitializeRuntime("Grizzled 'Bilge-Rat' Ben", OfficerSpecialtyType.Boatswain, SpecialistTier.Legendary, 450, 190, CargoItemType.Rum, 4, 32f, 0.40f, "Keeps the bilge dry and shields deckhands from hostile grape-shot.");
            _tavernRoster.Add(bosun);
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.transform.root;
            if (other.CompareTag("Player") || root.CompareTag("Player"))
            {
                _isPlayerMoored = true;
                _playerTransform = root;
                _playerLocomotion = root.GetComponent<ShipLocomotionController>();

                // Auto-save voyage on safe arrival into port harbor
                if (ShipSaveManager.Instance != null)
                {
                    ShipSaveManager.Instance.SaveGame(root.gameObject);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Transform root = other.transform.root;
            if (other.CompareTag("Player") || root.CompareTag("Player"))
            {
                // Only mark unmoored if distance is truly outside mooring zone
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
                if (PortTavernView.Instance != null && PortTavernView.Instance.IsOpen && PortTavernView.Instance.ActiveHaven == this)
                {
                    PortTavernView.Instance.CloseView();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleTavern();
            }
        }

        public void ToggleTavern()
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
                PortTavernView.Instance.OpenPortHaven(this);
            }
        }

        private static GUIStyle _mooringBtnStyle;
        public static GUIStyle GetMooringBtnStyle()
        {
            if (_mooringBtnStyle == null)
            {
                _mooringBtnStyle = new GUIStyle(GUI.skin.button);
                _mooringBtnStyle.font = UI.NauticalUITheme.GetDefaultFont();
                _mooringBtnStyle.fontSize = 15;
                _mooringBtnStyle.fontStyle = FontStyle.Bold;
                _mooringBtnStyle.normal.textColor = new Color(1.0f, 0.90f, 0.40f);
                _mooringBtnStyle.alignment = TextAnchor.MiddleCenter;
            }
            return _mooringBtnStyle;
        }

        private void OnGUI()
        {
            if (!_isPlayerMoored) return;
            if (PortTavernView.Instance != null && PortTavernView.Instance.IsOpen) return;

            GUIStyle btnStyle = GetMooringBtnStyle();

            // 1. Fixed, prominent screen-space Moor button at bottom
            float btnWidth = 340f;
            float btnHeight = 40f;
            float btnX = (Screen.width - btnWidth) * 0.5f;
            float btnY = Screen.height - 86f;

            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), $"⚓ MOOR AT {_portName.ToUpper()}", btnStyle))
            {
                ToggleTavern();
            }

            // 2. 3D In-World clickable floating badge above dock
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 promptPos = transform.position + _dockPromptOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(promptPos);

            if (screenPos.z < 0.2f) return;

            float screenY = Screen.height - screenPos.y;
            float badgeWidth = 320f;
            float badgeHeight = 32f;
            float startX = screenPos.x - (badgeWidth * 0.5f);

            if (GUI.Button(new Rect(startX, screenY, badgeWidth, badgeHeight), $"⚓ MOOR SHIP", btnStyle))
            {
                ToggleTavern();
            }
        }
    }
}
