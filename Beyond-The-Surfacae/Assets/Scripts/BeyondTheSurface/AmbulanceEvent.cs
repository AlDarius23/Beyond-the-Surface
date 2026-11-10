using System;
using System.Collections;
using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>
    /// Scripted event: an ambulance with siren drives down the street, brakes hard and parks broadside
    /// across the pedestrian crossing, blocking the player's way.
    /// </summary>
    public class AmbulanceEvent : MonoBehaviour
    {
        public float crosswalkZ = 30f;
        public Vector3 startPosition = new Vector3(1.6f, 0f, 72f);
        public Vector3 parkedPosition = new Vector3(0.3f, 0f, 30f);
        public float parkedYaw = 90f;
        public float speed = 17f;

        public bool Triggered { get; private set; }
        public bool Parked { get; private set; }
        public event Action OnParked;

        Transform amb;
        AudioSource sirenSrc, sfxSrc;
        Light redLight, blueLight;
        Renderer[] reds, blues;
        Material redOn, redOff, blueOn, blueOff;
        bool sirenOn;
        GameObject blocker;

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
            amb.position = startPosition + Vector3.up * -50f; // hidden below ground until triggered
            amb.rotation = Quaternion.identity;

            var rl = new System.Collections.Generic.List<Renderer>();
            var bl = new System.Collections.Generic.List<Renderer>();
            foreach (var r in amb.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("LightRed")) rl.Add(r);
                else if (r.name.Contains("LightBlue")) bl.Add(r);
            }
            reds = rl.ToArray(); blues = bl.ToArray();
            redOn = Lit(new Color(0.4f, 0f, 0f), new Color(1f, 0.05f, 0.05f) * 5f);
            redOff = Lit(new Color(0.25f, 0f, 0f), Color.black);
            blueOn = Lit(new Color(0f, 0f, 0.4f), new Color(0.1f, 0.3f, 1f) * 5f);
            blueOff = Lit(new Color(0f, 0f, 0.25f), Color.black);
            SetLights(false, false);

            sirenSrc = amb.gameObject.AddComponent<AudioSource>();
            sirenSrc.clip = ProceduralAudio.Siren(); sirenSrc.loop = true; sirenSrc.spatialBlend = 1f;
            sirenSrc.minDistance = 6f; sirenSrc.maxDistance = 120f; sirenSrc.rolloffMode = AudioRolloffMode.Linear;
            sirenSrc.playOnAwake = false;
            sfxSrc = amb.gameObject.AddComponent<AudioSource>();
            sfxSrc.spatialBlend = 1f; sfxSrc.minDistance = 6f; sfxSrc.maxDistance = 120f; sfxSrc.playOnAwake = false;

            redLight = NewLight("AmbRedLight", new Color(1f, 0.1f, 0.1f));
            blueLight = NewLight("AmbBlueLight", new Color(0.15f, 0.3f, 1f));

            blocker = new GameObject("AmbulanceBlocker");
            blocker.transform.position = new Vector3(0f, 3f, crosswalkZ);
            var bc = blocker.AddComponent<BoxCollider>();
            bc.size = new Vector3(7.2f, 6f, 6.4f);
            blocker.SetActive(false);
        }

        Light NewLight(string n, Color c)
        {
            var g = new GameObject(n);
            g.transform.SetParent(amb, false);
            g.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = 18f; l.intensity = 0f;
            return l;
        }

        void SetLights(bool red, bool blue)
        {
            foreach (var r in reds) r.sharedMaterial = red ? redOn : redOff;
            foreach (var r in blues) r.sharedMaterial = blue ? blueOn : blueOff;
            if (redLight != null) redLight.intensity = red ? 40f : 0f;
            if (blueLight != null) blueLight.intensity = blue ? 40f : 0f;
        }

        void Update()
        {
            if (!sirenOn) return;
            bool phase = ((int)(Time.time * 5f)) % 2 == 0;
            SetLights(phase, !phase);
        }

        public void Trigger()
        {
            if (Triggered || amb == null) return;
            Triggered = true;
            StartCoroutine(Drive());
        }

        IEnumerator Drive()
        {
            amb.position = startPosition;
            amb.rotation = Quaternion.identity;
            sirenOn = true;
            sirenSrc.volume = 1f; sirenSrc.pitch = 1f; sirenSrc.Play();

            // straight approach along -Z
            float brakeStartZ = crosswalkZ + 15f;
            while (amb.position.z > brakeStartZ)
            {
                amb.position += Vector3.back * speed * Time.deltaTime;
                yield return null;
            }

            // hard brake + swing broadside across the crossing
            sfxSrc.PlayOneShot(ProceduralAudio.Screech(), 1f);
            Vector3 p0 = amb.position; Quaternion r0 = amb.rotation;
            Quaternion r1 = Quaternion.Euler(0f, parkedYaw, 0f);
            float t = 0f, dur = 1.7f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                amb.position = Vector3.Lerp(p0, parkedPosition, k);
                amb.rotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            amb.position = parkedPosition; amb.rotation = r1;
            if (blocker != null) blocker.SetActive(true);
            Parked = true;
            OnParked?.Invoke();
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
    }
}
