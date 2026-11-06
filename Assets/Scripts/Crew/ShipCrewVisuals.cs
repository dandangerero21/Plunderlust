using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Crew
{
    /// <summary>
    /// Spawns and manages 3D Kenney Mini Character models on ship decks.
    /// Follows the visual rule:
    /// - Starter Rowboat / Small craft: Displays the Player Captain at the helm.
    /// - Larger ships (Cutter, Brigantine, Galleon): Dynamically spawns hired Officers
    ///   at their respective stations (Cannons, Rigging, Helm, Carpenter bench).
    /// </summary>
    public class ShipCrewVisuals : MonoBehaviour
    {
        [Header("Captain Spawn Anchor")]
        [SerializeField] private Vector3 _rowboatCaptainLocalPos = new Vector3(0f, 0.40f, -0.65f);
        [SerializeField] private GameObject _captainModel;

        private CrewManager _crewManager;
        private bool _isInitialized = false;

        private void Start()
        {
            _crewManager = GetComponent<CrewManager>();
            InitializeCrewVisuals();
        }

        public void InitializeCrewVisuals()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // Spawn Captain for Rowboat
            if (_captainModel == null)
            {
                SpawnCaptainCharacter();
            }
        }

        private void SpawnCaptainCharacter()
        {
            GameObject captainPrefab = Resources.Load<GameObject>("Models/character-male-a");
#if UNITY_EDITOR
            if (captainPrefab == null)
            {
                captainPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_mini-characters/Models/FBX format/character-male-a.fbx");
            }
#endif
            if (captainPrefab != null)
            {
                _captainModel = Instantiate(captainPrefab, transform);
                _captainModel.name = "ShipCaptain_Model";
                _captainModel.transform.localPosition = _rowboatCaptainLocalPos;
                _captainModel.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                _captainModel.transform.localScale = Vector3.one * 0.75f;

                Material paletteMat = Resources.Load<Material>("M_KenneyPiratePalette");
#if UNITY_EDITOR
                if (paletteMat == null)
                {
                    paletteMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/M_KenneyPiratePalette.mat");
                }
#endif
                if (paletteMat != null)
                {
                    Renderer[] rends = _captainModel.GetComponentsInChildren<Renderer>();
                    foreach (var r in rends) r.sharedMaterial = paletteMat;
                }
            }
        }
    }
}
