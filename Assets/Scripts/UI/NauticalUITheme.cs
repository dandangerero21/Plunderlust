using System;
using System.IO;
using UnityEngine;

namespace Plunderlust.UI
{
    /// <summary>
    /// Central design system tokens and Kenney UI Pack integration for the
    /// 17th-Century Nautical Aesthetic across all mobile and HUD views.
    /// Incorporates Kenney Vector Double UI sprites color-graded to authentic pirate palettes:
    /// - Dark Weathered Mahogany: #1B140E / #261912
    /// - Aged Parchment Inset: #D8C39E / #E6DAC0
    /// - Antique Brass Border: #C59B27 / #8C6C24
    /// - Blood-Crimson: #B22222 / #C42020
    /// - Emerald-Sea: #1E8B65
    /// - Deep Bilge Ocean: #1A5B7A
    /// - Bright Doubloon Gold: #FFD700
    /// </summary>
    public static class NauticalUITheme
    {
        // Color Tokens
        public static readonly Color MahoganyDark   = new Color(0.106f, 0.078f, 0.055f, 0.94f); // #1B140E
        public static readonly Color MahoganyMid    = new Color(0.180f, 0.130f, 0.090f, 0.96f);
        public static readonly Color ParchmentBase  = new Color(0.847f, 0.765f, 0.620f, 1.00f); // #D8C39E
        public static readonly Color ParchmentDark  = new Color(0.720f, 0.630f, 0.480f, 1.00f);
        public static readonly Color ParchmentLight = new Color(0.920f, 0.860f, 0.740f, 1.00f);
        public static readonly Color BrassBorder    = new Color(0.773f, 0.608f, 0.153f, 1.00f); // #C59B27
        public static readonly Color BrassHighlight = new Color(0.950f, 0.820f, 0.350f, 1.00f);
        public static readonly Color BrassDark      = new Color(0.480f, 0.360f, 0.080f, 1.00f);
        public static readonly Color CrimsonBlood   = new Color(0.698f, 0.133f, 0.133f, 1.00f); // #B22222
        public static readonly Color EmeraldSea     = new Color(0.118f, 0.545f, 0.396f, 1.00f); // #1E8B65
        public static readonly Color AmberWarning   = new Color(0.898f, 0.663f, 0.235f, 1.00f); // #E5A93C
        public static readonly Color BilgeOcean     = new Color(0.102f, 0.357f, 0.478f, 1.00f); // #1A5B7A
        public static readonly Color GoldAccent     = new Color(1.000f, 0.843f, 0.000f, 1.00f); // #FFD700
        public static readonly Color InkBlack       = new Color(0.080f, 0.060f, 0.040f, 1.00f);

        // Cached procedural & Kenney UI pack sprites
        private static Sprite _solidWhiteSprite;
        private static Sprite _parchmentPanelSprite;
        private static Sprite _mahoganyPanelSprite;
        private static Sprite _brassBorderSprite;
        private static Sprite _brassPanelSprite;
        private static Sprite _circleWaxSealSprite;
        private static Sprite _emeraldSealSprite;
        private static Sprite _brassSealSprite;
        private static Sprite _slideTrackVertical;
        private static Sprite _slideHandle;
        private static Sprite _dividerSprite;
        private static Sprite _arrowNeedleSprite;
        private static Sprite _compassRoseSprite;
        private static Sprite _shipHullSilhouetteSprite;
        private static Sprite _crossedYardarmsSprite;
        private static Sprite _rudderSternSprite;
        private static Sprite _cannonPipSprite;
        private static Font _defaultFont;

        public static Font GetDefaultFont()
        {
            if (_defaultFont == null)
            {
                // ponytail: authentic, highly readable 18th-century nautical serif font
                _defaultFont = Resources.Load<Font>("Fonts/PirateNautical");
#if UNITY_EDITOR
                if (_defaultFont == null) _defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/PirateNautical.ttf");
                if (_defaultFont == null) _defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PirateNautical.ttf");
#endif
                if (_defaultFont == null) _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_defaultFont == null) _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _defaultFont;
        }

