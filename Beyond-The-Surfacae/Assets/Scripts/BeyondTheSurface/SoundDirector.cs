using System.Collections;
using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>
    /// Music and ambience: rainy city bed + faint chill music on the street; when the ambulance appears the chill track
    /// fades out and the cinematic strings swell in. Entering the hospital silences the city; the strings come back softly
    /// (gentle ending section) once the father's name is revealed.
    /// </summary>
    public class SoundDirector : MonoBehaviour
    {
        public AudioClip cityRain;
        public AudioClip chillMusic;
        public AudioClip dramaticMusic;
        public AudioClip distantSiren;

        [Range(0, 1)] public float rainVolume = 0.55f;
        [Range(0, 1)] public float chillVolume = 0.16f;
        [Range(0, 1)] public float dramaVolume = 0.42f;
        [Tooltip("Where the strings start when the ambulance appears (seconds) — the louder part of the piece")]
        public float dramaStart = 24f;
        [Tooltip("Where the strings start in the hospital — the quiet ending")]
        public float hospitalStart = 66f;

        AudioSource rain, chill, drama, siren;
        AmbulanceEvent ambulance;
        bool ambulanceHandled, inHospital;

        AudioSource Make(string n, AudioClip c, bool loop)
        {
            var g = new GameObject(n);
            g.transform.SetParent(transform, false);
            var a = g.AddComponent<AudioSource>();
            a.clip = c; a.loop = loop; a.playOnAwake = false; a.spatialBlend = 0f; a.volume = 0f;
            return a;
        }

        void Start()
        {
            ambulance = GetComponent<AmbulanceEvent>();
            if (ambulance == null) ambulance = FindFirstObjectByType<AmbulanceEvent>();
            rain = Make("Ambience_Rain", cityRain, true);
            chill = Make("Music_Chill", chillMusic, true);
            drama = Make("Music_Drama", dramaticMusic, false);
            if (rain.clip != null) { rain.time = Random.Range(0f, rain.clip.length * 0.5f); rain.Play(); StartCoroutine(Fade(rain, rainVolume, 2.5f)); }
            if (chill.clip != null) { chill.Play(); StartCoroutine(Fade(chill, chillVolume, 4f)); }
            if (distantSiren != null) StartCoroutine(DistantSiren());
        }

        IEnumerator DistantSiren()
        {
            // one far-away siren early on, muffled behind the buildings: foreshadowing
            yield return new WaitForSeconds(7f);
            if (ambulanceHandled || inHospital || (ambulance != null && ambulance.Triggered)) yield break;
            var g = new GameObject("DistantSiren");
            g.transform.SetParent(transform, false);
            g.transform.position = new Vector3(-140f, 6f, 120f);
            siren = g.AddComponent<AudioSource>();
            siren.clip = distantSiren; siren.spatialBlend = 1f; siren.rolloffMode = AudioRolloffMode.Linear;
            siren.minDistance = 20f; siren.maxDistance = 400f; siren.volume = 0f; siren.dopplerLevel = 0f;
            var lp = g.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 900f;
            siren.Play();
            yield return Fade(siren, 0.5f, 3f);
            yield return new WaitForSeconds(5f);
            yield return Fade(siren, 0f, 4f);
            siren.Stop();
        }

        void Update()
        {
            if (!ambulanceHandled && ambulance != null && (ambulance.InStreet || ambulance.Parked))
            {
                ambulanceHandled = true;
                StartCoroutine(FadeStop(chill, 1.6f));
                if (siren != null) StartCoroutine(FadeStop(siren, 0.5f));
                if (drama.clip != null)
                {
                    drama.time = Mathf.Min(dramaStart, drama.clip.length - 1f);
                    drama.Play();
                    StartCoroutine(Fade(drama, dramaVolume, 2.2f));
                }
                StartCoroutine(Fade(rain, rainVolume * 0.7f, 2f));
            }
        }

        /// <summary>The fade to black into the hospital: the whole city goes quiet.</summary>
        public void EnterHospital(float fadeSeconds)
        {
            inHospital = true;
            StartCoroutine(FadeStop(rain, fadeSeconds));
            StartCoroutine(FadeStop(chill, fadeSeconds));
            StartCoroutine(FadeStop(drama, fadeSeconds));
            if (siren != null) StartCoroutine(FadeStop(siren, fadeSeconds));
        }

        /// <summary>Soft strings under the name reveal.</summary>
        public void HospitalTheme()
        {
            if (drama.clip == null) return;
            StopAllCoroutines();
            drama.Stop();
            drama.time = Mathf.Min(hospitalStart, drama.clip.length - 1f);
            drama.volume = 0f;
            drama.Play();
            StartCoroutine(Fade(drama, dramaVolume * 0.7f, 4f));
        }

        IEnumerator Fade(AudioSource a, float to, float s)
        {
            if (a == null) yield break;
            float from = a.volume, t = 0f;
            while (t < s) { t += Time.deltaTime; a.volume = Mathf.Lerp(from, to, t / s); yield return null; }
            a.volume = to;
        }

        IEnumerator FadeStop(AudioSource a, float s)
        {
            if (a == null || !a.isPlaying) yield break;
            yield return Fade(a, 0f, s);
            a.Stop();
        }
    }
}
