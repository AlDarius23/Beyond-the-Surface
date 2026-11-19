using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BeyondTheSurface
{
    /// <summary>
    /// Turns the greybox street into a rainy neon night city at runtime:
    /// night sky + fog, wet asphalt and puddles, lit windows, shop fronts, neon signs, street lamps, fairy lights,
    /// an extended city with cross streets and a far skyline, rain + splashes, steam, and city post-processing.
    /// Pedestrians and traffic live in <see cref="CityLife"/>, sound in <see cref="SoundDirector"/>.
    /// Layout (Unity coords): street runs along +Z (0..60), road |x|&lt;3.5, sidewalks 3.5..6 (top y 0.15), buildings |x| 6..18.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class CityAtmosphere : MonoBehaviour
    {
        public int seed = 1977;

        [Header("Night look")]
        public Color fogColor = new Color(0.075f, 0.055f, 0.11f);
        public float fogDensity = 0.012f;
        public Color skyHorizon = new Color(0.30f, 0.12f, 0.26f);
        public Color skyMid = new Color(0.10f, 0.05f, 0.14f);
        public Color skyZenith = new Color(0.012f, 0.012f, 0.03f);
        public Color moonColor = new Color(0.45f, 0.55f, 0.95f);
        public float moonIntensity = 0.12f;

        [Header("Street lamps")]
        public Color lampColor = new Color(1f, 0.62f, 0.32f);
        public float lampIntensity = 2.6f;

        [Header("Rain")]
        public bool rain = true;
        public int rainRate = 4500;

        [Header("Audio")]
        public AudioClip neonBuzz;

        public bool InCity { get; private set; }
        /// <summary>Things pedestrians must walk around on the sidewalks: xyz = position, w = radius.</summary>
        public static readonly List<Vector4> SidewalkObstacles = new List<Vector4>();
        static void Obstacle(Vector3 p, float r) { SidewalkObstacles.Add(new Vector4(p.x, p.y, p.z, r)); }
        public Transform Root { get { return root; } }

        Transform root, extRoot;
        System.Random rnd;
        Camera cam;
        ParticleSystem rainPS;
        Transform rainFollow;
        ReflectionProbe probe;
        Volume cityVolume;
        Light moon;
        readonly List<AudioSource> buzzes = new List<AudioSource>();
        readonly List<Renderer> blinkers = new List<Renderer>();
        readonly List<Light> flickerLights = new List<Light>();
        Material blinkOn, blinkOff;
        Material ledMat;

        Material matConcrete, matConcreteLight, matRoad, matSidewalk, matCurb, matPaint, matMetal, matDarkGlass;
        Material[] windowMats;
        Material[] puddleMats;

        static readonly string[] ShopNames = {
            "FARMACIE", "NON STOP", "BAR", "CAFE", "BISTRO", "SUSHI", "PIZZA", "KARAOKE", "FLORARIE", "RAMEN",
            "CASA DE SCHIMB", "LIBRARIE", "ELECTRO", "PATISERIE", "TATTOO", "KEBAB", "CINEMA", "FITNESS 24/7", "GELATO", "LOMBARD" };
        static readonly Color[] Neon = {
            new Color(1f, 0.10f, 0.55f), new Color(0.10f, 0.85f, 1f), new Color(1f, 0.15f, 0.12f), new Color(1f, 0.55f, 0.12f),
            new Color(0.25f, 1f, 0.45f), new Color(0.60f, 0.25f, 1f), new Color(0.85f, 0.90f, 1f) };

        float R() { return (float)rnd.NextDouble(); }
        float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }
        int RI(int a, int b) { return rnd.Next(a, b); }

        void Start()
        {
            rnd = new System.Random(seed);
            var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            cam = rig != null ? rig.Camera : Camera.main;
            if (rig != null) rig.gameObject.layer = 2; // rain collisions ignore the player's capsule

            root = new GameObject("CityDressing").transform;
            extRoot = new GameObject("ExtendedCity").transform;
            extRoot.SetParent(root, false);

            SidewalkObstacles.Clear();
            foreach (float z in new[] { 7.5f, 22.5f, 37.5f, 52.5f }) { Obstacle(new Vector3(-4.1f, 0, z), 0.15f); Obstacle(new Vector3(4.1f, 0, z), 0.15f); } // lamp poles
            HideGreyboxHelpers();
            BuildMaterials();
            ApplyEnvironment();
            RestyleGreybox();
            BuildStreetSurface();
            BuildMainFacades();
            BuildShopFronts();
            BuildBladeSigns();
            BuildBillboard();
            BuildStreetLamps();
            BuildFairyLights();
            BuildStreetProps();
            BuildExtendedCity();
            BuildSkyline();
            BuildRain();
            BuildSteam();
            BuildVolume();
            BuildReflectionProbe();
            InCity = true;
        }

        // ------------------------------------------------------------------ setup
        void HideGreyboxHelpers()
        {
            foreach (var n in new[] { "Environment", "street_reference_human" })
            {
                var g = GameObject.Find(n);
                if (g != null) g.SetActive(false);
            }
        }

        void BuildMaterials()
        {
            matConcrete = AtmosKit.Lit(new Color(0.075f, 0.075f, 0.09f), 0.35f);
            matConcreteLight = AtmosKit.Lit(new Color(0.16f, 0.16f, 0.18f), 0.45f);
            matRoad = AtmosKit.Lit(new Color(0.03f, 0.031f, 0.036f), 0.88f);
            matSidewalk = AtmosKit.Lit(new Color(0.10f, 0.10f, 0.11f), 0.62f);
            matCurb = AtmosKit.Lit(new Color(0.28f, 0.28f, 0.30f), 0.5f);
            matPaint = AtmosKit.Lit(new Color(0.75f, 0.75f, 0.72f), 0.55f);
            matMetal = AtmosKit.Lit(new Color(0.05f, 0.05f, 0.06f), 0.65f, 0.8f);
            matDarkGlass = AtmosKit.Lit(new Color(0.02f, 0.025f, 0.035f), 0.92f, 0.2f);
            windowMats = new[] {
                AtmosKit.WindowMaterial(seed + 1, 0.38f, 2.2f), AtmosKit.WindowMaterial(seed + 2, 0.28f, 2.6f),
                AtmosKit.WindowMaterial(seed + 3, 0.45f, 1.8f), AtmosKit.WindowMaterial(seed + 4, 0.22f, 3.0f) };
            puddleMats = new Material[3];
            for (int i = 0; i < puddleMats.Length; i++)
            {
                var m = AtmosKit.Lit(new Color(0.008f, 0.010f, 0.014f), 0.97f, 0.1f);
                var mask = AtmosKit.PuddleMask(seed + 10 + i);
                m.SetTexture("_BaseMap", mask); m.mainTexture = mask;
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                puddleMats[i] = m;
            }
            blinkOn = AtmosKit.Glow(new Color(1f, 0.05f, 0.05f) * 6f);
            blinkOff = AtmosKit.Glow(new Color(0.15f, 0.01f, 0.01f));
            ledMat = AtmosKit.Glow(new Color(0.2f, 0.4f, 1f) * 2f);
        }

        void ApplyEnvironment()
        {
            RenderSettings.skybox = AtmosKit.NightSky(skyHorizon, skyMid, skyZenith, new Color(0.02f, 0.015f, 0.03f));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.07f, 0.075f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.055f, 0.11f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { moon = l; break; }
            if (moon != null)
            {
                moon.color = moonColor; moon.intensity = moonIntensity;
                moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
                moon.shadows = LightShadows.Soft; moon.shadowStrength = 0.8f;
            }
            if (cam != null)
            {
                cam.farClipPlane = 900f; cam.allowHDR = true;
                var ucd = cam.GetComponent<UniversalAdditionalCameraData>();
                if (ucd != null) ucd.stopNaN = true; // safety net: one bad pixel must never flood bloom
            }
        }

        void RestyleGreybox()
        {
            SetMat("street_buildings", matConcrete);
            SetMat("street_ground", matRoad);
            SetMat("street_lights", matMetal);
            var cw = GameObject.Find("Crosswalk");
            if (cw != null) foreach (var r in cw.GetComponentsInChildren<Renderer>()) r.sharedMaterial = matPaint;
        }

        void SetMat(string name, Material m)
        {
            var g = GameObject.Find(name);
            if (g == null) return;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
        }

        // ------------------------------------------------------------------ ground
        void BuildStreetSurface()
        {
            var t = new GameObject("StreetSurface").transform; t.SetParent(root, false);
            for (int s = -1; s <= 1; s += 2)
            {
                // sidewalk top and curb
                AtmosKit.Facade("SidewalkTop", t, new Vector3(s * 4.75f, 0.152f, 30f), Vector3.up, 2.5f, 60f, matSidewalk, 1, 1, Vector2.zero);
                AtmosKit.Box("Curb", t, new Vector3(s * 3.55f, 0.08f, 30f), new Vector3(0.14f, 0.17f, 60f), matCurb);
            }
            // lane markings: dashed centre line, skip the crossing
            for (float z = 1.5f; z < 60f; z += 6f)
            {
                if (z > 24f && z < 36f) continue;
                AtmosKit.Box("Dash", t, new Vector3(0f, 0.004f, z), new Vector3(0.12f, 0.006f, 3f), matPaint);
            }
            AtmosKit.Box("StopLineA", t, new Vector3(0f, 0.004f, 26.2f), new Vector3(7f, 0.006f, 0.3f), matPaint);
            AtmosKit.Box("StopLineB", t, new Vector3(0f, 0.004f, 33.8f), new Vector3(7f, 0.006f, 0.3f), matPaint);

            // puddles on road and sidewalks
            for (int i = 0; i < 34; i++)
            {
                bool walk = R() < 0.35f;
                float x = walk ? (R() < 0.5f ? -1 : 1) * R(3.9f, 5.6f) : R(-3.2f, 3.2f);
                float z = R(1f, 59f);
                float y = walk ? 0.156f : 0.006f;
                float sx = walk ? R(0.7f, 1.6f) : R(1.2f, 3.6f), sz = sx * R(0.6f, 1.6f);
                var p = AtmosKit.Facade("Puddle", t, new Vector3(x, y, z), Vector3.up, sx, sz, puddleMats[i % puddleMats.Length], sx, sz, Vector2.zero);
                p.transform.RotateAround(new Vector3(x, y, z), Vector3.up, R(0f, 360f));
            }
        }

        // ------------------------------------------------------------------ buildings
        struct Block { public float x0, x1, z0, z1, h; public Block(float a, float b, float c, float d, float e) { x0 = a; x1 = b; z0 = c; z1 = d; h = e; } }

        static readonly Block[] MainBlocks = {
            new Block(6, 18, 0, 10, 10), new Block(6, 18, 11, 30, 20), new Block(6, 18, 31, 45, 8), new Block(6, 18, 46, 60, 14),
            new Block(-18, -6, 0, 14, 12), new Block(-18, -6, 15, 27, 18), new Block(-18, -6, 28, 42, 9), new Block(-18, -6, 43, 60, 15) };

        void BuildMainFacades()
        {
            var t = new GameObject("MainFacades").transform; t.SetParent(root, false);
            for (int i = 0; i < MainBlocks.Length; i++)
            {
                var b = MainBlocks[i];
                bool right = b.x0 > 0;
                float face = right ? b.x0 - 0.03f : b.x1 + 0.03f;
                Vector3 n = right ? Vector3.left : Vector3.right;
                var wm = windowMats[i % windowMats.Length];
                float len = b.z1 - b.z0, top = b.h + 0.15f;
                // upper floors (street side)
                AtmosKit.Facade("Windows", t, new Vector3(face, (4.6f + top) * 0.5f, (b.z0 + b.z1) * 0.5f), n, len, top - 4.6f, wm, 24f, 27.2f, new Vector2(RI(0, 8) / 8f, RI(0, 8) / 8f));
                // floor band / cornice between shops and windows
                AtmosKit.Box("Cornice", t, new Vector3(face + n.x * 0.12f, 4.35f, (b.z0 + b.z1) * 0.5f), new Vector3(0.3f, 0.25f, len), matConcreteLight);
                AtmosKit.Box("Parapet", t, new Vector3(face + n.x * 0.1f, top + 0.12f, (b.z0 + b.z1) * 0.5f), new Vector3(0.25f, 0.25f, len), matConcreteLight);
                // end walls facing the alleys / junctions
                foreach (float zf in new[] { b.z0 - 0.03f, b.z1 + 0.03f })
                {
                    Vector3 en = zf < b.z0 ? Vector3.back : Vector3.forward;
                    AtmosKit.Facade("EndWindows", t, new Vector3((b.x0 + b.x1) * 0.5f, (4.6f + top) * 0.5f, zf), en, b.x1 - b.x0, top - 4.6f, windowMats[(i + 1) % windowMats.Length], 24f, 27.2f, new Vector2(RI(0, 8) / 8f, 0));
                }
                // air conditioners under random windows
                int ac = RI(2, 6);
                for (int k = 0; k < ac; k++)
                {
                    float y = 5.2f + 3.4f * RI(0, Mathf.Max(1, (int)((top - 6f) / 3.4f)));
                    float z = b.z0 + 1.5f + 3f * RI(0, Mathf.Max(1, (int)((len - 3f) / 3f)));
                    AtmosKit.Box("AC", t, new Vector3(face + n.x * 0.3f, y, z), new Vector3(0.55f, 0.45f, 0.75f), matConcreteLight);
                }
            }
            // stepped towers on top of some blocks make the street feel taller
            Tower(t, new Vector3(12.5f, 0, 21f), 8f, 12f, 20.15f, 44f, 0);
            Tower(t, new Vector3(-12.5f, 0, 21f), 8f, 9f, 18.15f, 36f, 1);
            Tower(t, new Vector3(-12f, 0, 52f), 9f, 12f, 15.15f, 30f, 2);
            Tower(t, new Vector3(12f, 0, 53f), 8f, 10f, 14.15f, 26f, 3);
            Tower(t, new Vector3(13f, 0, 5f), 7f, 7f, 10.15f, 22f, 2);
        }

        GameObject Tower(Transform parent, Vector3 c, float sx, float sz, float baseY, float topY, int mat)
        {
            float h = topY - baseY;
            var g = AtmosKit.Box("Tower", parent, new Vector3(c.x, baseY + h * 0.5f, c.z), new Vector3(sx, h, sz), matConcrete);
            var wm = windowMats[mat % windowMats.Length];
            Vector2 off = new Vector2(RI(0, 8) / 8f, RI(0, 8) / 8f);
            AtmosKit.Facade("TW", parent, new Vector3(c.x - sx * 0.5f - 0.02f, baseY + h * 0.5f, c.z), Vector3.left, sz, h, wm, 24f, 27.2f, off);
            AtmosKit.Facade("TW", parent, new Vector3(c.x + sx * 0.5f + 0.02f, baseY + h * 0.5f, c.z), Vector3.right, sz, h, wm, 24f, 27.2f, off);
            AtmosKit.Facade("TW", parent, new Vector3(c.x, baseY + h * 0.5f, c.z - sz * 0.5f - 0.02f), Vector3.back, sx, h, wm, 24f, 27.2f, off);
            AtmosKit.Facade("TW", parent, new Vector3(c.x, baseY + h * 0.5f, c.z + sz * 0.5f + 0.02f), Vector3.forward, sx, h, wm, 24f, 27.2f, off);
            if (R() < 0.6f) AddBlinker(parent, new Vector3(c.x, topY + 1.2f, c.z));
            return g;
        }

        void AddBlinker(Transform parent, Vector3 p)
        {
            AtmosKit.Box("Antenna", parent, p - Vector3.up * 0.6f, new Vector3(0.08f, 1.2f, 0.08f), matMetal);
            var b = AtmosKit.Prim(PrimitiveType.Sphere, "AviationLight", parent, p, Vector3.one * 0.35f, blinkOff);
            blinkers.Add(b.GetComponent<Renderer>());
        }

        // ------------------------------------------------------------------ shop fronts + neon
        bool InDoorZone(bool right, float z)
        {
            float[] doors = right ? new[] { 5f, 20.5f, 34f, 38f, 53f } : new[] { 7f, 21f, 35f, 51.5f };
            foreach (var d in doors) if (Mathf.Abs(z - d) < (right && d == 34f ? 2.2f : 0.9f)) return true;
            return false;
        }

        void BuildShopFronts()
        {
            var t = new GameObject("ShopFronts").transform; t.SetParent(root, false);
            int nameIdx = RI(0, ShopNames.Length);
            int buzzCount = 0;
            foreach (var b in MainBlocks)
            {
                bool right = b.x0 > 0;
                float face = right ? b.x0 : b.x1;
                float s = right ? -1f : 1f;            // direction towards the street
                Vector3 n = new Vector3(s, 0, 0);
                Quaternion textRot = Quaternion.LookRotation(-n); // TMP faces its -Z: look away from the street so text reads from it
                float z = b.z0 + 0.6f;
                while (z < b.z1 - 2.5f)
                {
                    float w = Mathf.Min(R(3.6f, 6.5f), b.z1 - 0.6f - z);
                    float zc = z + w * 0.5f;
                    if (InDoorZone(right, zc) || w < 2.5f) { z += w + 0.4f; continue; }

                    Color interior = R() < 0.5f ? new Color(1f, 0.7f, 0.45f) : (R() < 0.5f ? new Color(0.6f, 0.85f, 1f) : Neon[RI(0, Neon.Length)]);
                    float bright = R(0.08f, 0.3f);
                    var glass = AtmosKit.Emissive(new Color(0.03f, 0.03f, 0.04f), interior * bright, 0.95f);
                    AtmosKit.Facade("ShopGlass", t, new Vector3(face + s * 0.02f, 1.85f, zc), n, w - 0.3f, 2.9f, glass, 1, 1, Vector2.zero);
                    AtmosKit.Box("ShopFrameTop", t, new Vector3(face + s * 0.06f, 3.35f, zc), new Vector3(0.12f, 0.15f, w), matMetal);
                    AtmosKit.Box("ShopFrameL", t, new Vector3(face + s * 0.06f, 1.75f, z + 0.08f), new Vector3(0.12f, 3.2f, 0.15f), matMetal);
                    AtmosKit.Box("ShopFrameR", t, new Vector3(face + s * 0.06f, 1.75f, z + w - 0.08f), new Vector3(0.12f, 3.2f, 0.15f), matMetal);
                    AtmosKit.Box("ShopSill", t, new Vector3(face + s * 0.08f, 0.3f, zc), new Vector3(0.16f, 0.3f, w), matConcreteLight);

                    // awning on some shops
                    if (R() < 0.35f)
                    {
                        var aw = AtmosKit.Lit(new[] { new Color(0.35f, 0.04f, 0.05f), new Color(0.03f, 0.18f, 0.16f), new Color(0.08f, 0.08f, 0.1f) }[RI(0, 3)], 0.7f);
                        var a = AtmosKit.Box("Awning", t, new Vector3(face + s * 0.6f, 3.55f, zc), new Vector3(1.2f, 0.06f, w - 0.2f), aw);
                        a.transform.rotation = Quaternion.Euler(0, 0, -s * 14f);
                    }

                    // sign above the shop
                    string label = ShopNames[nameIdx++ % ShopNames.Length];
                    Color nc = label == "FARMACIE" ? new Color(0.2f, 1f, 0.35f) : Neon[RI(0, Neon.Length)];
                    Vector3 signPos = new Vector3(face + s * 0.31f, 3.85f, zc);
                    if (R() < 0.55f)
                    {
                        AtmosKit.NeonText("Neon_" + label, label, t, signPos, textRot, new Vector2(Mathf.Min(w - 0.4f, 3.6f), 0.62f), nc, R(3f, 5.5f));
                    }
                    else
                    {   // light box sign
                        AtmosKit.Box("SignBox", t, signPos - n * 0.11f, new Vector3(0.18f, 0.7f, Mathf.Min(w - 0.4f, 3.4f)), AtmosKit.Glow(nc * 0.7f));
                        var txt = TextFactory.Create("SignText", label, t, signPos + n * 0.005f, textRot, new Vector2(Mathf.Min(w - 0.6f, 3.2f), 0.5f), new Color(0.03f, 0.03f, 0.04f), TextAlignmentOptions.Center, true, 40f);
                        txt.fontStyle = FontStyles.Bold;
                    }
                    if (label == "FARMACIE") PharmacyCross(t, new Vector3(face + s * 0.5f, 5.6f, z + 0.4f), nc);

                    var l = AtmosKit.PointLight("ShopLight", t, new Vector3(face + s * 1.3f, 3.0f, zc), Color.Lerp(nc, interior, 0.4f), 2.2f, 7f);
                    if (R() < 0.12f) flickerLights.Add(l);
                    if (neonBuzz != null && buzzCount < 6 && R() < 0.5f)
                    {
                        buzzes.Add(AtmosKit.Audio3D(l.gameObject, neonBuzz, 0.55f, 0.8f, 7f));
                        buzzCount++;
                    }
                    z += w + 0.4f;
                }
            }
        }

        void PharmacyCross(Transform t, Vector3 p, Color c)
        {
            var m = AtmosKit.Glow(c * 5f);
            AtmosKit.Box("CrossV", t, p, new Vector3(0.12f, 0.9f, 0.3f), m);
            AtmosKit.Box("CrossH", t, p, new Vector3(0.12f, 0.3f, 0.9f), m);
            AtmosKit.PointLight("CrossLight", t, p + Vector3.right * Mathf.Sign(-p.x) * 0.6f, c, 1.6f, 5f);
        }

        void BuildBladeSigns()
        {
            var t = new GameObject("BladeSigns").transform; t.SetParent(root, false);
            Blade(t, true, 17f, 7.5f, "HOTEL", Neon[1]);
            Blade(t, false, 23f, 8.5f, "BAR", Neon[0]);
            Blade(t, true, 49f, 6.5f, "NOODLE", Neon[3]);
            Blade(t, false, 46f, 7f, "CLUB", Neon[5]);
            Blade(t, true, 3f, 6.2f, "PUB", Neon[2]);
        }

        void Blade(Transform t, bool right, float z, float y, string word, Color c)
        {
            float face = right ? 6f : -6f, s = right ? -1f : 1f;
            float h = word.Length * 0.75f + 0.4f;
            var box = AtmosKit.Box("Blade_" + word, t, new Vector3(face + s * 0.75f, y, z), new Vector3(1.1f, h, 0.18f), matMetal);
            AtmosKit.Box("BladeArm", t, new Vector3(face + s * 0.12f, y + h * 0.5f - 0.1f, z), new Vector3(0.25f, 0.08f, 0.08f), matMetal);
            string vertical = string.Join("\n", System.Array.ConvertAll(word.ToCharArray(), ch => ch.ToString()));
            // readable from both directions along the street
            AtmosKit.NeonText("BladeTxtA", vertical, t, new Vector3(face + s * 0.75f, y, z - 0.1f), Quaternion.identity, new Vector2(0.9f, h - 0.3f), c, 4.5f).lineSpacing = -20f;
            AtmosKit.NeonText("BladeTxtB", vertical, t, new Vector3(face + s * 0.75f, y, z + 0.1f), Quaternion.Euler(0, 180, 0), new Vector2(0.9f, h - 0.3f), c, 4.5f).lineSpacing = -20f;
            // neon outline tubes
            var tube = AtmosKit.Glow(c * 4f);
            AtmosKit.Box("TubeL", t, new Vector3(face + s * 0.2f, y, z), new Vector3(0.04f, h, 0.22f), tube);
            AtmosKit.Box("TubeR", t, new Vector3(face + s * 1.3f, y, z), new Vector3(0.04f, h, 0.22f), tube);
            var l = AtmosKit.PointLight("BladeLight", t, new Vector3(face + s * 0.75f, y, z), c, 3f, 9f);
            if (neonBuzz != null && word == "HOTEL") buzzes.Add(AtmosKit.Audio3D(l.gameObject, neonBuzz, 0.4f, 1f, 9f));
        }

        void BuildBillboard()
        {
            var t = new GameObject("Billboard").transform; t.SetParent(root, false);
            // big LED screen on the roof of the low block across the crossing, facing the player's side of the street
            Vector3 c = new Vector3(8.5f, 12f, 38f);
            AtmosKit.Box("BillboardFrame", t, c + Vector3.right * 0.15f, new Vector3(0.3f, 5.6f, 11.6f), matMetal);
            AtmosKit.Box("BillboardLegA", t, new Vector3(9f, 8.6f, 34f), new Vector3(0.25f, 1.5f, 0.25f), matMetal);
            AtmosKit.Box("BillboardLegB", t, new Vector3(9f, 8.6f, 42f), new Vector3(0.25f, 1.5f, 0.25f), matMetal);
            AtmosKit.Facade("BillboardLED", t, c - Vector3.right * 0.02f, Vector3.left, 11f, 5f, ledMat, 1, 1, Vector2.zero);
            AtmosKit.NeonText("BillboardText", "ORASUL NU DOARME\n<size=55%>NICIODATA</size>", t, c - Vector3.right * 0.06f,
                Quaternion.LookRotation(Vector3.right), new Vector2(10f, 4f), new Color(1f, 0.95f, 0.9f), 2.2f);
            var l = AtmosKit.SpotLight("BillboardSpill", t, c + new Vector3(-1.5f, 0, 0), new Vector3(-1f, -0.6f, 0f), new Color(0.4f, 0.5f, 1f), 6f, 25f, 100f);
            billboardLight = l;
        }
        Light billboardLight;

        void BuildStreetLamps()
        {
            var t = new GameObject("StreetLamps").transform; t.SetParent(root, false);
            var head = AtmosKit.Glow(lampColor * 6f);
            foreach (float z in new[] { 7.5f, 22.5f, 37.5f, 52.5f })
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 hp = new Vector3(s * 2.9f, 4.93f, z);
                    AtmosKit.Box("LampGlow", t, hp, new Vector3(0.5f, 0.03f, 0.18f), head);
                    var l = AtmosKit.SpotLight("LampLight", t, hp + Vector3.down * 0.1f, new Vector3(-s * 0.15f, -1f, 0f), lampColor, lampIntensity, 13f, 115f);
                    l.shadows = LightShadows.None;
                }
        }

        void BuildFairyLights()
        {
            var t = new GameObject("FairyLights").transform; t.SetParent(root, false);
            var warm = AtmosKit.Glow(new Color(1f, 0.72f, 0.4f) * 4f);
            var cool = AtmosKit.Glow(new Color(0.55f, 0.75f, 1f) * 3.5f);
            var pink = AtmosKit.Glow(new Color(1f, 0.4f, 0.75f) * 3.5f);
            var wire = AtmosKit.Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f);
            foreach (float z in new[] { 4f, 13f, 25f, 41f, 50f, 57f })
            {
                Vector3 a = new Vector3(-6f, 8.2f + R(-0.6f, 0.6f), z + R(-1f, 1f));
                Vector3 b = new Vector3(6f, 8.2f + R(-0.6f, 0.6f), z + R(-1f, 1f));
                int count = 30;
                Vector3 prev = a;
                for (int i = 0; i <= count; i++)
                {
                    float k = i / (float)count;
                    Vector3 p = Vector3.Lerp(a, b, k) + Vector3.down * (Mathf.Sin(k * Mathf.PI) * 1.4f);
                    if (i > 0)
                    {
                        var seg = AtmosKit.Box("Wire", t, (p + prev) * 0.5f, new Vector3(0.012f, 0.012f, Vector3.Distance(p, prev)), wire);
                        seg.transform.rotation = Quaternion.LookRotation(p - prev);
                    }
                    if (i % 2 == 0 && i > 0 && i < count)
                    {
                        var m = R() < 0.75f ? warm : (R() < 0.5f ? cool : pink);
                        AtmosKit.Prim(PrimitiveType.Sphere, "Bulb", t, p + Vector3.down * 0.08f, Vector3.one * 0.08f, m)
                            .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    prev = p;
                }
            }
        }

        void BuildStreetProps()
        {
            var t = new GameObject("StreetProps").transform; t.SetParent(root, false);
            var bin = AtmosKit.Lit(new Color(0.06f, 0.09f, 0.08f), 0.6f, 0.5f);
            var wood = AtmosKit.Lit(new Color(0.18f, 0.11f, 0.07f), 0.55f);
            var red = AtmosKit.Lit(new Color(0.45f, 0.04f, 0.03f), 0.6f, 0.3f);
            var planter = AtmosKit.Lit(new Color(0.12f, 0.12f, 0.13f), 0.4f);
            var leaves = AtmosKit.Lit(new Color(0.035f, 0.08f, 0.045f), 0.12f);
            // bins, benches, bollards, hydrants, trees with lights
            foreach (float z in new[] { 10f, 26f, 44f, 56f })
            {
                AtmosKit.Prim(PrimitiveType.Cylinder, "Bin", t, new Vector3(4.0f, 0.55f, z), new Vector3(0.45f, 0.4f, 0.45f), bin);
                AtmosKit.Prim(PrimitiveType.Cylinder, "Bin", t, new Vector3(-4.0f, 0.55f, z + 6f), new Vector3(0.45f, 0.4f, 0.45f), bin);
                Obstacle(new Vector3(4.0f, 0, z), 0.25f); Obstacle(new Vector3(-4.0f, 0, z + 6f), 0.25f);
            }
            foreach (float z in new[] { 15f, 49f })
            {
                AtmosKit.Box("Bench", t, new Vector3(5.5f, 0.6f, z), new Vector3(0.5f, 0.08f, 1.8f), wood, true);
                AtmosKit.Box("BenchBack", t, new Vector3(5.75f, 0.9f, z), new Vector3(0.06f, 0.5f, 1.8f), wood);
                Obstacle(new Vector3(5.5f, 0, z - 0.6f), 0.4f); Obstacle(new Vector3(5.5f, 0, z + 0.6f), 0.4f);
            }
            AtmosKit.Prim(PrimitiveType.Cylinder, "Hydrant", t, new Vector3(-3.85f, 0.5f, 25f), new Vector3(0.22f, 0.35f, 0.22f), red);
            AtmosKit.Prim(PrimitiveType.Cylinder, "Hydrant", t, new Vector3(3.85f, 0.5f, 47f), new Vector3(0.22f, 0.35f, 0.22f), red);
            Obstacle(new Vector3(-3.85f, 0, 25f), 0.2f); Obstacle(new Vector3(3.85f, 0, 47f), 0.2f);
            BuildRailingsAndSignals(t);

            var bulb = AtmosKit.Glow(new Color(1f, 0.75f, 0.45f) * 3f);
            foreach (var p in new[] { new Vector3(-5.2f, 0, 12f), new Vector3(5.2f, 0, 23.5f), new Vector3(-5.2f, 0, 47f), new Vector3(5.2f, 0, 57f) })
            {
                Obstacle(p, 0.62f);
                AtmosKit.Box("Planter", t, p + Vector3.up * 0.45f, new Vector3(0.9f, 0.6f, 0.9f), planter, true);
                AtmosKit.Prim(PrimitiveType.Cylinder, "Trunk", t, p + Vector3.up * 1.6f, new Vector3(0.14f, 1.1f, 0.14f), wood);
                AtmosKit.Prim(PrimitiveType.Sphere, "Crown", t, p + Vector3.up * 3.3f, new Vector3(1.5f, 1.2f, 1.5f), leaves);
                AtmosKit.Prim(PrimitiveType.Sphere, "Crown", t, p + new Vector3(0.45f, 3.0f, 0.3f), new Vector3(1.1f, 0.9f, 1.1f), leaves);
                AtmosKit.Prim(PrimitiveType.Sphere, "Crown", t, p + new Vector3(-0.4f, 3.7f, -0.25f), new Vector3(1.0f, 0.85f, 1.0f), leaves);
                for (int i = 0; i < 18; i++)
                {
                    float a = i * 0.9f, yy = 2.6f + i * 0.08f;
                    AtmosKit.Prim(PrimitiveType.Sphere, "TreeBulb", t, p + new Vector3(Mathf.Cos(a) * 0.95f, yy, Mathf.Sin(a) * 0.95f), Vector3.one * 0.06f, bulb)
                        .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // bus stop with a glowing ad panel on the player's side
            var shelterGlass = AtmosKit.TransparentLit(new Color(0.6f, 0.7f, 0.8f, 0.18f), 0.95f);
            Vector3 bs = new Vector3(-5.3f, 0f, 33.5f);
            AtmosKit.Box("ShelterRoof", t, bs + new Vector3(0, 2.5f, 0), new Vector3(1.4f, 0.08f, 3.6f), matMetal);
            AtmosKit.Box("ShelterBack", t, bs + new Vector3(-0.6f, 1.3f, 0), new Vector3(0.04f, 2.3f, 3.4f), shelterGlass);
            AtmosKit.Box("ShelterAd", t, bs + new Vector3(-0.1f, 1.35f, 1.6f), new Vector3(1.0f, 1.8f, 0.1f), AtmosKit.Glow(new Color(1f, 0.85f, 0.7f) * 1.6f));
            AtmosKit.Box("ShelterBench", t, bs + new Vector3(-0.35f, 0.6f, -0.3f), new Vector3(0.4f, 0.06f, 2f), matMetal);
            AtmosKit.PointLight("ShelterLight", t, bs + new Vector3(0.1f, 2.3f, 0), new Color(0.8f, 0.9f, 1f), 1.5f, 5f);
            for (float dz = -1.5f; dz <= 1.51f; dz += 0.75f) Obstacle(bs + new Vector3(-0.3f, 0, dz), 0.55f);

            // warm light over the destination door so the goal reads from far away
            AtmosKit.SpotLight("DestinationLight", t, new Vector3(5.2f, 3.4f, 34f), new Vector3(0.4f, -1f, 0), new Color(1f, 0.8f, 0.55f), 6f, 6f, 90f);
        }

        Renderer[] pedRed, pedGreen;
        Material pedRedOn, pedRedOff, pedGreenOn, pedGreenOff;

        // guard railings along both curbs (gap only at the crossing) so the road can't be cut across, plus walk signals
        void BuildRailingsAndSignals(Transform t)
        {
            var rail = AtmosKit.Lit(new Color(0.18f, 0.19f, 0.2f), 0.75f, 0.85f);
            foreach (float s in new[] { -1f, 1f })
                foreach (var span in new[] { new Vector2(0.3f, 26.4f), new Vector2(33.6f, 59.7f) })
                {
                    float len = span.y - span.x, zc = (span.x + span.y) * 0.5f, x = s * 3.66f;
                    AtmosKit.Box("RailTop", t, new Vector3(x, 1.05f, zc), new Vector3(0.05f, 0.05f, len), rail);
                    AtmosKit.Box("RailMid", t, new Vector3(x, 0.62f, zc), new Vector3(0.03f, 0.03f, len), rail);
                    for (float z = span.x; z <= span.y + 0.01f; z += 2f)
                        AtmosKit.Box("RailPost", t, new Vector3(x, 0.6f, Mathf.Min(z, span.y)), new Vector3(0.06f, 0.9f, 0.06f), rail);
                    var col = new GameObject("RailCollider");
                    col.transform.SetParent(t, false);
                    col.transform.position = new Vector3(x, 0.8f, zc);
                    col.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1.6f, len);
                }

            pedRedOn = AtmosKit.Glow(new Color(1f, 0.08f, 0.05f) * 5f); pedRedOff = AtmosKit.Glow(new Color(0.12f, 0.01f, 0.01f));
            pedGreenOn = AtmosKit.Glow(new Color(0.1f, 1f, 0.45f) * 4.5f); pedGreenOff = AtmosKit.Glow(new Color(0.01f, 0.08f, 0.04f));
            var reds = new List<Renderer>(); var greens = new List<Renderer>();
            foreach (var p in new[] { new Vector3(-3.95f, 0.15f, 26.2f), new Vector3(3.95f, 0.15f, 33.8f), new Vector3(-3.95f, 0.15f, 33.8f), new Vector3(3.95f, 0.15f, 26.2f) })
            {
                float face = -Mathf.Sign(p.x); // the light faces people across the road
                AtmosKit.Box("WalkPole", t, p + Vector3.up * 1.4f, new Vector3(0.1f, 2.8f, 0.1f), matMetal);
                Obstacle(p, 0.12f);
                AtmosKit.Box("WalkHead", t, p + new Vector3(face * 0.12f, 2.45f, 0), new Vector3(0.18f, 0.7f, 0.32f), matMetal);
                reds.Add(AtmosKit.Box("WalkRed", t, p + new Vector3(face * 0.22f, 2.62f, 0), new Vector3(0.02f, 0.26f, 0.22f), pedRedOn).GetComponent<Renderer>());
                greens.Add(AtmosKit.Box("WalkGreen", t, p + new Vector3(face * 0.22f, 2.28f, 0), new Vector3(0.02f, 0.26f, 0.22f), pedGreenOff).GetComponent<Renderer>());
                AtmosKit.PointLight("WalkGlow", t, p + new Vector3(face * 0.5f, 2.5f, 0), new Color(1f, 0.15f, 0.1f), 0.6f, 3f).name = "WalkGlow";
            }
            pedRed = reds.ToArray(); pedGreen = greens.ToArray();
        }

        bool lastPedGreen;
        void UpdateWalkSignals()
        {
            if (pedRed == null) return;
            var tr = TrafficSystem.Instance;
            bool g = tr != null && tr.pedestrianGreen;
            if (g == lastPedGreen) return;
            lastPedGreen = g;
            foreach (var r in pedRed) r.sharedMaterial = g ? pedRedOff : pedRedOn;
            foreach (var r in pedGreen) r.sharedMaterial = g ? pedGreenOn : pedGreenOff;
            foreach (var l in root.GetComponentsInChildren<Light>()) if (l.name == "WalkGlow") l.color = g ? new Color(0.1f, 1f, 0.45f) : new Color(1f, 0.15f, 0.1f);
        }


        // ------------------------------------------------------------------ extended city
        void BuildExtendedCity()
        {
            // street continues both ways, with cross streets at the two ends of the playable block
            Segment(-110f, -12f);
            Segment(72f, 190f);
            Junction(-12f, 0f);
            Junction(60f, 72f);
            // invisible walls keep the player inside the scenario block
            Wall(new Vector3(0, 2, -2.5f), new Vector3(12, 4, 0.3f));
            Wall(new Vector3(0, 2, 62.5f), new Vector3(12, 4, 0.3f));
        }

        void Wall(Vector3 c, Vector3 s)
        {
            var g = new GameObject("PlayerBoundary");
            g.transform.SetParent(root, false);
            g.transform.position = c;
            g.AddComponent<BoxCollider>().size = s;
        }

        void Ground(Transform t, Vector3 c, Vector3 s, Material m, float top)
        {
            var g = AtmosKit.Box("Ground", t, new Vector3(c.x, top - s.y * 0.5f, c.z), s, m, true);
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        void Segment(float z0, float z1)
        {
            var t = new GameObject("Segment_" + z0).transform; t.SetParent(extRoot, false);
            float len = z1 - z0, zc = (z0 + z1) * 0.5f;
            Ground(t, new Vector3(0, 0, zc), new Vector3(7f, 0.3f, len), matRoad, 0f);
            Ground(t, new Vector3(4.75f, 0, zc), new Vector3(2.5f, 0.3f, len), matSidewalk, 0.15f);
            Ground(t, new Vector3(-4.75f, 0, zc), new Vector3(2.5f, 0.3f, len), matSidewalk, 0.15f);
            for (float z = z0 + 2f; z < z1; z += 6f) AtmosKit.Box("Dash", t, new Vector3(0, 0.004f, z), new Vector3(0.12f, 0.006f, 3f), matPaint);

            var head = AtmosKit.Glow(lampColor * 6f);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = z0;
                while (z < z1 - 4f)
                {
                    float w = Mathf.Min(R(9f, 20f), z1 - z);
                    float h = R(10f, 42f);
                    BuildingBlock(t, new Vector3(s * 12f, 0, z + w * 0.5f), 12f, w - 1f, h, s < 0 ? Vector3.right : Vector3.left, true);
                    z += w;
                }
                for (float lz = z0 + 7f; lz < z1; lz += 15f)
                {
                    AtmosKit.Box("Pole", t, new Vector3(s * 4.1f, 2.6f, lz), new Vector3(0.12f, 5.1f, 0.12f), matMetal);
                    AtmosKit.Box("Arm", t, new Vector3(s * 3.5f, 5.1f, lz), new Vector3(1.26f, 0.1f, 0.1f), matMetal);
                    AtmosKit.Box("LampGlow", t, new Vector3(s * 2.9f, 4.98f, lz), new Vector3(0.5f, 0.04f, 0.2f), head);
                    float dist = Mathf.Min(Mathf.Abs(lz - 0f), Mathf.Abs(lz - 60f));
                    if (dist < 60f)
                        AtmosKit.SpotLight("LampLight", t, new Vector3(s * 2.9f, 4.85f, lz), Vector3.down, lampColor, lampIntensity, 13f, 115f);
                }
            }
        }

        void Junction(float z0, float z1)
        {
            var t = new GameObject("Junction_" + z0).transform; t.SetParent(extRoot, false);
            float zc = (z0 + z1) * 0.5f, len = z1 - z0;
            Ground(t, new Vector3(0, 0, zc), new Vector3(340f, 0.3f, len), matRoad, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Ground(t, new Vector3(s * 92f, 0, z0 + 1.25f), new Vector3(170f, 0.3f, 2.5f), matSidewalk, 0.15f);
                Ground(t, new Vector3(s * 92f, 0, z1 - 1.25f), new Vector3(170f, 0.3f, 2.5f), matSidewalk, 0.15f);
                // building rows along the cross street (behind the main blocks)
                foreach (float side in new[] { -1f, 1f })
                {
                    float rowZ = side < 0 ? z0 - 8f : z1 + 8f;
                    Vector3 face = side < 0 ? Vector3.forward : Vector3.back;
                    float x = 19f;
                    while (x < 175f)
                    {
                        float w = R(10f, 22f);
                        BuildingBlock(t, new Vector3(s * (x + w * 0.5f), 0, rowZ), w - 1f, 15f, R(14f, 55f), face, x < 80f);
                        x += w;
                    }
                }
                // crossing stripes
                for (float x = -3f; x <= 3.01f; x += 0.9f)
                    AtmosKit.Box("Stripe", t, new Vector3(x, 0.004f, s < 0 ? z0 + 3.2f : z1 - 3.2f), new Vector3(0.5f, 0.006f, 2.4f), matPaint);
            }
            // traffic lights at the corners
            var red = AtmosKit.Glow(new Color(1f, 0.08f, 0.05f) * 5f);
            var green = AtmosKit.Glow(new Color(0.1f, 1f, 0.5f) * 4f);
            foreach (var p in new[] { new Vector3(-4.4f, 0, z0 + 1f), new Vector3(4.4f, 0, z1 - 1f) })
            {
                AtmosKit.Box("SignalPole", t, p + Vector3.up * 1.8f, new Vector3(0.12f, 3.6f, 0.12f), matMetal);
                AtmosKit.Box("SignalHead", t, p + Vector3.up * 3.6f, new Vector3(0.35f, 0.9f, 0.3f), matMetal);
                AtmosKit.Prim(PrimitiveType.Sphere, "Red", t, p + new Vector3(0, 3.85f, p.z < zc ? 0.17f : -0.17f), Vector3.one * 0.2f, red);
                AtmosKit.Prim(PrimitiveType.Sphere, "Green", t, p + new Vector3(0, 3.35f, p.z < zc ? 0.17f : -0.17f), Vector3.one * 0.2f, green);
                AtmosKit.PointLight("SignalGlow", t, p + new Vector3(0, 3.7f, p.z < zc ? 0.6f : -0.6f), new Color(1f, 0.15f, 0.1f), 1.2f, 5f);
            }
            AtmosKit.SpotLight("JunctionLight", t, new Vector3(0, 7f, zc), Vector3.down, lampColor, lampIntensity * 1.2f, 16f, 120f);
        }

        void BuildingBlock(Transform t, Vector3 c, float sx, float sz, float h, Vector3 streetNormal, bool signs)
        {
            AtmosKit.Box("Block", t, new Vector3(c.x, h * 0.5f, c.z), new Vector3(sx, h, sz), matConcrete, true)
                .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var wm = windowMats[RI(0, windowMats.Length)];
            Vector2 off = new Vector2(RI(0, 8) / 8f, RI(0, 8) / 8f);
            float faceW = Mathf.Abs(streetNormal.x) > 0.5f ? sz : sx;
            Vector3 fc = c + new Vector3(streetNormal.x * (sx * 0.5f + 0.02f), 0, streetNormal.z * (sz * 0.5f + 0.02f));
            AtmosKit.Facade("BW", t, new Vector3(fc.x, (4.5f + h) * 0.5f, fc.z), streetNormal, faceW, h - 4.5f, wm, 24f, 27.2f, off);
            // shop band
            Color ic = R() < 0.6f ? new Color(1f, 0.7f, 0.45f) : Neon[RI(0, Neon.Length)];
            AtmosKit.Facade("Shops", t, new Vector3(fc.x, 1.9f, fc.z), streetNormal, faceW - 1f, 2.8f,
                AtmosKit.Emissive(new Color(0.02f, 0.02f, 0.03f), ic * R(0.15f, 0.5f), 0.9f), 1, 1, Vector2.zero);
            if (signs && R() < 0.6f)
            {
                var nc = Neon[RI(0, Neon.Length)];
                AtmosKit.NeonText("FarNeon", ShopNames[RI(0, ShopNames.Length)], t, new Vector3(fc.x, 3.95f, fc.z) + streetNormal * 0.05f,
                    Quaternion.LookRotation(-streetNormal), new Vector2(Mathf.Min(faceW - 1f, 5f), 0.8f), nc, 4f);
            }
            if (R() < 0.25f) AddBlinker(t, new Vector3(c.x, h + 1.2f, c.z));
        }

        void BuildSkyline()
        {
            var t = new GameObject("Skyline").transform; t.SetParent(root, false);
            var crownColors = new[] { new Color(0.3f, 0.8f, 1f), new Color(1f, 0.3f, 0.7f), new Color(1f, 0.9f, 0.8f), new Color(0.6f, 0.4f, 1f) };
            int placed = 0, tries = 0;
            var spots = new List<Vector3>();
            while (placed < 85 && tries < 2000)
            {
                tries++;
                float ang = R(0f, Mathf.PI * 2f), rad = R(85f, 340f);
                Vector3 p = new Vector3(Mathf.Cos(ang) * rad, 0, 40f + Mathf.Sin(ang) * rad);
                if (Mathf.Abs(p.x) < 32f && p.z > -140f && p.z < 220f) continue;           // main street corridor
                if (Mathf.Abs(p.x) < 185f && (Mathf.Abs(p.z + 6f) < 34f || Mathf.Abs(p.z - 66f) < 34f)) continue; // cross streets
                if (p.x > 150f && p.x < 250f && Mathf.Abs(p.z) < 50f) continue;            // hospital set
                bool clash = false;
                foreach (var q in spots) if ((q - p).sqrMagnitude < 26f * 26f) { clash = true; break; }
                if (clash) continue;
                spots.Add(p);
                float w = R(14f, 30f), d = R(14f, 30f), h = R(35f, 150f) * (rad < 160f ? 0.8f : 1f);
                var g = AtmosKit.Box("SkylineTower", t, new Vector3(p.x, h * 0.5f, p.z), new Vector3(w, h, d), matConcrete);
                g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var wm = windowMats[RI(0, windowMats.Length)];
                Vector2 off = new Vector2(RI(0, 8) / 8f, RI(0, 8) / 8f);
                AtmosKit.Facade("SW", t, new Vector3(p.x - w * 0.5f - 0.05f, h * 0.5f, p.z), Vector3.left, d, h, wm, 24f, 27.2f, off);
                AtmosKit.Facade("SW", t, new Vector3(p.x + w * 0.5f + 0.05f, h * 0.5f, p.z), Vector3.right, d, h, wm, 24f, 27.2f, off);
                AtmosKit.Facade("SW", t, new Vector3(p.x, h * 0.5f, p.z - d * 0.5f - 0.05f), Vector3.back, w, h, wm, 24f, 27.2f, off);
                AtmosKit.Facade("SW", t, new Vector3(p.x, h * 0.5f, p.z + d * 0.5f + 0.05f), Vector3.forward, w, h, wm, 24f, 27.2f, off);
                if (R() < 0.35f)
                {
                    var cc = crownColors[RI(0, crownColors.Length)];
                    AtmosKit.Box("Crown", t, new Vector3(p.x, h + 0.6f, p.z), new Vector3(w + 0.4f, 0.5f, d + 0.4f), AtmosKit.Glow(cc * 3f));
                }
                if (h > 70f) AddBlinker(t, new Vector3(p.x, h + 2f, p.z));
                placed++;
            }
        }

        // ------------------------------------------------------------------ weather
        void BuildRain()
        {
            if (!rain) return;
            rainFollow = new GameObject("RainFollow").transform;
            rainFollow.SetParent(root, false);

            var go = new GameObject("Rain");
            go.transform.SetParent(rainFollow, false);
            go.transform.localPosition = new Vector3(0, 9f, 2f);
            rainPS = go.AddComponent<ParticleSystem>();
            rainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rainPS.main;
            main.loop = true; main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = 1.3f;
            main.startSpeed = 0f;
            main.startSize = 0.018f;
            main.startColor = new Color(0.65f, 0.72f, 0.85f, 0.45f);
            main.maxParticles = 9000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = rainPS.emission; em.rateOverTime = rainRate;
            var sh = rainPS.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26f, 0.1f, 26f);
            var vel = rainPS.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); vel.y = new ParticleSystem.MinMaxCurve(-15f, -13f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var col = rainPS.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D; col.quality = ParticleSystemCollisionQuality.Medium;
            col.lifetimeLoss = 1f; col.collidesWith = ~(1 << 2); col.sendCollisionMessages = false; col.maxCollisionShapes = 64;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.04f; rend.lengthScale = 1.5f;
            rend.sharedMaterial = AtmosKit.Particles(new Color(0.7f, 0.78f, 0.9f, 0.5f), AtmosKit.Streak(), true);
            rend.shadowCastingMode = ShadowCastingMode.Off; rend.receiveShadows = false;

            // splashes spawned where drops hit the ground
            var sgo = new GameObject("RainSplash");
            sgo.transform.SetParent(go.transform, false);
            var splash = sgo.AddComponent<ParticleSystem>();
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = splash.main;
            sm.loop = false; sm.playOnAwake = false; sm.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f); sm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            sm.startColor = new Color(0.75f, 0.82f, 0.95f, 0.5f); sm.gravityModifier = 0.6f; sm.maxParticles = 4000;
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            var sem = splash.emission; sem.rateOverTime = 0f;
            var ssh = splash.shape; ssh.shapeType = ParticleSystemShapeType.Hemisphere; ssh.radius = 0.02f;
            var sz = splash.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.4f));
            var sc = splash.colorOverLifetime; sc.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            sc.color = grad;
            var srend = sgo.GetComponent<ParticleSystemRenderer>();
            srend.sharedMaterial = AtmosKit.Particles(new Color(0.8f, 0.85f, 1f, 0.6f), AtmosKit.SoftDot(), true);
            srend.shadowCastingMode = ShadowCastingMode.Off;
            var sub = rainPS.subEmitters; sub.enabled = true;
            sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, 0.55f);
            var burst = splash.emission; burst.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });

            rainPS.Play();
        }

        void BuildSteam()
        {
            var t = new GameObject("Steam").transform; t.SetParent(root, false);
            var tex = AtmosKit.SoftDot();
            var mat = AtmosKit.Particles(new Color(0.65f, 0.65f, 0.75f, 0.10f), tex, false);
            var cover = AtmosKit.Lit(new Color(0.04f, 0.04f, 0.045f), 0.7f, 0.9f);
            foreach (var p in new[] { new Vector3(-1.3f, 0f, 17f), new Vector3(1.6f, 0f, 47f), new Vector3(0.5f, 0f, 85f) })
            {
                AtmosKit.Prim(PrimitiveType.Cylinder, "Manhole", t, p + Vector3.up * 0.005f, new Vector3(0.8f, 0.005f, 0.8f), cover);
                var go = new GameObject("SteamVent"); go.transform.SetParent(t, false); go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(-90, 0, 0);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var m = ps.main; m.startLifetime = 5f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); m.maxParticles = 120; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                var e = ps.emission; e.rateOverTime = 14f;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 12f; s.radius = 0.25f;
                var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.6f));
                var co = ps.colorOverLifetime; co.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
                co.color = g;
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(0.2f, 0.5f); v.y = new ParticleSystem.MinMaxCurve(0f, 0f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
                ps.Play();
            }
        }

        // ------------------------------------------------------------------ post + reflections
        void BuildVolume()
        {
            var go = new GameObject("CityVolume");
            go.transform.SetParent(root, false);
            cityVolume = go.AddComponent<Volume>();
            cityVolume.isGlobal = true; cityVolume.priority = 10f; cityVolume.weight = 1f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.85f); bloom.intensity.Override(2.6f); bloom.scatter.Override(0.78f);
            bloom.highQualityFiltering.Override(true);
            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.0f); ca.contrast.Override(22f); ca.saturation.Override(14f);
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(-8f); wb.tint.Override(6f);
            var st = p.Add<SplitToning>(true);
            st.shadows.Override(new Color(0.25f, 0.45f, 0.6f)); st.highlights.Override(new Color(0.75f, 0.5f, 0.35f)); st.balance.Override(-15f);
            var fg = p.Add<FilmGrain>(true); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.18f);
            cityVolume.profile = p;
        }

        void BuildReflectionProbe()
        {
            var go = new GameObject("StreetReflections");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 6f, 30f);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(14f, 30f, 80f);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.hdr = true;
            probe.intensity = 1.2f;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.farClipPlane = 500f;
            Invoke(nameof(RenderProbe), 0.3f);
        }

        void RenderProbe() { if (probe != null && InCity) probe.RenderProbe(); }

        // ------------------------------------------------------------------ runtime
        void LateUpdate()
        {
            if (!InCity) return;
            if (rainFollow != null && cam != null)
            {
                Vector3 p = cam.transform.position; p.y = 0f;
                rainFollow.position = p;
            }
            UpdateWalkSignals();
            bool on = Mathf.Repeat(Time.time, 1.6f) < 0.18f;
            foreach (var b in blinkers) if (b != null) b.sharedMaterial = on ? blinkOn : blinkOff;
            for (int i = 0; i < flickerLights.Count; i++)
                if (flickerLights[i] != null) flickerLights[i].enabled = Mathf.PerlinNoise(Time.time * 6f, i * 3.7f) > 0.28f;
            if (ledMat != null)
            {
                float h = Mathf.Repeat(Time.time * 0.03f, 1f);
                Color c = Color.HSVToRGB(h, 0.75f, 1f) * 1.6f;
                ledMat.SetColor("_BaseColor", c);
                if (billboardLight != null) billboardLight.color = Color.HSVToRGB(h, 0.6f, 1f);
            }
        }

        /// <summary>Called by the scenario when the player is pulled into the hospital.</summary>
        public void Leave()
        {
            InCity = false;
            if (rainPS != null) rainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var a in buzzes) if (a != null) a.Stop();
            if (cityVolume != null) cityVolume.weight = 0f;
            if (moon != null) moon.enabled = false;
            root.gameObject.SetActive(false);
        }
    }
}
