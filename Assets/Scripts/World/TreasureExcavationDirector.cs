using System;
using System.Collections;
using Plunderlust.CameraControl;
using Plunderlust.Cargo;
using Plunderlust.Core.Data;
using Plunderlust.Ship;
using Plunderlust.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Plunderlust.World
{
    /// <summary>
    /// Master Cinematic Treasure Excavation Director.
    /// Orchestrates smooth camera blending, procedural 3-strike shovel digging animations,
    /// sand burst particle bursts, chest ascent, and mobile touch loot claiming.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class TreasureExcavationDirector : MonoBehaviour
    {
        public static TreasureExcavationDirector Instance { get; private set; }

        [Header("Audio Feedback Slots")]
        [SerializeField] private AudioClip _digStrikeSound;
        [SerializeField] private AudioClip _chestAscentSound;
        [SerializeField] private AudioClip _treasureFanfareSound;

        [Header("Excavation Constraints")]
        [SerializeField] private float _excavationDistanceThreshold = 18.0f;

        private GameObject _playerShip;
        private ShipLocomotionController _playerLocomotion;
        private ShipCargoHold _playerCargo;

        private TreasureIslandLandmark _activeNearbyAtoll;
        private bool _isExcavating = false;

        // Dynamic UGUI Mobile Touch Overlay
        private Canvas _directorCanvas;
        private GameObject _excavateButtonRoot;
        private GameObject _lootModalRoot;
        private Text _lootSummaryText;

        public bool IsExcavating => _isExcavating;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildMobileUIOverlay();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            _playerShip = GameObject.FindGameObjectWithTag("Player");
            if (_playerShip != null)
            {
                _playerLocomotion = _playerShip.GetComponent<ShipLocomotionController>();
                _playerCargo = _playerShip.GetComponent<ShipCargoHold>();
            }
        }

        private void BuildMobileUIOverlay()
        {
            GameObject canvasGo = new GameObject("TreasureExcavation_Canvas");
            canvasGo.transform.SetParent(transform, false);

            _directorCanvas = canvasGo.AddComponent<Canvas>();
            _directorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _directorCanvas.sortingOrder = 95;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // 1. Excavate Touch Button (Anchored bottom center)
            _excavateButtonRoot = new GameObject("Btn_ExcavateTreasure");
            _excavateButtonRoot.transform.SetParent(canvasGo.transform, false);

            RectTransform btnRt = _excavateButtonRoot.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 130f);
            btnRt.sizeDelta = new Vector2(340f, 68f);

            Image btnImg = _excavateButtonRoot.AddComponent<Image>();
            btnImg.sprite = NauticalUITheme.ParchmentPanel;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = NauticalUITheme.BrassBorder;

            Button btn = _excavateButtonRoot.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(StartExcavationSequence);

            GameObject btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(_excavateButtonRoot.transform, false);
            RectTransform txtRt = btnTextGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            Text btnTxt = btnTextGo.AddComponent<Text>();
            btnTxt.font = NauticalUITheme.GetDefaultFont();
            btnTxt.fontSize = 24;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = NauticalUITheme.InkBlack;
            btnTxt.text = "⚔️ EXCAVATE TREASURE";

            _excavateButtonRoot.SetActive(false);

            // 2. Booty Claimed Modal
            BuildLootClaimModal(canvasGo.transform);
        }

        private void BuildLootClaimModal(Transform parent)
        {
            _lootModalRoot = new GameObject("Modal_LootClaim");
            _lootModalRoot.transform.SetParent(parent, false);

            RectTransform modalRt = _lootModalRoot.AddComponent<RectTransform>();
            modalRt.anchorMin = new Vector2(0.5f, 0.5f);
            modalRt.anchorMax = new Vector2(0.5f, 0.5f);
            modalRt.pivot = new Vector2(0.5f, 0.5f);
            modalRt.anchoredPosition = Vector2.zero;
            modalRt.sizeDelta = new Vector2(520f, 320f);

            Image bg = _lootModalRoot.AddComponent<Image>();
            bg.sprite = NauticalUITheme.ParchmentPanel;
            bg.type = Image.Type.Sliced;
            bg.color = NauticalUITheme.ParchmentBase;

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_lootModalRoot.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -24f);
            titleRt.sizeDelta = new Vector2(0f, 40f);

            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = NauticalUITheme.GetDefaultFont();
            titleTxt.fontSize = 26;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = NauticalUITheme.InkBlack;
            titleTxt.text = "👑 BURIED BOOTY UNEARTHED!";

            // Summary text
            GameObject sumGo = new GameObject("Summary");
            sumGo.transform.SetParent(_lootModalRoot.transform, false);
            RectTransform sumRt = sumGo.AddComponent<RectTransform>();
            sumRt.anchorMin = new Vector2(0.1f, 0.35f);
            sumRt.anchorMax = new Vector2(0.9f, 0.85f);
            sumRt.sizeDelta = Vector2.zero;

            _lootSummaryText = sumGo.AddComponent<Text>();
            _lootSummaryText.font = NauticalUITheme.GetDefaultFont();
            _lootSummaryText.fontSize = 20;
            _lootSummaryText.fontStyle = FontStyle.Bold;
            _lootSummaryText.alignment = TextAnchor.MiddleCenter;
            _lootSummaryText.color = NauticalUITheme.InkBlack;
            _lootSummaryText.lineSpacing = 1.3f;
            _lootSummaryText.text = "✦ 650 Gold\n🌿 5x Spices  |  🧣 4x Silk  |  🍾 6x Rum";

            // Claim Booty Touch Button
            GameObject claimBtnGo = new GameObject("Btn_Claim");
            claimBtnGo.transform.SetParent(_lootModalRoot.transform, false);
            RectTransform claimRt = claimBtnGo.AddComponent<RectTransform>();
            claimRt.anchorMin = new Vector2(0.5f, 0f);
            claimRt.anchorMax = new Vector2(0.5f, 0f);
            claimRt.pivot = new Vector2(0.5f, 0f);
            claimRt.anchoredPosition = new Vector2(0f, 26f);
            claimRt.sizeDelta = new Vector2(240f, 54f);

            Image claimImg = claimBtnGo.AddComponent<Image>();
            claimImg.sprite = NauticalUITheme.CircleWaxSeal;
            claimImg.color = NauticalUITheme.EmeraldSea;

            Button claimBtn = claimBtnGo.AddComponent<Button>();
            claimBtn.targetGraphic = claimImg;
            claimBtn.onClick.AddListener(CompleteAndClaimBooty);

            GameObject claimTxtGo = new GameObject("Text");
            claimTxtGo.transform.SetParent(claimBtnGo.transform, false);
            RectTransform cTxtRt = claimTxtGo.AddComponent<RectTransform>();
            cTxtRt.anchorMin = Vector2.zero;
            cTxtRt.anchorMax = Vector2.one;
            cTxtRt.sizeDelta = Vector2.zero;

            Text cTxt = claimTxtGo.AddComponent<Text>();
            cTxt.font = NauticalUITheme.GetDefaultFont();
            cTxt.fontSize = 20;
            cTxt.fontStyle = FontStyle.Bold;
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.color = Color.white;
            cTxt.text = "CLAIM BOOTY";

            _lootModalRoot.SetActive(false);
        }

        private void Update()
        {
            if (_isExcavating) return;

            if (_playerShip == null) FindPlayer();
            if (_playerShip == null) return;

            // Find closest atoll landmark
            FindNearbyAtoll();

            bool canExcavate = false;
            if (_activeNearbyAtoll != null && !_activeNearbyAtoll.IsExcavated)
            {
                bool isAnchored = _playerLocomotion != null && _playerLocomotion.CurrentSailState == SailState.Anchored;
                bool hasMap = _playerCargo != null && _playerCargo.GetItemCount(CargoItemType.TreasureMap) > 0;
                canExcavate = isAnchored && hasMap;
            }

            if (_excavateButtonRoot != null)
            {
                _excavateButtonRoot.SetActive(canExcavate);
            }
        }

        private void FindNearbyAtoll()
        {
            _activeNearbyAtoll = null;
            var atolls = FindObjectsByType<TreasureIslandLandmark>(FindObjectsInactive.Exclude);
            float minSqrDist = _excavationDistanceThreshold * _excavationDistanceThreshold;

            for (int i = 0; i < atolls.Length; i++)
            {
                var atoll = atolls[i];
                if (atoll == null || atoll.IsExcavated) continue;

                float sqrDist = (atoll.transform.position - _playerShip.transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    _activeNearbyAtoll = atoll;
                }
            }
        }

        public void StartExcavationSequence()
        {
            if (_isExcavating || _activeNearbyAtoll == null) return;
            StartCoroutine(ExcavationCutsceneRoutine(_activeNearbyAtoll));
        }

        private IEnumerator ExcavationCutsceneRoutine(TreasureIslandLandmark atoll)
        {
            _isExcavating = true;
            if (_excavateButtonRoot != null) _excavateButtonRoot.SetActive(false);

            // Locate dig site items
            Transform digSite = atoll.transform.Find("ExcavationSite");
            Transform shovel = digSite != null ? digSite.Find("Shovel") : null;
            Transform hole = digSite != null ? digSite.Find("Hole") : null;
            Transform chest = digSite != null ? digSite.Find("Chest") : atoll.transform.Find("Chest");

            Vector3 digSiteCenter = digSite != null ? digSite.position : atoll.transform.position;

            // 1. Blend Camera to Beachside Diorama Angle
            if (NavalCameraController.Instance != null)
            {
                Vector3 camPos = digSiteCenter + new Vector3(-4.5f, 2.8f, -4.5f);
                Quaternion camRot = Quaternion.LookRotation((digSiteCenter + Vector3.up * 0.35f) - camPos);
                NavalCameraController.Instance.SetCinematicPose(camPos, camRot, blendSpeed: 2.8f);
            }

            yield return new WaitForSeconds(1.2f);

            // 2. Enable Shovel and Procedural 3-Strike Digging
            if (shovel != null)
            {
                shovel.gameObject.SetActive(true);
                Vector3 originalPos = shovel.localPosition;
                Quaternion originalRot = shovel.localRotation;

                for (int strike = 1; strike <= 3; strike++)
                {
                    // Raise shovel
                    float raiseElapsed = 0f;
                    while (raiseElapsed < 0.25f)
                    {
                        raiseElapsed += Time.deltaTime;
                        float t = raiseElapsed / 0.25f;
                        shovel.localPosition = originalPos + Vector3.up * 0.4f;
                        shovel.localRotation = originalRot * Quaternion.Euler(-25f * t, 0f, 0f);
                        yield return null;
                    }

                    // Downward jab into sand
                    float jabElapsed = 0f;
                    while (jabElapsed < 0.15f)
                    {
                        jabElapsed += Time.deltaTime;
                        float t = jabElapsed / 0.15f;
                        shovel.localPosition = originalPos - Vector3.up * 0.25f;
                        shovel.localRotation = originalRot * Quaternion.Euler(30f * t, 0f, 0f);
                        yield return null;
                    }

                    // Play dig strike audio & spawn sand burst
                    PlaySound(_digStrikeSound);
                    SpawnSandBurst(digSiteCenter);

                    yield return new WaitForSeconds(0.2f);
                }

                // Shovel rests aside
                shovel.localPosition = originalPos + new Vector3(0.6f, -0.2f, 0.3f);
                shovel.localRotation = Quaternion.Euler(85f, 20f, 0f);
            }

            yield return new WaitForSeconds(0.3f);

            // 3. Swap in Dug Hole Mesh
            if (hole != null)
            {
                hole.gameObject.SetActive(true);
            }

            // 4. Animate Chest Rising Out of the Sand with a Bounce Curve
            if (chest != null)
            {
                PlaySound(_chestAscentSound);

                Vector3 startPos = chest.localPosition;
                Vector3 targetPos = new Vector3(startPos.x, 0.28f, startPos.z);
                float riseElapsed = 0f;
                float riseDuration = 1.2f;

                while (riseElapsed < riseDuration)
                {
                    riseElapsed += Time.deltaTime;
                    float t = riseElapsed / riseDuration;
                    // Overshoot bounce curve
                    float bounce = Mathf.Sin(t * Mathf.PI * 0.5f) + Mathf.Sin(t * Mathf.PI * 2f) * 0.08f * (1f - t);
                    chest.localPosition = Vector3.LerpUnclamped(startPos, targetPos, bounce);
                    yield return null;
                }
                chest.localPosition = targetPos;

                // Gold sparkle burst
                SpawnGoldSparkles(chest.position);
            }

            PlaySound(_treasureFanfareSound);

            // 5. Award Booty to Player Hold
            int goldYield = UnityEngine.Random.Range(550, 950);
            int spicesYield = UnityEngine.Random.Range(3, 7);
            int silkYield = UnityEngine.Random.Range(2, 6);
            int rumYield = UnityEngine.Random.Range(4, 8);

            if (_playerCargo != null)
            {
                _playerCargo.ConsumeCargo(CargoItemType.TreasureMap, 1);
                _playerCargo.AddCargo(CargoItemType.Gold, goldYield, out _);
                _playerCargo.AddCargo(CargoItemType.Spices, spicesYield, out _);
                _playerCargo.AddCargo(CargoItemType.Silk, silkYield, out _);
                _playerCargo.AddCargo(CargoItemType.Rum, rumYield, out _);
            }

            atoll.MarkExcavated();

            // 6. Show Loot Claim Modal
            if (_lootSummaryText != null)
            {
                _lootSummaryText.text = $"✦ {goldYield} Gold\n🌿 {spicesYield}x Spices  |  🧣 {silkYield}x Silk  |  🍾 {rumYield}x Rum";
            }
            if (_lootModalRoot != null)
            {
                _lootModalRoot.SetActive(true);
            }
        }

        public void CompleteAndClaimBooty()
        {
            if (_lootModalRoot != null) _lootModalRoot.SetActive(false);

            // Release Camera back to standard 3rd person ship pose
            if (NavalCameraController.Instance != null)
            {
                NavalCameraController.Instance.ReleaseCinematicPose();
            }

            _isExcavating = false;
            Debug.Log("<color=gold>[TreasureDirector]</color> Booty successfully claimed! Voyage resumed.");
        }

        private void SpawnSandBurst(Vector3 pos)
        {
            // Lightweight procedural sand burst cubes
            for (int i = 0; i < 6; i++)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "SandParticle";
                p.transform.position = pos + Vector3.up * 0.2f;
                p.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.08f, 0.16f);

                Collider col = p.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer r = p.GetComponent<Renderer>();
                if (r != null && PrefabRegistrySO.Instance != null && PrefabRegistrySO.Instance.KenneyPaletteMaterial != null)
                {
                    r.sharedMaterial = PrefabRegistrySO.Instance.KenneyPaletteMaterial;
                }

                Rigidbody rb = p.AddComponent<Rigidbody>();
                Vector3 blast = (Vector3.up * UnityEngine.Random.Range(2.5f, 4.5f)) + (UnityEngine.Random.insideUnitSphere * 1.5f);
                rb.linearVelocity = blast;

                Destroy(p, UnityEngine.Random.Range(0.6f, 1.2f));
            }
        }

        private void SpawnGoldSparkles(Vector3 pos)
        {
            for (int i = 0; i < 12; i++)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "GoldSparkle";
                g.transform.position = pos + Vector3.up * 0.4f + UnityEngine.Random.insideUnitSphere * 0.4f;
                g.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.06f, 0.12f);

                Collider col = g.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer r = g.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = new Color(1.0f, 0.85f, 0.2f);
                }

                Rigidbody rb = g.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.linearVelocity = (Vector3.up * UnityEngine.Random.Range(1.2f, 2.5f)) + (UnityEngine.Random.insideUnitSphere * 0.8f);

                Destroy(g, UnityEngine.Random.Range(0.8f, 1.5f));
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position);
            }
        }
    }
}