        public static Sprite SolidWhite
        {
            get
            {
                if (_solidWhiteSprite == null)
                {
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    Color[] c = { Color.white, Color.white, Color.white, Color.white };
                    tex.SetPixels(c);
                    tex.Apply();
                    _solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                }
                return _solidWhiteSprite;
            }
        }

        public static Sprite ParchmentPanel
        {
            get
            {
                if (_parchmentPanelSprite == null)
                {
                    _parchmentPanelSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_border.png",
                        ParchmentBase,
                        new Color(0.48f, 0.36f, 0.22f), // Russet shadow
                        new Vector4(28f, 36f, 28f, 28f),
                        BrassBorder // Antiqued brass outer border
                    );
                    if (_parchmentPanelSprite == null)
                    {
                        _parchmentPanelSprite = CreateBorderedBoxSprite(64, 64, ParchmentBase, BrassBorder, 3);
                    }
                }
                return _parchmentPanelSprite;
            }
        }

        public static Sprite MahoganyPanel
        {
            get
            {
                if (_mahoganyPanelSprite == null)
                {
                    _mahoganyPanelSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_border.png",
                        MahoganyMid,
                        new Color(0.06f, 0.04f, 0.02f), // Deep ebony
                        new Vector4(28f, 36f, 28f, 28f),
                        new Color(0.55f, 0.42f, 0.15f) // Weathered brass trim
                    );
                    if (_mahoganyPanelSprite == null)
                    {
                        _mahoganyPanelSprite = CreateBorderedBoxSprite(64, 64, MahoganyDark, BrassBorder, 3);
                    }
                }
                return _mahoganyPanelSprite;
            }
        }

        public static Sprite BrassBorderBox
        {
            get
            {
                if (_brassBorderSprite == null)
                {
                    _brassBorderSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_border.png",
                        new Color(0, 0, 0, 0),
                        BrassDark,
                        new Vector4(28f, 36f, 28f, 28f),
                        BrassBorder
                    );
                    if (_brassBorderSprite == null)
                    {
                        _brassBorderSprite = CreateBorderedBoxSprite(48, 48, new Color(0, 0, 0, 0), BrassBorder, 3);
                    }
                }
                return _brassBorderSprite;
            }
        }

        public static Sprite BrassPanel
        {
            get
            {
                if (_brassPanelSprite == null)
                {
                    _brassPanelSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Yellow/Double/button_rectangle_depth_flat.png",
                        GoldAccent,
                        BrassDark,
                        new Vector4(28f, 36f, 28f, 28f)
                    );
                    if (_brassPanelSprite == null)
                    {
                        _brassPanelSprite = CreateBorderedBoxSprite(64, 64, BrassBorder, BrassHighlight, 3);
                    }
                }
                return _brassPanelSprite;
            }
        }

        public static Sprite CircleWaxSeal
        {
            get
            {
                if (_circleWaxSealSprite == null)
                {
                    _circleWaxSealSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Red/Double/button_round_depth_flat.png",
                        CrimsonBlood,
                        new Color(0.32f, 0.05f, 0.05f), // Dark burgundy wax
                        Vector4.zero
                    );
                    if (_circleWaxSealSprite == null)
                    {
                        _circleWaxSealSprite = CreateWaxSealSprite(48);
                    }
                }
                return _circleWaxSealSprite;
            }
        }

        public static Sprite EmeraldSeal
        {
            get
            {
                if (_emeraldSealSprite == null)
                {
                    _emeraldSealSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Green/Double/button_round_depth_flat.png",
                        EmeraldSea,
                        new Color(0.05f, 0.28f, 0.20f),
                        Vector4.zero
                    );
                    if (_emeraldSealSprite == null)
                    {
                        _emeraldSealSprite = CreateWaxSealSprite(48);
                    }
                }
                return _emeraldSealSprite;
            }
        }

        public static Sprite BrassSeal
        {
            get
            {
                if (_brassSealSprite == null)
                {
                    _brassSealSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Yellow/Double/button_round_depth_flat.png",
                        GoldAccent,
                        BrassDark,
                        Vector4.zero
                    );
                    if (_brassSealSprite == null)
                    {
                        _brassSealSprite = CreateWaxSealSprite(48);
                    }
                }
                return _brassSealSprite;
            }
        }

        public static Sprite SlideTrackVertical
        {
            get
            {
                if (_slideTrackVertical == null)
                {
                    _slideTrackVertical = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Grey/Double/slide_vertical_grey.png",
                        new Color(0.18f, 0.13f, 0.09f), // Weathered oak
                        new Color(0.08f, 0.06f, 0.04f), // Dark iron
                        new Vector4(12f, 24f, 12f, 24f)
                    );
                    if (_slideTrackVertical == null) _slideTrackVertical = MahoganyPanel;
                }
                return _slideTrackVertical;
            }
        }

        public static Sprite SlideHandle
        {
            get
            {
                if (_slideHandle == null)
                {
                    _slideHandle = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Yellow/Double/slide_hangle.png",
                        GoldAccent,
                        BrassDark,
                        Vector4.zero
                    );
                    if (_slideHandle == null) _slideHandle = BrassPanel;
                }
                return _slideHandle;
            }
        }

        public static Sprite Divider
        {
            get
            {
                if (_dividerSprite == null)
                {
                    _dividerSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Extra/Double/divider_edges.png",
                        BrassBorder,
                        BrassDark,
                        new Vector4(24f, 0f, 24f, 0f)
                    );
                    if (_dividerSprite == null) _dividerSprite = SolidWhite;
                }
                return _dividerSprite;
            }
        }

        public static Sprite ArrowNeedle
        {
            get
            {
                if (_arrowNeedleSprite == null)
                {
                    _arrowNeedleSprite = LoadAndColorGradeKenney(
                        "kenney_ui-pack/PNG/Yellow/Double/arrow_decorative_n.png",
                        GoldAccent,
                        BrassDark,
                        Vector4.zero
                    );
                }
                return _arrowNeedleSprite;
            }
        }

        public static Sprite CompassRose
        {
            get
            {
                if (_compassRoseSprite == null)
                {
                    _compassRoseSprite = CreateCompassRoseSprite(128);
                }
                return _compassRoseSprite;
            }
        }

        public static Sprite ShipHullSilhouette
        {
            get
            {
                if (_shipHullSilhouetteSprite == null)
                {
                    _shipHullSilhouetteSprite = CreateShipSilhouetteSprite(64, 128);
                }
                return _shipHullSilhouetteSprite;
            }
        }

        public static Sprite CrossedYardarms
        {
            get
            {
                if (_crossedYardarmsSprite == null)
                {
                    _crossedYardarmsSprite = CreateCrossedYardarmsSprite(48, 48);
                }
                return _crossedYardarmsSprite;
            }
        }

        public static Sprite RudderStern
        {
            get
            {
                if (_rudderSternSprite == null)
                {
                    _rudderSternSprite = CreateRudderSprite(24, 36);
                }
                return _rudderSternSprite;
            }
        }

        public static Sprite CannonPip
        {
            get
            {
                if (_cannonPipSprite == null)
                {
                    _cannonPipSprite = CreateCircleSprite(16, Color.white);
                }
                return _cannonPipSprite;
            }
        }

        // --- Kenney UI Asset Color-Grader & Loader ---

        private static Sprite LoadAndColorGradeKenney(string relativePath, Color topColor, Color shadowColor, Vector4 border, Color? borderColor = null)
        {
            try
            {
                byte[] data = null;

                // 1. Try loading raw bytes from Resources (works on Android APK, PC, iOS!)
                string resPath = ResolveKenneyResourcePath(relativePath);
                if (!string.IsNullOrEmpty(resPath))
                {
                    TextAsset ta = Resources.Load<TextAsset>(resPath);
                    if (ta != null && ta.bytes != null && ta.bytes.Length > 0)
                    {
                        data = ta.bytes;
                    }
                }

                // 2. Fallback to direct disk read for Editor
                if (data == null)
                {
                    string fullPath = Path.Combine(Application.dataPath, relativePath);
                    if (File.Exists(fullPath))
                    {
                        data = File.ReadAllBytes(fullPath);
                    }
                }

                if (data != null && data.Length > 0)
                {
                    Texture2D srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (srcTex.LoadImage(data))
                    {
                        Color[] pixels = srcTex.GetPixels();
                        int w = srcTex.width;
                        int h = srcTex.height;

                        for (int y = 0; y < h; y++)
                        {
                            for (int x = 0; x < w; x++)
                            {
                                int idx = y * w + x;
                                Color c = pixels[idx];
                                if (c.a <= 0.005f) continue;

                                float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

                                Color target;
                                if (borderColor.HasValue && lum < 0.28f)
                                {
                                    target = borderColor.Value;
                                }
                                else
                                {
                                    target = Color.Lerp(shadowColor, topColor, lum);
                                }
                                pixels[idx] = new Color(target.r, target.g, target.b, c.a * target.a);
                            }
                        }

                        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        outTex.filterMode = FilterMode.Bilinear;
                        outTex.SetPixels(pixels);
                        outTex.Apply();

                        return Sprite.Create(outTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NauticalUITheme] Failed to load Kenney asset '{relativePath}': {ex.Message}");
            }
            return null;
        }

        private static string ResolveKenneyResourcePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (relativePath.Contains("button_round_depth_flat"))
            {
                if (relativePath.Contains("Red")) return "KenneyUI/button_round_depth_flat_red";
                if (relativePath.Contains("Green")) return "KenneyUI/button_round_depth_flat_green";
                if (relativePath.Contains("Yellow")) return "KenneyUI/button_round_depth_flat_yellow";
            }
            string fileName = Path.GetFileNameWithoutExtension(relativePath);
            return "KenneyUI/" + fileName;
        }

        // --- Procedural Fallback Sprite Builders ---

        public static Sprite CreateBorderedBoxSprite(int width, int height, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = (x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth);
                    int idx = y * width + x;
                    if (isBorder)
                    {
                        if (y >= height - borderWidth || x < borderWidth)
                            pixels[idx] = Color.Lerp(border, BrassHighlight, 0.35f);
                        else
                            pixels[idx] = border;
                    }
                    else
                    {
                        pixels[idx] = fill;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(borderWidth, borderWidth, borderWidth, borderWidth));
        }

        public static Sprite CreateCircleSprite(int size, Color fill)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float radius = (size - 2) * 0.5f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    int idx = y * size + x;
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        pixels[idx] = new Color(fill.r, fill.g, fill.b, fill.a * alpha);
                    }
                    else
                    {
                        pixels[idx] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateCompassRoseSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = (size - 4) * 0.5f;

            Color ringGold = BrassBorder;
            Color ringHighlight = BrassHighlight;
            Color ringDark = BrassDark;
            Color shadowColor = MahoganyDark;
            Color lightPoint = GoldAccent;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    Vector2 offset = new Vector2((x + 0.5f) - center.x, (y + 0.5f) - center.y);
                    float r = offset.magnitude;

                    // 1. Concentric Brass Graduated Rings
                    if (r >= maxR - 3f && r <= maxR)
                    {
                        pixels[idx] = (y > center.y) ? ringHighlight : ringGold;
                        continue;
                    }
                    if (r >= maxR * 0.65f - 1.5f && r <= maxR * 0.65f + 1.5f)
                    {
                        pixels[idx] = ringDark;
                        continue;
                    }

                    // 2. 8-Point Compass Points
                    bool colored = false;
                    for (int p = 0; p < 8; p++)
                    {
                        float pointAngle = p * 45f;
                        bool isCardinal = (p % 2 == 0);
                        float pointLength = isCardinal ? maxR - 2f : maxR * 0.68f;
                        float baseWidth = isCardinal ? 7.5f : 4.5f;

                        float rad = -pointAngle * Mathf.Deg2Rad;
                        float localX = offset.x * Mathf.Cos(rad) - offset.y * Mathf.Sin(rad);
                        float localY = offset.x * Mathf.Sin(rad) + offset.y * Mathf.Cos(rad);

                        if (localX >= 0f && localX <= pointLength)
                        {
                            float halfWAtX = baseWidth * (1f - (localX / pointLength));
                            if (Mathf.Abs(localY) <= halfWAtX)
                            {
                                bool isNorth = (p == 2);
                                if (localY >= 0f)
                                    pixels[idx] = isNorth ? ringHighlight : lightPoint;
                                else
                                    pixels[idx] = isNorth ? CrimsonBlood : shadowColor;
                                colored = true;
                                break;
                            }
                        }
                    }

                    if (colored) continue;

                    // 3. Center Brass Boss & Center Ruby
                    if (r <= 7.5f)
                    {
                        if (r <= 3.5f)
                            pixels[idx] = CrimsonBlood;
                        else
                            pixels[idx] = ringGold;
                        continue;
                    }

                    pixels[idx] = Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateWaxSealSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float radius = (size - 3) * 0.5f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            Color waxRed = CrimsonBlood;
            Color waxHighlight = new Color(0.9f, 0.25f, 0.25f);
            Color brassRing = BrassBorder;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    int idx = y * size + x;
                    if (dist <= radius)
                    {
                        float edgeDist = radius - dist;
                        if (edgeDist < 2.5f)
                            pixels[idx] = brassRing;
                        else if (edgeDist < 4.0f)
                            pixels[idx] = Color.Lerp(waxRed, Color.black, 0.35f);
                        else
                        {
                            float light = 1f - (dist / radius) * 0.3f;
                            pixels[idx] = Color.Lerp(waxRed, waxHighlight, light * 0.25f);
                        }
                    }
                    else
                    {
                        pixels[idx] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateShipSilhouetteSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            float halfW = width * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                float beamCurve;
                if (t > 0.65f)
                {
                    float bowT = (t - 0.65f) / 0.35f;
                    beamCurve = Mathf.Cos(bowT * Mathf.PI * 0.5f);
                }
                else if (t < 0.20f)
                {
                    float sternT = (0.20f - t) / 0.20f;
                    beamCurve = 0.85f - sternT * 0.35f;
                }
                else
                {
                    beamCurve = 1.0f - Mathf.Pow(t - 0.45f, 2f) * 0.8f;
                }

                float rowMaxX = halfW * 0.82f * beamCurve;

                for (int x = 0; x < width; x++)
                {
                    float distFromCenter = Mathf.Abs(x - halfW);
                    int idx = y * width + x;

                    if (distFromCenter <= rowMaxX)
                    {
                        bool isGunwaleRim = (distFromCenter >= rowMaxX - 2.5f);
                        pixels[idx] = isGunwaleRim ? Color.white : new Color(0.9f, 0.9f, 0.9f, 0.85f);
                    }
                    else
                    {
                        pixels[idx] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateCrossedYardarmsSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float dx = x - halfW;
                    float dy = y - halfH;

                    bool isMast = (Mathf.Abs(dx) <= 1.5f && Mathf.Abs(dy) <= halfH * 0.85f);
                    bool isYardarm1 = (Mathf.Abs(dy) <= 1.5f && Mathf.Abs(dx) <= halfW * 0.85f);
                    bool isDiag1 = Mathf.Abs(dx - dy) <= 1.0f && (Mathf.Abs(dx) <= halfW * 0.6f);
                    bool isDiag2 = Mathf.Abs(dx + dy) <= 1.0f && (Mathf.Abs(dx) <= halfW * 0.6f);

                    if (isMast || isYardarm1 || isDiag1 || isDiag2)
                        pixels[idx] = Color.white;
                    else
                        pixels[idx] = Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        public static Sprite CreateRudderSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            float halfW = width * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                float bladeWidth = Mathf.Lerp(halfW * 0.9f, halfW * 0.4f, t);

                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (Mathf.Abs(x - halfW) <= bladeWidth)
                        pixels[idx] = Color.white;
                    else
                        pixels[idx] = Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}
