using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>
    /// Placeholder sound effects generated in code (siren, EKG beep, clock tick, tyre screech),
    /// so the prototype works without audio assets. Replace with Freesound / recorded clips later.
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050;
        static AudioClip _siren, _beep, _tick, _screech;

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = f((float)i / Rate);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip Siren()
        {
            if (_siren != null) return _siren;
            double phase = 0;
            _siren = Make("siren", 2f, t =>
            {
                float f = 650f + 320f * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t));
                phase += 2.0 * System.Math.PI * f / Rate;
                float edge = Mathf.Clamp01(Mathf.Min(t, 2f - t) / 0.01f);
                return edge * (0.30f * (float)System.Math.Sin(phase) + 0.08f * (float)System.Math.Sin(2 * phase));
            });
            return _siren;
        }

        public static AudioClip Beep()
        {
            if (_beep != null) return _beep;
            _beep = Make("ekg_beep", 0.12f, t =>
            {
                float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 18f);
                return 0.5f * env * Mathf.Sin(2f * Mathf.PI * 980f * t);
            });
            return _beep;
        }

        public static AudioClip Tick()
        {
            if (_tick != null) return _tick;
            _tick = Make("tick", 0.03f, t =>
            {
                float env = Mathf.Exp(-t * 160f);
                return 0.6f * env * Mathf.Sin(2f * Mathf.PI * 2400f * t);
            });
            return _tick;
        }

        public static AudioClip Screech()
        {
            if (_screech != null) return _screech;
            var rng = new System.Random(7);
            _screech = Make("screech", 1.3f, t =>
            {
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Pow(Mathf.Clamp01(1f - t / 1.3f), 1.5f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = Mathf.Sin(2f * Mathf.PI * (1900f - 700f * t) * t);
                return env * (0.25f * noise + 0.2f * tone);
            });
            return _screech;
        }
    }
}
