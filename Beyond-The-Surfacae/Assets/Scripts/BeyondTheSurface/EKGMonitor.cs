using TMPro;
using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>
    /// Draws a scrolling PQRST heartbeat trace in front of the monitor screen model and beeps on every R peak.
    /// The screen faces +Z (towards the player standing at the hospital spawn point).
    /// </summary>
    public class EKGMonitor : MonoBehaviour
    {
        public float bpm = 138f;
        public int samples = 140;
        public float sweepSeconds = 3.2f;

        public bool Active { get; private set; }

        LineRenderer line;
        AudioSource beep;
        TextMeshPro bpmText, spo2Text;
        Vector3 center; Vector2 size;
        int lastBeat = -1;

        static float G(float x, float mu, float w, float a) { float d = (x - mu) / w; return a * Mathf.Exp(-d * d); }

        static float Wave(float p)
        {
            return G(p, 0.12f, 0.035f, 0.14f)      // P
                 + G(p, 0.27f, 0.010f, -0.12f)     // Q
                 + G(p, 0.30f, 0.012f, 1.00f)      // R
                 + G(p, 0.33f, 0.012f, -0.28f)     // S
                 + G(p, 0.56f, 0.050f, 0.26f);     // T
        }

        public bool Build()
        {
            Renderer screen = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.name == "Hospital_EKG_Screen") { screen = r; break; }
            if (screen == null) { Debug.LogWarning("[BTS] EKG screen not found."); return false; }

            center = screen.bounds.center;
            size = new Vector2(screen.bounds.size.x, screen.bounds.size.y);
            float zFront = screen.bounds.max.z + 0.004f;

            var go = new GameObject("EKG_Trace");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = samples;
            line.widthMultiplier = 0.006f;
            line.numCapVertices = 2;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(0.2f, 1f, 0.35f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;

            beep = go.AddComponent<AudioSource>();
            beep.clip = ProceduralAudio.Beep(); beep.spatialBlend = 1f; beep.volume = 0.8f;
            beep.minDistance = 2f; beep.maxDistance = 25f; beep.playOnAwake = false;

            var faceRot = Quaternion.Euler(0f, 180f, 0f);
            bpmText = TextFactory.Create("EKG_BPM", "--", null,
                new Vector3(center.x + size.x * 0.28f, center.y + size.y * 0.30f, zFront), faceRot,
                new Vector2(size.x * 0.40f, size.y * 0.30f), new Color(0.3f, 1f, 0.4f), TextAlignmentOptions.Right, true);
            spo2Text = TextFactory.Create("EKG_SpO2", "SpO2 --", null,
                new Vector3(center.x - size.x * 0.22f, center.y + size.y * 0.32f, zFront), faceRot,
                new Vector2(size.x * 0.45f, size.y * 0.22f), new Color(0.4f, 0.9f, 1f), TextAlignmentOptions.Left, true);
            bpmText.gameObject.SetActive(false); spo2Text.gameObject.SetActive(false);
            return true;
        }

        public void Begin()
        {
            if (line == null && !Build()) return;
            Active = true; line.enabled = true;
            bpmText.gameObject.SetActive(true); spo2Text.gameObject.SetActive(true);
            bpmText.text = Mathf.RoundToInt(bpm) + "\n<size=40%>BPM</size>";
            spo2Text.text = "SpO2 86%";
            lastBeat = -1;
        }

        void Update()
        {
            if (!Active || line == null) return;
            float zFront = center.z + 0.0f;
            float zLine = FindZ();
            float baseY = center.y - size.y * 0.18f;
            float amp = size.y * 0.42f;
            float hz = bpm / 60f;
            for (int i = 0; i < samples; i++)
            {
                float tt = Time.time - (samples - 1 - i) * (sweepSeconds / samples);
                float phase = Mathf.Repeat(tt * hz, 1f);
                float x = center.x + size.x * 0.46f - size.x * 0.92f * ((float)i / (samples - 1)); // world +X is to the player's left
                line.SetPosition(i, new Vector3(x, baseY + amp * Wave(phase), zLine));
            }
            int beat = Mathf.FloorToInt(Time.time * hz - 0.30f);
            if (beat != lastBeat) { if (lastBeat >= 0) beep.Play(); lastBeat = beat; }
        }

        float _z = float.NaN;
        float FindZ()
        {
            if (float.IsNaN(_z)) _z = center.z + 0.012f;
            return _z;
        }

        public void SetBpm(float v) { bpm = v; if (bpmText != null) bpmText.text = Mathf.RoundToInt(v) + "\n<size=40%>BPM</size>"; }
    }
}
