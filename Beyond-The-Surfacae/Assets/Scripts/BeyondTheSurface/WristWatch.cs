using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace BeyondTheSurface
{
    /// <summary>
    /// Smartwatch worn on the left wrist: countdown timer, haptic pulses and ticking as time runs out.
    /// Also records frustration metrics (looking at the watch, fast angry hand gestures).
    /// </summary>
    public class WristWatch : MonoBehaviour
    {
        [Header("Placement relative to the left controller (tweak in headset)")]
        public Vector3 localPosition = new Vector3(0f, 0.0f, -0.085f);
        public Vector3 localEuler = new Vector3(-15f, 0f, 0f);

        public float Remaining { get; private set; }
        public bool Running { get; private set; }
        public event Action Expired;

        Transform watch;
        Renderer screenRenderer;
        TextMeshPro label;
        AudioSource audioSrc;
        HapticImpulsePlayer haptics;
        Transform leftCtrl, rightCtrl;
        Camera cam;
        int lastWholeSecond = -1;
        Vector3 lastL, lastR;
        float gestureCooldown;
        Color baseColor = new Color(1f, 0.55f, 0.15f);

        public static Transform FindByName(string n)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == n && t.gameObject.scene.IsValid()) return t;
            return null;
        }

        void Start()
        {
            cam = Camera.main;
            leftCtrl = FindByName("Left Controller");
            rightCtrl = FindByName("Right Controller");
            var w = GameObject.Find("Watch");
            if (w != null) watch = w.transform;
            if (watch == null || leftCtrl == null) { Debug.LogWarning("[BTS] Watch or Left Controller not found."); return; }

            watch.SetParent(leftCtrl, false);
            watch.localPosition = localPosition;
            watch.localRotation = Quaternion.Euler(localEuler);
            watch.localScale = Vector3.one;
            haptics = leftCtrl.GetComponent<HapticImpulsePlayer>();

            foreach (var r in watch.GetComponentsInChildren<Renderer>())
                if (r.name == "Watch_Screen") screenRenderer = r;

            Vector3 center = screenRenderer != null ? screenRenderer.bounds.center : watch.position + watch.up * 0.044f;
            label = TextFactory.Create("WatchLabel", "00:00", null, center + watch.up * 0.002f,
                watch.rotation * Quaternion.Euler(90f, 0f, 0f), new Vector2(0.036f, 0.018f), baseColor,
                TextAlignmentOptions.Center, true, 10f);
            label.transform.SetParent(watch, true);
            label.fontStyle = FontStyles.Bold;

            audioSrc = watch.gameObject.AddComponent<AudioSource>();
            audioSrc.spatialBlend = 1f; audioSrc.volume = 0.5f; audioSrc.playOnAwake = false;
            Show(0);
        }

        public void StartCountdown(float seconds)
        {
            Remaining = seconds; Running = true; lastWholeSecond = Mathf.CeilToInt(seconds);
            Show(Remaining);
        }

        void Show(float t)
        {
            if (label == null) return;
            int s = Mathf.Max(0, Mathf.CeilToInt(t));
            label.text = string.Format("{0:00}:{1:00}", s / 60, s % 60);
        }

        void Update()
        {
            TrackFrustration();
            if (!Running) return;

            Remaining -= Time.deltaTime;
            int whole = Mathf.Max(0, Mathf.CeilToInt(Remaining));
            if (whole != lastWholeSecond)
            {
                lastWholeSecond = whole;
                if (audioSrc != null) audioSrc.PlayOneShot(ProceduralAudio.Tick(), 0.7f);
                if (Remaining < 10f && haptics != null)
                    haptics.SendHapticImpulse(Mathf.Lerp(0.25f, 0.9f, 1f - Mathf.Clamp01(Remaining / 10f)), 0.12f);
            }
            if (label != null)
            {
                Show(Remaining);
                bool urgent = Remaining < 10f;
                float pulse = urgent ? (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f))) : 1f;
                label.color = (urgent ? new Color(1f, 0.1f, 0.1f) : baseColor) * pulse;
            }
            if (Remaining <= 0f)
            {
                Remaining = 0f; Running = false; Show(0);
                if (label != null) label.color = Color.red;
                if (haptics != null) haptics.SendHapticImpulse(1f, 0.6f);
                Expired?.Invoke();
            }
        }

        void TrackFrustration()
        {
            var gm = GameManager.Instance;
            if (gm == null || cam == null || !Running) return;

            // looking at the watch
            if (watch != null)
            {
                Vector3 to = watch.position - cam.transform.position;
                if (to.magnitude < 0.8f && Vector3.Angle(cam.transform.forward, to) < 30f)
                    gm.watchLookSeconds += Time.deltaTime;
            }
            // fast, agitated hand movement
            gestureCooldown -= Time.deltaTime;
            if (leftCtrl != null && rightCtrl != null && Time.deltaTime > 0f)
            {
                float vl = (leftCtrl.position - lastL).magnitude / Time.deltaTime;
                float vr = (rightCtrl.position - lastR).magnitude / Time.deltaTime;
                if ((vl > 3f || vr > 3f) && gestureCooldown <= 0f && lastL != Vector3.zero)
                {
                    gm.angryGestures++; gestureCooldown = 0.7f;
                }
                lastL = leftCtrl.position; lastR = rightCtrl.position;
            }
        }
    }
}
