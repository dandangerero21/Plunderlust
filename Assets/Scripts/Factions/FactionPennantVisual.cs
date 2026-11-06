using System.Collections.Generic;
using Plunderlust.Core.Data;
using UnityEngine;

namespace Plunderlust.Factions
{
    /// <summary>
    /// In-world 3D faction pennant/flag visualizer.
    /// Finds authentic Kenney ship flag meshes (flag-a, flag-b) on the masthead and stern flagpole,
    /// and dynamically tints them to telegraph diplomatic alignment:
    /// - Imperial Navy: Deep Royal Blue
    /// - Corsair Brethren: Crimson & Jet Black
    /// - Merchant Guild: Emerald Green
    /// - Freeport Traders: Warm Amber Gold
    /// - Surrendered / Struck Colors: Pure White Flag
    /// If attached to a small rowboat (which has no masts), cleanly mounts a small grounded stern pennant
    /// or avoids floating disconnected flag blocks.
    /// </summary>
    public class FactionPennantVisual : MonoBehaviour
    {
        [Header("Faction Alignment")]
        [SerializeField] private FactionType _faction = FactionType.MerchantGuild;
        [SerializeField] private bool _isSurrendered = false;

        private readonly List<Renderer> _flagRenderers = new List<Renderer>();
        private Material _flagMaterial;
        private GameObject _spawnedSternPennant;

        // Faction Colors
        private static readonly Color NavyBlue = new Color(0.11f, 0.23f, 0.55f);
        private static readonly Color CorsairCrimson = new Color(0.65f, 0.08f, 0.09f);
        private static readonly Color MerchantEmerald = new Color(0.12f, 0.48f, 0.24f);
        private static readonly Color FreeportAmber = new Color(0.88f, 0.60f, 0.12f);
        private static readonly Color WhiteFlag = new Color(0.96f, 0.96f, 0.96f);

        public FactionType Faction => _faction;
        public bool IsSurrendered => _isSurrendered;

        private void Start()
        {
            EnsureFlagVisual();
            ApplyFactionColor();
        }

        public void SetFaction(FactionType faction)
        {
            _faction = faction;
            _isSurrendered = false;
            EnsureFlagVisual();
            ApplyFactionColor();
        }

        public void SetSurrendered(bool surrendered)
        {
            _isSurrendered = surrendered;
            ApplyFactionColor();
        }

        public void RefreshPennants()
        {
            EnsureFlagVisual();
            ApplyFactionColor();
        }

        private void EnsureFlagVisual()
        {
            _flagRenderers.Clear();

            // 1. Search for existing flag meshes on the ship model (flag-a, flag-b, etc.)
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                string lowerName = r.gameObject.name.ToLower();
                if (lowerName.Contains("flag") || lowerName.Contains("pennant"))
                {
                    _flagRenderers.Add(r);
                }
            }

            // 2. If no flags exist (e.g. starter rowboat dinghy)
            if (_flagRenderers.Count == 0 && _spawnedSternPennant == null)
            {
                bool isRowboat = gameObject.name.ToLower().Contains("rowboat") ||
                                 gameObject.name.ToLower().Contains("boat-row");

                if (isRowboat)
                {
                    // Small grounded stern transom pennant on the rear of the rowboat
                    GameObject pennantPrefab = Resources.Load<GameObject>("Models/flag-pirate-pennant") ??
                                               Resources.Load<GameObject>("Models/flag-pennant");
#if UNITY_EDITOR
                    if (pennantPrefab == null)
                    {
                        pennantPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_pirate-kit/Models/FBX format/flag-pirate-pennant.fbx") ??
                                        UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/kenney_pirate-kit/Models/FBX format/flag-pennant.fbx");
                    }
#endif
                    if (pennantPrefab != null)
                    {
                        _spawnedSternPennant = Instantiate(pennantPrefab, transform);
                        _spawnedSternPennant.name = "SternTransom_Pennant";
                        _spawnedSternPennant.transform.localPosition = new Vector3(0f, 0.45f, -1.05f);
                        _spawnedSternPennant.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        _spawnedSternPennant.transform.localScale = Vector3.one * 0.45f;

                        Renderer r = _spawnedSternPennant.GetComponentInChildren<Renderer>();
                        if (r != null)
                        {
                            _flagRenderers.Add(r);
                        }
                    }
                }
            }

            // Create instanced material for faction colors
            if (_flagMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _flagMaterial = new Material(litShader);
            }

            foreach (var rend in _flagRenderers)
            {
                if (rend != null)
                {
                    rend.sharedMaterial = _flagMaterial;
                }
            }
        }

        private void ApplyFactionColor()
        {
            if (_flagMaterial == null) return;

            Color targetColor;
            if (_isSurrendered)
            {
                targetColor = WhiteFlag;
            }
            else
            {
                switch (_faction)
                {
                    case FactionType.ImperialNavy: targetColor = NavyBlue; break;
                    case FactionType.CorsairBrethren: targetColor = CorsairCrimson; break;
                    case FactionType.MerchantGuild: targetColor = MerchantEmerald; break;
                    case FactionType.FreeportTraders: targetColor = FreeportAmber; break;
                    default: targetColor = MerchantEmerald; break;
                }
            }

            if (_flagMaterial.HasProperty("_BaseColor"))
            {
                _flagMaterial.SetColor("_BaseColor", targetColor);
            }
            else if (_flagMaterial.HasProperty("_Color"))
            {
                _flagMaterial.SetColor("_Color", targetColor);
            }

            _flagMaterial.SetFloat("_Smoothness", 0.1f);
            _flagMaterial.SetFloat("_Metallic", 0.0f);
        }

        private void OnDestroy()
        {
            if (_flagMaterial != null)
            {
                Destroy(_flagMaterial);
            }
            if (_spawnedSternPennant != null)
            {
                Destroy(_spawnedSternPennant);
            }
        }
    }
}
