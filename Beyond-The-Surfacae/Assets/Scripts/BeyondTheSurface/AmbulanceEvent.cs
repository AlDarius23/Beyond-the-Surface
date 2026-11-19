using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>
    /// The ambulance arrives from the far cross street with its siren on, turns into the street and overtakes the southbound
    /// jam in the oncoming lane (traffic pulls over for it). Just past the crossing two cars have crashed across both lanes,
    /// so it ends up stuck on the pedestrian crossing - in traffic - creeping and honking.
    /// The model's front faces its local -Z.
    /// </summary>
    public class AmbulanceEvent : MonoBehaviour
    {
        public float crosswalkZ = 30f;
        [Tooltip("Real siren recording; falls back to the generated one if empty")]
        public AudioClip sirenClip;

        [Header("Route")]
        public float startX = -50f;
        public float crossLaneZ = 64.3f;
        public float streetX = 0.6f;
        public float turnRadius = 5f;
        [Tooltip("Where the ambulance's centre ends up, on the crossing")]
        public float stuckZ = 30.6f;
        public float stuckX = 1.0f;
        [Tooltip("Nose turned towards the jammed lane, trying to squeeze back in")]
        public float stuckYawOffset = 10f;
        public float cruiseSpeed = 16f;
        public float startSpeed = 13f;
        public float lateralAccel = 5.5f;
        public float brakeDecel = 5.5f;

        public bool Triggered { get; private set; }
        public bool Parked { get; private set; }
        public bool InStreet { get; private set; }
        public Transform Body { get { return amb; } }
        public Vector3 Heading { get; private set; }
        public event Action OnParked;

        Transform amb;
        AudioSource sirenSrc, sfxSrc;
        Light redLight, blueLight;
        Renderer[] reds, blues;
        Material redOn, redOff, blueOn, blueOff;
        bool sirenOn;
        GameObject blocker;
        readonly List<Transform> wheels = new List<Transform>();
        AudioClip horn;

        static Material Lit(Color baseC, Color emission)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh);
            m.color = baseC;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseC);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        void Start()
        {
            var go = GameObject.Find("Ambulance");
            if (go == null) { Debug.LogWarning("[BTS] Ambulance not found."); return; }
            amb = go.transform;
            amb.position = new Vector3(startX, -50f, crossLaneZ); // hidden until it sets off
            Heading = Vector3.right;

            var rl = new List<Renderer>();
            var bl = new List<Renderer>();
            foreach (var r in amb.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("LightRed")) rl.Add(r);
                else if (r.name.Contains("LightBlue")) bl.Add(r);
                if (r.name.Contains("Wheel")) wheels.Add(r.transform);
                if (r.name.Contains("Headlight")) r.sharedMaterial = Lit(new Color(0.9f, 0.9f, 0.85f), new Color(1f, 0.95f, 0.85f) * 5f);
            }
            reds = rl.ToArray(); blues = bl.ToArray();
            redOn = Lit(new Color(0.4f, 0f, 0f), new Color(1f, 0.05f, 0.05f) * 6f);
            redOff = Lit(new Color(0.25f, 0f, 0f), Color.black);
            blueOn = Lit(new Color(0f, 0f, 0.4f), new Color(0.1f, 0.3f, 1f) * 6f);
            blueOff = Lit(new Color(0f, 0f, 0.25f), Color.black);
            SetLights(false, false);

            sirenSrc = amb.gameObject.AddComponent<AudioSource>();
            sirenSrc.clip = sirenClip != null ? sirenClip : ProceduralAudio.Siren(); sirenSrc.loop = true; sirenSrc.spatialBlend = 1f;
            sirenSrc.minDistance = 10f; sirenSrc.maxDistance = 260f; sirenSrc.rolloffMode = AudioRolloffMode.Logarithmic; sirenSrc.dopplerLevel = 0.6f;
            sirenSrc.playOnAwake = false;
            sfxSrc = amb.gameObject.AddComponent<AudioSource>();
            sfxSrc.spatialBlend = 1f; sfxSrc.minDistance = 6f; sfxSrc.maxDistance = 120f; sfxSrc.playOnAwake = false;
            horn = MakeAmbulanceHorn();

            redLight = NewLight("AmbRedLight", new Color(1f, 0.1f, 0.1f), 0.6f);
            blueLight = NewLight("AmbBlueLight", new Color(0.15f, 0.3f, 1f), -0.6f);

            // closes the road at the crossing (railings close the sidewalk edges)
            blocker = new GameObject("AmbulanceBlocker");
            blocker.transform.position = new Vector3(0f, 2f, crosswalkZ);
            var bc = blocker.AddComponent<BoxCollider>();
            bc.size = new Vector3(7.0f, 4f, 7.0f);
            blocker.SetActive(false);
        }

        Light NewLight(string n, Color c, float x)
        {
            var g = new GameObject(n);
            g.transform.SetParent(amb, false);
            g.transform.localPosition = new Vector3(x, 3.0f, 0f);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = 16f; l.intensity = 0f;
            return l;
        }

        void SetLights(bool red, bool blue)
        {
            foreach (var r in reds) r.sharedMaterial = red ? redOn : redOff;
            foreach (var r in blues) r.sharedMaterial = blue ? blueOn : blueOff;
            if (redLight != null) redLight.intensity = red ? 14f : 0f;
            if (blueLight != null) blueLight.intensity = blue ? 14f : 0f;
        }

        void Update()
        {
            if (!sirenOn) return;
            // double-flash pattern, alternating red / blue
            float p = Mathf.Repeat(Time.time * 1.6f, 1f);
            bool redPhase = p < 0.5f;
            float q = Mathf.Repeat(p * 2f, 1f);
            bool flash = q < 0.18f || (q > 0.3f && q < 0.48f);
            SetLights(redPhase && flash, !redPhase && flash);
        }

        public void Trigger()
        {
            if (Triggered || amb == null) return;
            Triggered = true;
            StartCoroutine(Drive());
        }

        // ---------------------------------------------------------------- route
        List<Vector3> BuildRoute()
        {
            var pts = new List<Vector3>();
            float xc = streetX - turnRadius;                // turn centre x
            float zc = crossLaneZ - turnRadius;             // turn centre z
            for (float x = startX; x < xc; x += 1f) pts.Add(new Vector3(x, 0, crossLaneZ));
            for (int i = 0; i <= 16; i++)                   // right turn: heading +X -> heading -Z
            {
                float a = Mathf.PI * 0.5f * i / 16f;
                pts.Add(new Vector3(xc + Mathf.Sin(a) * turnRadius, 0, zc + Mathf.Cos(a) * turnRadius));
            }
            for (float z = zc - 1f; z > stuckZ + 9f; z -= 1f) pts.Add(new Vector3(streetX, 0, z));
            for (int i = 1; i <= 9; i++)                    // ease towards the final spot on the crossing
            {
                float k = Mathf.SmoothStep(0f, 1f, i / 9f);
                pts.Add(new Vector3(Mathf.Lerp(streetX, stuckX, k), 0, Mathf.Lerp(stuckZ + 9f, stuckZ, i / 9f)));
            }
            return pts;
        }

        IEnumerator Drive()
        {
            var pts = BuildRoute();
            int n = pts.Count;
            var cum = new float[n];
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            float total = cum[n - 1];

            // speed limit from curvature, then a backward pass so it can brake for the turn and for the stop
            var vmax = new float[n];
            for (int i = 0; i < n; i++)
            {
                float k = 0f;
                if (i > 0 && i < n - 1)
                {
                    Vector3 a = (pts[i] - pts[i - 1]).normalized, b = (pts[i + 1] - pts[i]).normalized;
                    float ang = Vector3.Angle(a, b) * Mathf.Deg2Rad;
                    float ds = 0.5f * (Vector3.Distance(pts[i - 1], pts[i]) + Vector3.Distance(pts[i], pts[i + 1]));
                    k = ang / Mathf.Max(0.01f, ds);
                }
                vmax[i] = k > 1e-4f ? Mathf.Min(cruiseSpeed, Mathf.Sqrt(lateralAccel / k)) : cruiseSpeed;
            }
            vmax[n - 1] = 0f;
            for (int i = n - 2; i >= 0; i--)
                vmax[i] = Mathf.Min(vmax[i], Mathf.Sqrt(vmax[i + 1] * vmax[i + 1] + 2f * brakeDecel * (cum[i + 1] - cum[i])));

            amb.position = pts[0];
            amb.rotation = Quaternion.LookRotation(-(pts[1] - pts[0]).normalized);
            sirenOn = true;
            sirenSrc.volume = 1f; sirenSrc.pitch = 1f; sirenSrc.Play();

            float s = 0f, v = startSpeed, prevV = v;
            float pitch = 0f, roll = 0f;
            int seg = 0;
            bool screeched = false;
            while (s < total - 0.02f)
            {
                float dt = Time.deltaTime;
                while (seg < n - 2 && cum[seg + 1] < s) seg++;
                float segT = Mathf.InverseLerp(cum[seg], cum[seg + 1], s);
                float limit = Mathf.Lerp(vmax[seg], vmax[seg + 1], segT);
                float accel = v < limit ? 2.6f : -brakeDecel * 1.2f;
                v = Mathf.Clamp(v + accel * dt, 0.4f, Mathf.Max(limit, 0.4f));
                s = Mathf.Min(total, s + Mathf.Max(v, 0.4f) * dt);

                Vector3 pos = Vector3.Lerp(pts[seg], pts[seg + 1], segT);
                Vector3 dir = (pts[Mathf.Min(seg + 2, n - 1)] - pts[seg]).normalized;
                Heading = dir;
                InStreet = pos.z < crossLaneZ - turnRadius + 0.5f;

                // body dynamics: nose dips when braking, leans out of the turn
                float longA = (v - prevV) / Mathf.Max(dt, 1e-4f); prevV = v;
                float k = 0f;
                if (seg > 0) k = Vector3.SignedAngle(pts[seg] - pts[seg - 1], pts[seg + 1] - pts[seg], Vector3.up) * Mathf.Deg2Rad / Mathf.Max(0.01f, cum[seg + 1] - cum[seg]);
                pitch = Mathf.Lerp(pitch, Mathf.Clamp(longA * 0.45f, -2.8f, 1.5f), dt * 6f);
                roll = Mathf.Lerp(roll, Mathf.Clamp(v * v * k * 0.45f, -3f, 3f), dt * 5f);

                float remaining = total - s;
                float yawOff = Mathf.Lerp(stuckYawOffset, 0f, Mathf.Clamp01(remaining / 8f));
                Quaternion baseRot = Quaternion.LookRotation(-dir) * Quaternion.Euler(0f, -yawOff, 0f);
                amb.position = pos;
                amb.rotation = baseRot * Quaternion.Euler(pitch, 0f, roll);
                SpinWheels(v * dt);

                if (InStreet && blocker != null && !blocker.activeSelf && pos.z < crosswalkZ + 22f) blocker.SetActive(true);
                if (!screeched && remaining < 11f && v > 6f)
                {
                    screeched = true;
                    sfxSrc.PlayOneShot(ProceduralAudio.Screech(), 0.6f);
                    sfxSrc.PlayOneShot(horn, 0.9f);
                }
                yield return null;
            }

            // settle: the nose rises back after the stop
            float t = 0f;
            Quaternion settled = Quaternion.LookRotation(-Heading) * Quaternion.Euler(0f, -stuckYawOffset, 0f);
            Quaternion from = amb.rotation;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float w = Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI) * 0.6f;
                amb.rotation = Quaternion.Slerp(from, settled, t / 0.6f) * Quaternion.Euler(-w, 0, 0);
                yield return null;
            }
            amb.rotation = settled;
            if (blocker != null) blocker.SetActive(true);
            Parked = true;
            OnParked?.Invoke();
            StartCoroutine(Stuck());
        }

        void SpinWheels(float dist)
        {
            if (amb == null) return;
            float deg = dist / 0.42f * Mathf.Rad2Deg;
            foreach (var w in wheels) w.RotateAround(w.position, amb.right, deg);
        }

        // stuck in traffic: occasionally inches forward and leans on the horn
        IEnumerator Stuck()
        {
            int creeps = 0;
            while (sirenOn)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(3.5f, 6.5f));
                if (!sirenOn) yield break;
                sfxSrc.PlayOneShot(horn, 1f);
                if (creeps < 2)
                {
                    creeps++;
                    Vector3 fwd = -amb.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 p0 = amb.position, p1 = p0 + fwd * 0.3f;
                    float t = 0f;
                    while (t < 0.7f)
                    {
                        t += Time.deltaTime;
                        amb.position = Vector3.Lerp(p0, p1, Mathf.SmoothStep(0, 1, t / 0.7f));
                        SpinWheels(0.3f * Time.deltaTime / 0.7f);
                        yield return null;
                    }
                }
            }
        }

        /// <summary>The crash has locked the street: close the road at the crossing.</summary>
        public void CloseCrossing()
        {
            if (blocker != null) blocker.SetActive(true);
        }

        public void StopSiren(float fadeSeconds = 1f)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(FadeSiren(fadeSeconds));
        }

        IEnumerator FadeSiren(float s)
        {
            float t = 0f;
            while (t < s && sirenSrc != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / s);
                sirenSrc.volume = 1f - k;
                sirenSrc.pitch = Mathf.Lerp(1f, 0.4f, k); // siren sags as time "freezes"
                yield return null;
            }
            if (sirenSrc != null) sirenSrc.Stop();
            sirenOn = false;
            SetLights(false, false);
        }

        static AudioClip MakeAmbulanceHorn()
        {
            const int sr = 44100; float dur = 0.9f;
            int n = (int)(sr * dur);
            var d = new float[n];
            float p1 = 0, p2 = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                p1 += 330f / sr; p2 += 392f / sr;
                float s = (Mathf.Repeat(p1, 1f) * 2f - 1f) + (Mathf.Repeat(p2, 1f) * 2f - 1f);
                lp += (s - lp) * 0.15f;
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((dur - t) / 0.1f);
                d[i] = Mathf.Clamp(lp * 0.45f * env, -1f, 1f);
            }
            var c = AudioClip.Create("AmbulanceHorn", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
