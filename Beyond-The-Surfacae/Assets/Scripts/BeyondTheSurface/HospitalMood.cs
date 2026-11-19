using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BeyondTheSurface
{
    /// <summary>
    /// Cold, isolating hospital lighting that contrasts with the warm neon city: one hard cold pool of light on the bed,
    /// the green glow of the EKG, striped moonlight through blinds, a dying fluorescent tube by the door, a strip of
    /// corridor light under the closed door, near-black ambient and desaturated, grainy post-processing.
    /// </summary>
    public class HospitalMood : MonoBehaviour
    {
        public Color keyColor = new Color(0.78f, 0.88f, 1f);
        public float keyIntensity = 9f;
        [Tooltip("Raise mattress + patient so the father is visible over the footboard from the door")]
        public float bedRaise = 0.24f;
        public Color moonColor = new Color(0.32f, 0.45f, 0.85f);
        public Color monitorColor = new Color(0.2f, 1f, 0.55f);

        Transform root;
        Volume volume;
        Light flicker;
        Renderer flickerPanel;
        Material panelOn, panelOff, panelDim;
        ReflectionProbe probe;
        bool active;
        float nextFlicker;

        static Renderer Find(string name)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.name == name) return r;
            return null;
        }

        public void Build()
        {
            if (root != null) return;
            var hosp = GameObject.Find("Hospital_ER");
            if (hosp == null) { Debug.LogWarning("[BTS] Hospital_ER not found."); return; }
            root = new GameObject("HospitalMood").transform;

            AddColliders(hosp.transform);
            Restyle();
            RaiseBed(hosp.transform);

            Vector3 bed = new Vector3(200f, 0.6f, -1.6f);
            var b = Find("Hospital_Bed_Mattress");
            if (b != null) bed = b.bounds.center;

            // key light: hard cold pool straight down onto the bed
            var key = AtmosKit.SpotLight("Hosp_Key", root, new Vector3(bed.x, 2.93f, bed.z + 0.1f), new Vector3(0, -1f, -0.08f),
                keyColor, keyIntensity, 5f, 58f, LightShadows.Soft);
            key.shadowStrength = 0.9f;
            key.spotAngle = 70f; key.innerSpotAngle = 30f;

            // narrow cold light on the chart so the name can be read
            var chart = Find("Hospital_ChartBoard");
            if (chart != null)
            {
                Vector3 cp = chart.bounds.center;
                var cl = AtmosKit.SpotLight("Hosp_ChartLight", root, new Vector3(cp.x, 2.9f, cp.z + 1.3f), (cp - new Vector3(cp.x, 2.9f, cp.z + 1.3f)).normalized,
                    keyColor, 2.2f, 4f, 28f);
                cl.innerSpotAngle = 12f;
            }

            // EKG monitor glow
            var ekg = Find("Hospital_EKG_Screen");
            if (ekg != null)
            {
                var em = AtmosKit.Emissive(new Color(0f, 0.04f, 0.02f), new Color(0.03f, 0.35f, 0.14f) * 1.4f, 0.9f);
                ekg.sharedMaterial = em;
                AtmosKit.PointLight("Hosp_MonitorGlow", root, ekg.bounds.center + new Vector3(0, 0, 0.35f), monitorColor, 1.4f, 2.6f);
            }

            // moonlight through blinds on the east wall, falling across the bed in stripes
            var wallE = Find("Hospital_Wall_E");
            float wx = wallE != null ? wallE.bounds.max.x : 196.5f;
            Vector3 win = new Vector3(wx + 0.01f, 2.0f, -1.3f);
            AtmosKit.Facade("Hosp_Window", root, win, Vector3.right, 1.3f, 1.2f, AtmosKit.Glow(new Color(0.12f, 0.18f, 0.35f) * 1.4f), 1, 1, Vector2.zero);
            var frameM = AtmosKit.Lit(new Color(0.5f, 0.52f, 0.55f), 0.5f);
            var slatM = AtmosKit.Lit(new Color(0.55f, 0.57f, 0.6f), 0.4f);
            AtmosKit.Box("Hosp_WindowFrame", root, win + new Vector3(0.03f, 0.63f, 0), new Vector3(0.06f, 0.06f, 1.42f), frameM);
            AtmosKit.Box("Hosp_WindowFrame", root, win + new Vector3(0.03f, -0.63f, 0), new Vector3(0.06f, 0.06f, 1.42f), frameM);
            for (int i = 0; i < 11; i++)
            {
                var s = AtmosKit.Box("Hosp_Slat", root, win + new Vector3(0.12f, -0.55f + i * 0.11f, 0), new Vector3(0.07f, 0.012f, 1.3f), slatM);
                s.transform.rotation = Quaternion.Euler(0, 0, 25f);
                s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            }
            // the light sits outside the wall (which doesn't cast shadows) so only the slats stripe it
            if (wallE != null) wallE.shadowCastingMode = ShadowCastingMode.Off;
            Vector3 mp = win + new Vector3(-0.7f, 0.45f, 0);
            var moon = AtmosKit.SpotLight("Hosp_Moon", root, mp, (bed + Vector3.down * 0.2f - mp).normalized,
                moonColor, 16f, 9f, 44f, LightShadows.Hard);
            moon.shadowStrength = 1f; moon.shadowNearPlane = 0.1f; moon.shadowBias = 0.02f; moon.shadowNormalBias = 0.2f;

            // ceiling panels: the two over the bed stay cold and dim, the ones by the door are dead, one of them dying
            panelOn = AtmosKit.Glow(new Color(0.75f, 0.85f, 1f) * 1.6f);
            panelDim = AtmosKit.Glow(new Color(0.55f, 0.65f, 0.75f) * 0.6f);
            panelOff = AtmosKit.Lit(new Color(0.12f, 0.13f, 0.14f), 0.6f);
            for (int i = 1; i <= 4; i++)
            {
                var p = Find("Hospital_CeilingLight_" + i);
                if (p == null) continue;
                bool overBed = p.bounds.center.z < 0f;
                p.sharedMaterial = overBed ? panelDim : panelOff;
                if (i == 1)
                {
                    flickerPanel = p;
                    flicker = AtmosKit.PointLight("Hosp_Flicker", root, p.bounds.center + Vector3.down * 0.3f, new Color(0.7f, 0.82f, 0.95f), 1.6f, 4.5f);
                }
            }

            // closed door with corridor light leaking under it and through the small window
            var dl = Find("Hospital_DoorFrame_L"); var dr = Find("Hospital_DoorFrame_R");
            if (dl != null && dr != null)
            {
                float x0 = Mathf.Min(dl.bounds.max.x, dr.bounds.max.x), x1 = Mathf.Max(dl.bounds.min.x, dr.bounds.min.x);
                float dz = dl.bounds.center.z;
                var doorM = AtmosKit.Lit(new Color(0.22f, 0.27f, 0.3f), 0.45f);
                var door = AtmosKit.Box("Hosp_Door", root, new Vector3((x0 + x1) * 0.5f, 1.05f, dz), new Vector3(x1 - x0, 2.06f, 0.06f), doorM, true);
                door.transform.position += Vector3.up * 0.015f;
                AtmosKit.Box("Hosp_DoorGap", root, new Vector3((x0 + x1) * 0.5f, 0.006f, dz - 0.04f), new Vector3(x1 - x0, 0.012f, 0.02f), AtmosKit.Glow(new Color(0.9f, 0.95f, 1f) * 3f));
                AtmosKit.Box("Hosp_DoorWindow", root, new Vector3((x0 + x1) * 0.5f, 1.55f, dz - 0.035f), new Vector3(0.28f, 0.42f, 0.01f), AtmosKit.Glow(new Color(0.7f, 0.8f, 0.9f) * 1.2f));
                var spill = AtmosKit.SpotLight("Hosp_DoorSpill", root, new Vector3((x0 + x1) * 0.5f, 0.05f, dz - 0.1f), new Vector3(0, -0.15f, -1f),
                    new Color(0.8f, 0.9f, 1f), 1.2f, 3.5f, 120f);
                spill.shadows = LightShadows.None;
            }

            BuildVolume();

            var pg = new GameObject("Hosp_Reflections"); pg.transform.SetParent(root, false);
            pg.transform.position = hosp.transform.position + Vector3.up * 1.5f;
            probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(7.4f, 3.3f, 6.4f); probe.boxProjection = true; probe.resolution = 128; probe.hdr = true;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor; probe.backgroundColor = Color.black;

            root.gameObject.SetActive(false);
        }

        void AddColliders(Transform hosp)
        {
            foreach (var r in hosp.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                bool solid = n.StartsWith("Hospital_Floor") || n.StartsWith("Hospital_Wall") || n == "Hospital_Bed_Base" ||
                             n == "Hospital_Bed_Mattress" || n == "Hospital_Bed_Footboard" || n == "Hospital_Bed_Headboard" ||
                             n == "Hospital_Cabinet" || n == "Hospital_CrashCart" || n == "Hospital_Curtain";
                if (solid && r.GetComponent<Collider>() == null) r.gameObject.AddComponent<BoxCollider>();
            }
        }

        void RaiseBed(Transform hosp)
        {
            Renderer baseR = null;
            foreach (Transform t in hosp)
            {
                string n = t.name;
                if (n == "Hospital_Bed_Base") baseR = t.GetComponent<Renderer>();
                if (n.StartsWith("Patient") || n == "Hospital_Bed_Mattress" || n == "Hospital_Bed_Blanket" || n == "Hospital_Bed_Pillow" ||
                    n == "Hospital_Bed_RailL" || n == "Hospital_Bed_RailR" || n == "Hospital_Bed_Headboard")
                    t.position += Vector3.up * bedRaise;
            }
            if (baseR != null && bedRaise > 0.01f)
            {
                // fill the gap between frame and raised mattress (lift column + side panel)
                var b = baseR.bounds;
                var fill = AtmosKit.Box("Hosp_BedLift", root, new Vector3(b.center.x, b.max.y + bedRaise * 0.5f, b.center.z),
                    new Vector3(b.size.x * 0.96f, bedRaise + 0.02f, b.size.z * 0.96f), baseR.sharedMaterial, true);
                fill.transform.SetParent(hosp, true);
            }
        }


        void Restyle()
        {
            // colder, greener, more clinical surfaces; polished floor catches the light pool
            Tint("Hospital_Wall", new Color(0.55f, 0.63f, 0.64f), 0.35f);
            Tint("Hospital_Floor", new Color(0.24f, 0.30f, 0.31f), 0.82f);
            Tint("Hospital_Ceiling", new Color(0.45f, 0.5f, 0.52f), 0.3f);
            Tint("Hospital_Curtain", new Color(0.22f, 0.38f, 0.42f), 0.25f);
        }

        void Tint(string prefix, Color c, float smooth)
        {
            var mats = new Dictionary<Material, Material>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith(prefix)) continue;
                var src = r.sharedMaterial;
                if (src == null) continue;
                Material m;
                if (!mats.TryGetValue(src, out m))
                {
                    m = new Material(src);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                    mats[src] = m;
                }
                r.sharedMaterial = m;
            }
        }

        void BuildVolume()
        {
            var go = new GameObject("HospitalVolume");
            go.transform.SetParent(root, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 20f; volume.weight = 1f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true); bloom.threshold.Override(1f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.6f);
            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f); ca.contrast.Override(28f); ca.saturation.Override(-45f);
            ca.colorFilter.Override(new Color(0.86f, 0.95f, 1f));
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(-28f); wb.tint.Override(-6f);
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.85f, 0.95f, 1.15f, -0.08f));
            var st = p.Add<SplitToning>(true); st.shadows.Override(new Color(0.5f, 0.5f, 0.5f)); st.highlights.Override(new Color(0.5f, 0.5f, 0.5f));
            var fg = p.Add<FilmGrain>(true); fg.type.Override(FilmGrainLookup.Medium3); fg.intensity.Override(0.38f);
            volume.profile = p;
        }

        /// <summary>Switch the world lighting to the hospital.</summary>
        public void Enter()
        {
            Build();
            if (root == null) return;
            root.gameObject.SetActive(true);
            active = true;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.015f, 0.02f, 0.025f);
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.05f, 0.06f);
            RenderSettings.reflectionIntensity = 0.5f;
            Invoke(nameof(RenderProbe), 0.2f);

            // brighter, colder EKG trace that blooms
            var trace = GameObject.Find("EKG_Trace");
            if (trace != null)
            {
                var lr = trace.GetComponent<LineRenderer>();
                if (lr != null) { lr.material = AtmosKit.Glow(new Color(0.25f, 1f, 0.45f) * 4f); lr.startColor = lr.endColor = Color.white; }
            }
        }

        void RenderProbe() { if (probe != null) probe.RenderProbe(); }

        void Update()
        {
            if (!active || flicker == null) return;
            // a dying fluorescent tube: mostly off, sputters every few seconds
            if (Time.time > nextFlicker)
            {
                bool burst = Random.value < 0.5f;
                nextFlicker = Time.time + (burst ? Random.Range(0.03f, 0.12f) : Random.Range(1.5f, 5f));
                bool on = burst && Random.value < 0.6f;
                flicker.enabled = on;
                if (flickerPanel != null) flickerPanel.sharedMaterial = on ? panelOn : panelOff;
            }
        }
    }
}
