using Plunderlust.Core.Data;
using Plunderlust.Ship;
using UnityEngine;

namespace Plunderlust.Shipyard
{
    /// <summary>
    /// Physical 3D Drydock Slipway landmark in port harbors.
    /// Provides a dedicated repair canal with guide piles and scaffolding where
    /// the player can physically moor to refit and upgrade their vessel hull.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ShipyardDrydock : MonoBehaviour
    {
        [Header("Drydock Identity")]
        [SerializeField] private string _shipyardName = "Port Royal Royal Drydock";
        [SerializeField] private float _slipwayRadius = 14.0f;
        [SerializeField] private Vector3 _slipwayCenterOffset = Vector3.zero;

        private SphereCollider _slipwayTrigger;
        private bool _isPlayerInSlipway = false;
        private Transform _playerTransform;
        private ShipLocomotionController _playerLocomotion;

        public string ShipyardName => _shipyardName;
        public bool IsPlayerInSlipway => _isPlayerInSlipway;

        private void Awake()
        {
            _slipwayTrigger = GetComponent<SphereCollider>();
            if (_slipwayTrigger == null) _slipwayTrigger = gameObject.AddComponent<SphereCollider>();
            _slipwayTrigger.isTrigger = true;
            _slipwayTrigger.radius = _slipwayRadius;
            _slipwayTrigger.center = _slipwayCenterOffset;

            EnsurePhysicalColliders();
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

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.transform.root;
            if (other.CompareTag("Player") || root.CompareTag("Player"))
            {
                _isPlayerInSlipway = true;
                _playerTransform = root;
                _playerLocomotion = root.GetComponent<ShipLocomotionController>();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Transform root = other.transform.root;
            if (other.CompareTag("Player") || root.CompareTag("Player"))
            {
                if (_playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) > (_slipwayRadius * 1.25f))
                {
                    _isPlayerInSlipway = false;
                    _playerTransform = null;
                    _playerLocomotion = null;

                    if (ShipyardView.Instance != null && ShipyardView.Instance.IsOpen)
                    {
                        ShipyardView.Instance.CloseView();
                    }
                }
            }
        }

        private void Update()
        {
            // Fallback distance check
            if (_playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _playerLocomotion = player.GetComponent<ShipLocomotionController>();
                }
            }

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                _isPlayerInSlipway = dist <= (_slipwayRadius * 1.25f);
            }

            if (!_isPlayerInSlipway)
            {
                if (ShipyardView.Instance != null && ShipyardView.Instance.IsOpen && ShipyardView.Instance.ActiveDrydock == this)
                {
                    ShipyardView.Instance.CloseView();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleShipyard();
            }
        }

        public void ToggleShipyard()
        {
            if (ShipyardView.Instance == null) return;

            if (ShipyardView.Instance.IsOpen)
            {
                ShipyardView.Instance.CloseView();
            }
            else
            {
                if (_playerLocomotion != null)
                {
                    _playerLocomotion.SetSailState(SailState.Anchored);
                }
                ShipyardView.Instance.OpenDrydock(this);
            }
        }

        private void OnGUI()
        {
            if (!_isPlayerInSlipway) return;
            if (ShipyardView.Instance != null && ShipyardView.Instance.IsOpen) return;

            // Screen-space prominent action button
            float btnWidth = 360f;
            float btnHeight = 40f;
            float btnX = (Screen.width - btnWidth) * 0.5f;
            float btnY = Screen.height - 132f;

            GUIStyle btnStyle = Port.PortIslandHaven.GetMooringBtnStyle();
            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), $"🛠️ ENTER {_shipyardName.ToUpper()}", btnStyle))
            {
                ToggleShipyard();
            }
        }
    }
}
