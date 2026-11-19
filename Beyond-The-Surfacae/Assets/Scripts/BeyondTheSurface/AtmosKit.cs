using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BeyondTheSurface
{
    /// <summary>Small helpers for building the night city and hospital mood at runtime: materials, meshes, textures.</summary>
    public static class AtmosKit
    {
        static Shader lit, unlit, particles;

        static Shader LitShader { get { if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit"); return lit; } }
        static Shader UnlitShader { get { if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit"); return unlit; } }
        static Shader ParticleShader { get { if (particles == null) particles = Shader.Find("Universal Render Pipeline/Particles/Unlit"); return particles; } }

        public static Material Lit(Color c, float smoothness, float metallic = 0f)
        {
            var m = new Material(LitShader);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            return m;
        }

        public static Material Emissive(Color baseC, Color emission, float smoothness = 0.5f)
        {
            var m = Lit(baseC, smoothness);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        /// <summary>Unlit colour; values above 1 glow through bloom.</summary>
        public static Material Glow(Color hdr)
        {
            var m = new Material(UnlitShader);
            m.SetColor("_BaseColor", hdr);
            m.enableInstancing = true;
            return m;
        }

        public static Material Particles(Color c, Texture2D tex, bool additive)
        {
            var m = new Material(ParticleShader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // additive blend set via _Blend/_DstBlend
            m.renderQueue = (int)RenderQueue.Transparent;
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            m.SetColor("_BaseColor", c);
            return m;
        }

        public static Material TransparentLit(Color c, float smoothness)
        {
            var m = Lit(c, smoothness);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m,
            bool collider = false, float yaw = 0f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = center;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            return g;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material m, Quaternion? localRot = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot ?? Quaternion.identity;
            g.transform.localScale = localScale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        /// <summary>A flat rectangle facing <paramref name="normal"/>, UVs tiled so one texture tile covers tileW x tileH metres.</summary>
        public static GameObject Facade(string name, Transform parent, Vector3 center, Vector3 normal, float w, float h,
            Material m, float tileW, float tileH, Vector2 uvOffset, bool shadows = false)
        {
            normal.Normalize();
            Vector3 up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(normal, up).normalized;
            if (Mathf.Abs(normal.y) > 0.9f) { right = Vector3.right; up = Vector3.Cross(right, normal).normalized * -1f; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh();
            Vector3 hr = right * (w * 0.5f), hu = up * (h * 0.5f);
            mesh.vertices = new[] { center - hr - hu, center - hr + hu, center + hr + hu, center + hr - hu };
            float u = w / tileW, v = h / tileH;
            mesh.uv = new[] { uvOffset, uvOffset + new Vector2(0, v), uvOffset + new Vector2(u, v), uvOffset + new Vector2(u, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Window grid: 8x8 windows per tile, some lit warm, cool or TV-blue.</summary>
        public static Texture2D WindowTexture(int seed, float litChance, out Texture2D emission)
        {
            const int cells = 8, cs = 32, size = cells * cs;
            var rnd = new System.Random(seed);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var em = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color32[size * size];
            var ep = new Color32[size * size];
            Color32 wall = new Color32(26, 27, 33, 255), frame = new Color32(16, 16, 20, 255);
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    bool lit = rnd.NextDouble() < litChance;
                    double k = rnd.NextDouble();
                    Color lc = k < 0.55 ? new Color(1f, 0.72f, 0.42f) : k < 0.8 ? new Color(0.75f, 0.85f, 1f) : k < 0.92 ? new Color(0.35f, 0.55f, 1f) : new Color(1f, 0.45f, 0.75f);
                    float bright = 0.45f + (float)rnd.NextDouble() * 0.55f;
                    bool blind = rnd.NextDouble() < 0.35;
                    Color glass = new Color(0.05f, 0.06f, 0.09f);
                    for (int y = 0; y < cs; y++)
                        for (int x = 0; x < cs; x++)
                        {
                            int i = (cy * cs + y) * size + cx * cs + x;
                            bool inWin = x >= 5 && x < cs - 5 && y >= 7 && y < cs - 4;
                            bool mullion = inWin && (x == cs / 2);
                            if (!inWin) { px[i] = (x < 2 || x >= cs - 2) ? frame : wall; ep[i] = new Color32(0, 0, 0, 255); continue; }
                            if (mullion) { px[i] = frame; ep[i] = new Color32(0, 0, 0, 255); continue; }
                            if (lit)
                            {
                                float g = bright * (blind ? (0.55f + 0.45f * ((y / 2) % 2)) : (0.8f + 0.2f * (y / (float)cs)));
                                Color c = lc * g;
                                px[i] = Color.Lerp(glass, c, 0.6f);
                                ep[i] = c;
                            }
                            else { px[i] = glass; ep[i] = new Color32(0, 0, 0, 255); }
                        }
                }
            tex.SetPixels32(px); tex.Apply(true);
            em.SetPixels32(ep); em.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat; em.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear; em.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4; em.anisoLevel = 4;
            emission = em;
            return tex;
        }

        public static Material WindowMaterial(int seed, float litChance, float emissionBoost)
        {
            Texture2D em;
            var tex = WindowTexture(seed, litChance, out em);
            var m = Lit(Color.white, 0.72f);
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_EmissionMap", em);
            m.SetColor("_EmissionColor", Color.white * emissionBoost);
            return m;
        }

        public static Texture2D SoftDot(int size = 64)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var p = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    p[y * size + x] = new Color(1, 1, 1, a * a);
                }
            t.SetPixels(p); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Thin vertical streak for rain drops.</summary>
        public static Texture2D Streak()
        {
            const int w = 8, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float fy = Mathf.Sin((y + 0.5f) / h * Mathf.PI);
                    t.SetPixel(x, y, new Color(1, 1, 1, fx * fx * fy));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Irregular puddle mask (alpha) built from overlapping blobs.</summary>
        public static Texture2D PuddleMask(int seed, int size = 128)
        {
            var rnd = new System.Random(seed);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var acc = new float[size * size];
            int blobs = 5 + rnd.Next(5);
            for (int b = 0; b < blobs; b++)
            {
                float cx = 0.25f + (float)rnd.NextDouble() * 0.5f, cy = 0.25f + (float)rnd.NextDouble() * 0.5f;
                float rx = 0.12f + (float)rnd.NextDouble() * 0.2f, ry = 0.08f + (float)rnd.NextDouble() * 0.16f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = ((x + 0.5f) / size - cx) / rx, dy = ((y + 0.5f) / size - cy) / ry;
                        acc[y * size + x] += Mathf.Exp(-(dx * dx + dy * dy) * 2.2f);
                    }
            }
            var p = new Color[size * size];
            for (int i = 0; i < p.Length; i++) p[i] = new Color(1, 1, 1, Mathf.Clamp01(acc[i]));
            t.SetPixels(p); t.Apply(true); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Equirectangular night sky: city glow at the horizon fading to near black.</summary>
        public static Material NightSky(Color horizon, Color mid, Color zenith, Color ground)
        {
            const int h = 256;
            var t = new Texture2D(4, h, TextureFormat.RGBAHalf, false);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;       // 0 = straight down, 1 = straight up
                Color c;
                if (v < 0.5f) c = Color.Lerp(ground, horizon, Mathf.Pow(v / 0.5f, 6f));
                else
                {
                    float k = (v - 0.5f) / 0.5f;
                    c = k < 0.18f ? Color.Lerp(horizon, mid, k / 0.18f) : Color.Lerp(mid, zenith, Mathf.Pow((k - 0.18f) / 0.82f, 0.6f));
                }
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var m = new Material(Shader.Find("Skybox/Panoramic"));
            m.SetTexture("_MainTex", t);
            m.SetFloat("_Exposure", 1f);
            return m;
        }

        /// <summary>World-space neon text: HDR face colour + TMP glow so bloom makes it read like gas tubes.</summary>
        public static TextMeshPro NeonText(string name, string text, Transform parent, Vector3 pos, Quaternion rot,
            Vector2 size, Color neon, float intensity = 4f)
        {
            var t = TextFactory.Create(name, text, parent, pos, rot, size, Color.white, TextAlignmentOptions.Center, true, 40f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = FontStyles.Bold;
            var m = new Material(t.fontSharedMaterial);
            m.SetColor("_FaceColor", neon * intensity);
            if (m.HasProperty("_GlowColor"))
            {
                m.EnableKeyword("GLOW_ON");
                m.SetColor("_GlowColor", new Color(neon.r, neon.g, neon.b, 0.6f) * 2f);
                m.SetFloat("_GlowOuter", 0.55f);
                m.SetFloat("_GlowInner", 0.1f);
                m.SetFloat("_GlowPower", 0.6f);
                m.SetFloat("_GlowOffset", 0f);
            }
            t.fontSharedMaterial = m;
            return t;
        }

        public static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        public static Light SpotLight(string name, Transform parent, Vector3 pos, Vector3 dir, Color c, float intensity,
            float range, float angle, LightShadows shadows = LightShadows.None)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(dir);
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot; l.color = c; l.intensity = intensity; l.range = range;
            l.spotAngle = angle; l.innerSpotAngle = angle * 0.45f; l.shadows = shadows;
            return l;
        }

        public static AudioSource Audio3D(GameObject host, AudioClip clip, float volume, float minDist, float maxDist, bool loop = true)
        {
            var a = host.AddComponent<AudioSource>();
            a.clip = clip; a.volume = volume; a.loop = loop; a.playOnAwake = false;
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = minDist; a.maxDistance = maxDist; a.dopplerLevel = 0f; a.spread = 30f;
            if (clip != null && loop) { a.time = Random.Range(0f, clip.length * 0.9f); a.Play(); }
            return a;
        }
    }
}
