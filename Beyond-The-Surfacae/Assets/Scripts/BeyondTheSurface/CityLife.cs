using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BeyondTheSurface
{
    /// <summary>
    /// Ambient life for the night city: low-poly pedestrians with umbrellas walking the sidewalks (they step around the
    /// player and turn back at the parked ambulance), people waiting at the crossing, parked cars, and traffic with
    /// glowing head/tail lights on the cross streets and the far ends of the street.
    /// </summary>
    public class CityLife : MonoBehaviour
    {
        public int seed = 4242;
        public int walkers = 38;
        public AudioClip pedestrianSteps;
        [Range(0, 12)] public int walkersWithSteps = 7;

        class Walker
        {
            public Transform root, legL, legR, body;
            public float speed, dir, laneX, phase, sideOffset, z;
            public float minZ, maxZ;
            public float waited;
        }

        class Car
        {
            public Transform t;
            public Vector3[] path;
            public int seg;
            public float speed;
        }

        readonly List<Walker> people = new List<Walker>();
        readonly List<Car> cars = new List<Car>();
        readonly List<Transform> idlers = new List<Transform>();
        System.Random rnd;
        Transform root;
        Camera cam;
        AmbulanceEvent ambulance;
        Mesh canopy;
        Material skin, glowFront, glowRear, tyre, glass, phone;
        Material[] coats, umbrellas, paints;
        bool active = true;

        float R() { return (float)rnd.NextDouble(); }
        float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }

        void Start()
        {
            rnd = new System.Random(seed);
            var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            cam = rig != null ? rig.Camera : Camera.main;
            ambulance = FindFirstObjectByType<AmbulanceEvent>();
            root = new GameObject("CityLife").transform;

            skin = AtmosKit.Lit(new Color(0.55f, 0.4f, 0.32f), 0.4f);
            tyre = AtmosKit.Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f);
            glass = AtmosKit.Lit(new Color(0.01f, 0.012f, 0.02f), 0.95f, 0.3f);
            glowFront = AtmosKit.Glow(new Color(0.9f, 0.95f, 1f) * 6f);
            glowRear = AtmosKit.Glow(new Color(1f, 0.05f, 0.04f) * 4f);
            phone = AtmosKit.Glow(new Color(0.6f, 0.8f, 1f) * 2.5f);
            coats = new[] {
                AtmosKit.Lit(new Color(0.03f, 0.03f, 0.035f), 0.55f), AtmosKit.Lit(new Color(0.12f, 0.09f, 0.07f), 0.5f),
                AtmosKit.Lit(new Color(0.05f, 0.07f, 0.1f), 0.6f), AtmosKit.Lit(new Color(0.18f, 0.16f, 0.14f), 0.5f),
                AtmosKit.Lit(new Color(0.25f, 0.04f, 0.05f), 0.6f), AtmosKit.Lit(new Color(0.55f, 0.48f, 0.2f), 0.7f) };
            umbrellas = new[] {
                AtmosKit.Lit(new Color(0.02f, 0.02f, 0.025f), 0.85f), AtmosKit.Lit(new Color(0.02f, 0.03f, 0.08f), 0.85f),
                AtmosKit.Lit(new Color(0.4f, 0.02f, 0.04f), 0.85f), AtmosKit.Lit(new Color(0.02f, 0.2f, 0.2f), 0.85f),
                AtmosKit.TransparentLit(new Color(0.75f, 0.85f, 0.95f, 0.3f), 0.97f), AtmosKit.Lit(new Color(0.5f, 0.4f, 0.05f), 0.85f) };
            paints = new[] {
                AtmosKit.Lit(new Color(0.02f, 0.02f, 0.025f), 0.9f, 0.6f), AtmosKit.Lit(new Color(0.25f, 0.02f, 0.03f), 0.9f, 0.6f),
                AtmosKit.Lit(new Color(0.55f, 0.56f, 0.6f), 0.9f, 0.8f), AtmosKit.Lit(new Color(0.02f, 0.06f, 0.14f), 0.9f, 0.6f),
                AtmosKit.Lit(new Color(0.85f, 0.65f, 0.05f), 0.85f, 0.4f), AtmosKit.Lit(new Color(0.8f, 0.8f, 0.82f), 0.85f, 0.3f) };
            canopy = BuildCanopy();

            SpawnWalkers();
            SpawnIdlers();
            // cars are simulated by TrafficSystem
        }

        // ------------------------------------------------------------------ pedestrians
        Mesh BuildCanopy()
        {
            // top and underside use separate vertices: shared vertices would average opposite normals to zero (NaN shading -> bloom blows the screen white)
            const int ribs = 8; const float r = 0.55f, h = 0.2f;
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int apex = v.Count;
                v.Add(new Vector3(0, h - side * 0.01f, 0));
                for (int i = 0; i < ribs; i++) { float a = i / (float)ribs * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * r, -side * 0.01f, Mathf.Sin(a) * r)); }
                for (int i = 0; i < ribs; i++)
                {
                    int a = apex + 1 + i, b = apex + 1 + (i + 1) % ribs;
                    if (side == 0) tris.AddRange(new[] { apex, b, a }); else tris.AddRange(new[] { apex, a, b });
                }
            }
            var m = new Mesh { vertices = v.ToArray(), triangles = tris.ToArray() };
            var uv = new Vector2[v.Count];
            m.uv = uv;
            m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        Transform BuildPerson(string name, bool umbrella)
        {
            var p = new GameObject(name).transform;
            p.SetParent(root, false);
            var coat = coats[rnd.Next(coats.Length)];
            float tall = R(0.92f, 1.08f);
            p.localScale = Vector3.one * tall;
            var body = AtmosKit.Prim(PrimitiveType.Capsule, "Body", p, new Vector3(0, 1.08f, 0), new Vector3(0.42f, 0.5f, 0.3f), coat);
            AtmosKit.Prim(PrimitiveType.Sphere, "Head", body.transform, new Vector3(0, 1.32f, 0), new Vector3(0.52f, 0.36f, 0.68f), R() < 0.4f ? coat : skin);
            for (int s = -1; s <= 1; s += 2)
            {
                var hip = new GameObject(s < 0 ? "LegL" : "LegR").transform;
                hip.SetParent(p, false); hip.localPosition = new Vector3(s * 0.1f, 0.78f, 0);
                AtmosKit.Prim(PrimitiveType.Cube, "Leg", hip, new Vector3(0, -0.38f, 0), new Vector3(0.12f, 0.78f, 0.14f), coats[0]);
            }
            if (umbrella)
            {
                AtmosKit.Prim(PrimitiveType.Cylinder, "Shaft", p, new Vector3(0.12f, 1.62f, 0.15f), new Vector3(0.02f, 0.45f, 0.02f), coats[0]);
                var c = new GameObject("Canopy");
                c.transform.SetParent(p, false); c.transform.localPosition = new Vector3(0.1f, 2.05f, 0.12f);
                c.transform.localRotation = Quaternion.Euler(R(-8f, 8f), 0, R(-8f, 8f));
                c.AddComponent<MeshFilter>().sharedMesh = canopy;
                var mr = c.AddComponent<MeshRenderer>(); mr.sharedMaterial = umbrellas[rnd.Next(umbrellas.Length)];
            }
            else if (R() < 0.6f)
            {
                AtmosKit.Prim(PrimitiveType.Cube, "Phone", p, new Vector3(0.05f, 1.3f, 0.28f), new Vector3(0.07f, 0.12f, 0.01f), phone, Quaternion.Euler(-35, 0, 0));
            }
            foreach (var r in p.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return p;
        }

        void SpawnWalkers()
        {
            float[] lanes = { -5.35f, -4.35f, 4.35f, 5.35f };
            for (int i = 0; i < walkers; i++)
            {
                var w = new Walker();
                w.root = BuildPerson("Pedestrian_" + i, R() < 0.72f);
                w.body = w.root.Find("Body");
                w.legL = w.root.Find("LegL"); w.legR = w.root.Find("LegR");
                w.laneX = lanes[rnd.Next(lanes.Length)] + R(-0.2f, 0.2f);
                w.dir = R() < 0.5f ? 1f : -1f;
                w.speed = R(1.0f, 1.5f);
                w.phase = R(0f, 6.28f);
                // most people near the playable block, some far away down the street
                w.minZ = -100f; w.maxZ = 180f;
                w.z = i < walkers * 0.6f ? R(-5f, 65f) : R(w.minZ, w.maxZ);
                if (w.z > 24f && w.z < 36f && ambulance != null) w.z += 14f;
                people.Add(w);
                if (pedestrianSteps != null && i < walkersWithSteps)
                {
                    var a = AtmosKit.Audio3D(w.root.gameObject, pedestrianSteps, 0.7f, 1.2f, 9f);
                    a.pitch = R(0.92f, 1.08f);
                }
            }
        }

        void SpawnIdlers()
        {
            // people waiting at the crossing, on both corners
            var spots = new[] { new Vector3(4.6f, 0.15f, 28.6f), new Vector3(5.3f, 0.15f, 29.4f), new Vector3(4.4f, 0.15f, 31.2f),
                                new Vector3(-5.5f, 0.15f, 31.6f), new Vector3(-5.4f, 0.15f, 25.6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = BuildPerson("Waiting_" + i, i != 2);
                p.position = spots[i];
                p.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sign(spots[i].x), 0, R(-0.4f, 0.4f)));
                idlers.Add(p);
            }
        }

        const float BandIn = 3.9f, BandOut = 5.72f, BodyR = 0.32f;

        // pick the side of an obstacle that keeps the walker on the sidewalk and is closest to where they are
        static bool Dodge(float side, float curX, float ox, float clear, out float target)
        {
            float lo = side < 0 ? -BandOut : BandIn, hi = side < 0 ? -BandIn : BandOut;
            float a = ox - clear, b = ox + clear;
            bool okA = a >= lo && a <= hi, okB = b >= lo && b <= hi;
            if (okA && okB) target = Mathf.Abs(a - curX) < Mathf.Abs(b - curX) ? a : b;
            else if (okA) target = a;
            else if (okB) target = b;
            else { target = curX; return false; }
            return true;
        }

        void UpdateWalkers(float dt)
        {
            Vector3 head = cam != null ? cam.transform.position : new Vector3(999, 0, 999);
            var traffic = TrafficSystem.Instance;
            var statics = CityAtmosphere.SidewalkObstacles;
            foreach (var w in people)
            {
                float side = Mathf.Sign(w.laneX);
                float curX = w.laneX + w.sideOffset;
                float targetX = w.laneX;
                float urgent = 99f, speedK = 1f;

                // static props: trees, benches, bins, poles, bus stop, waiting people, the player
                System.Action<Vector3, float> consider = (o, r) =>
                {
                    if (Mathf.Sign(o.x) != side) return;
                    float clear = r + BodyR + 0.08f;
                    float along = (o.z - w.z) * w.dir;
                    if (along < -clear || along > 4.2f) return; // keep the detour until fully past it
                    if (Mathf.Abs(o.x - curX) >= clear && Mathf.Abs(o.x - w.laneX) >= clear) return;
                    if (along >= urgent) return;
                    float tx;
                    if (Dodge(side, curX, o.x, clear, out tx)) { urgent = along; targetX = tx; }
                    else if (along < 1.2f) { urgent = along; targetX = curX; speedK = 0f; } // no room: wait
                };
                foreach (var o in statics) if (Mathf.Abs(o.z - w.z) < 5f) consider(o, o.w);
                foreach (var p in idlers) if (Mathf.Abs(p.position.z - w.z) < 5f) consider(p.position, 0.32f);
                if (Mathf.Abs(head.z - w.z) < 5f) consider(head, 0.4f);

                // people coming the other way: both keep to their right
                foreach (var o in people)
                {
                    if (o == w || o.dir == w.dir || Mathf.Sign(o.laneX) != side) continue;
                    float along = (o.z - w.z) * w.dir;
                    if (along < 0f || along > 2.6f) continue;
                    if (Mathf.Abs((o.laneX + o.sideOffset) - curX) > 0.7f) continue;
                    if (urgent < 98f) continue; // already dodging something solid
                    float rightSign = w.dir > 0 ? 1f : -1f;
                    targetX = Mathf.Clamp(curX + rightSign * 0.5f, side < 0 ? -BandOut : BandIn, side < 0 ? -BandIn : BandOut);
                }

                if (Mathf.Abs(targetX - curX) > 0.35f) speedK = Mathf.Min(speedK, 0.65f);
                // the crash: people nearby stop and stare for a few seconds
                bool staring = false;
                if (traffic != null && traffic.CrashTime > 1f && Time.time - traffic.CrashTime < 5f + (w.phase % 2f))
                {
                    Vector3 cp = traffic.CrashPoint;
                    if (Mathf.Abs(cp.z - w.z) < 16f) { speedK = 0f; staring = true; w.waited = 0f; }
                }
                // boxed in for a while (e.g. by the crowd at the crossing): turn around
                if (speedK == 0f && !staring) { w.waited += dt; if (w.waited > 1.5f) { w.dir = -w.dir; w.waited = 0f; } } else w.waited = 0f;
                float next = w.z + w.dir * w.speed * speedK * dt;
                // wait at the corner while cross-street traffic has green
                bool entering = InJunction(next) && !InJunction(w.z);
                if (entering && traffic != null && traffic.CrossGreen(next)) { next = w.z; speedK = 0f; }
                w.z = next;
                if (w.z > w.maxZ) w.z = w.minZ; else if (w.z < w.minZ) w.z = w.maxZ;

                float prevOff = w.sideOffset;
                w.sideOffset = Mathf.MoveTowards(w.sideOffset, targetX - w.laneX, dt * 1.7f);
                float x = w.laneX + w.sideOffset;
                x = side < 0 ? Mathf.Clamp(x, -BandOut, -BandIn) : Mathf.Clamp(x, BandIn, BandOut);
                float y = InJunction(w.z) ? 0f : 0.15f;

                float moving = speedK;
                w.phase += dt * w.speed * 5.2f * moving;
                float bob = Mathf.Abs(Mathf.Sin(w.phase)) * 0.035f;
                w.root.position = new Vector3(x, y + bob, w.z);
                float latV = (w.sideOffset - prevOff) / Mathf.Max(dt, 1e-4f);
                Vector3 look = new Vector3(latV * 0.6f, 0, w.dir * Mathf.Max(0.3f, w.speed * speedK));
                if (staring)
                {
                    Vector3 toCrash = traffic.CrashPoint - w.root.position; toCrash.y = 0f;
                    look = toCrash.sqrMagnitude > 0.01f ? toCrash : look;
                }
                w.root.rotation = Quaternion.Slerp(w.root.rotation, Quaternion.LookRotation(look), dt * (staring ? 3f : 8f));
                float swing = Mathf.Sin(w.phase) * 26f * Mathf.Clamp01(moving + Mathf.Abs(latV));
                if (w.legL != null) w.legL.localRotation = Quaternion.Euler(swing, 0, 0);
                if (w.legR != null) w.legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            }
            float t = Time.time;
            for (int i = 0; i < idlers.Count; i++)
            {
                var b = idlers[i].Find("Body");
                if (b != null) b.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + i) * 2f, Mathf.Sin(t * 0.3f + i * 2f) * 8f, 0);
                if (traffic != null && traffic.CrashTime > 1f)
                {
                    Vector3 toCrash = traffic.CrashPoint - idlers[i].position; toCrash.y = 0f;
                    idlers[i].rotation = Quaternion.Slerp(idlers[i].rotation, Quaternion.LookRotation(toCrash), dt * 2f);
                }
            }
        }

        static bool InJunction(float z) { return (z > -12f && z < 0f) || (z > 60f && z < 72f); }


        // ------------------------------------------------------------------ cars
        Transform BuildCar(string name, bool lightsOn, out float length)
        {
            var c = new GameObject(name).transform;
            c.SetParent(root, false);
            bool taxi = R() < 0.15f;
            var paint = taxi ? paints[4] : paints[rnd.Next(paints.Length)];
            float L = R(4.0f, 4.7f); length = L;
            AtmosKit.Prim(PrimitiveType.Cube, "Body", c, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.6f, L), paint);
            AtmosKit.Prim(PrimitiveType.Cube, "Cabin", c, new Vector3(0, 1.12f, -0.15f), new Vector3(1.6f, 0.5f, L * 0.5f), glass);
            AtmosKit.Prim(PrimitiveType.Cube, "Roof", c, new Vector3(0, 1.39f, -0.15f), new Vector3(1.55f, 0.05f, L * 0.46f), paint);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    AtmosKit.Prim(PrimitiveType.Cylinder, "Wheel", c, new Vector3(sx * 0.85f, 0.33f, sz * L * 0.32f), new Vector3(0.62f, 0.12f, 0.62f), tyre, Quaternion.Euler(0, 0, 90));
            var front = lightsOn ? glowFront : AtmosKit.Lit(new Color(0.6f, 0.6f, 0.6f), 0.9f);
            var rear = lightsOn ? glowRear : AtmosKit.Glow(new Color(0.3f, 0.01f, 0.01f));
            for (int sx = -1; sx <= 1; sx += 2)
            {
                AtmosKit.Prim(PrimitiveType.Cube, "HeadLight", c, new Vector3(sx * 0.62f, 0.68f, L * 0.5f + 0.01f), new Vector3(0.35f, 0.12f, 0.02f), front);
                AtmosKit.Prim(PrimitiveType.Cube, "TailLight", c, new Vector3(sx * 0.65f, 0.72f, -L * 0.5f - 0.01f), new Vector3(0.3f, 0.1f, 0.02f), rear);
            }
            if (taxi) AtmosKit.Prim(PrimitiveType.Cube, "TaxiSign", c, new Vector3(0, 1.5f, -0.15f), new Vector3(0.5f, 0.16f, 0.2f), AtmosKit.Glow(new Color(1f, 0.85f, 0.3f) * 3f));
            foreach (var r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return c;
        }

        void SpawnParkedCars()
        {
            // parked along the player's side of the street, away from the crossing where the ambulance stops
            foreach (float z in new[] { 3.5f, 9.8f, 16.4f, 42.5f, 48.6f, 56f })
            {
                float len;
                var c = BuildCar("ParkedCar", false, out len);
                c.position = new Vector3(-2.45f, 0f, z);
                c.rotation = Quaternion.Euler(0, R() < 0.5f ? 0f : 180f, 0);
                var bc = c.gameObject.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.75f, 0); bc.size = new Vector3(1.8f, 1.5f, len);
            }
        }

        void AddRoute(Vector3[] path, int count, float minSpeed, float maxSpeed)
        {
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector3.Distance(path[i - 1], path[i]);
            for (int k = 0; k < count; k++)
            {
                float len;
                var car = new Car { t = BuildCar("TrafficCar", true, out len), path = path, speed = R(minSpeed, maxSpeed) };
                // place at a random distance along the route
                float d = total * (k + R(0f, 0.6f)) / count;
                car.seg = 1;
                Vector3 p = path[0];
                for (int i = 1; i < path.Length; i++)
                {
                    float sl = Vector3.Distance(path[i - 1], path[i]);
                    if (d <= sl) { p = Vector3.Lerp(path[i - 1], path[i], d / sl); car.seg = i; break; }
                    d -= sl;
                }
                car.t.position = p;
                car.t.rotation = Quaternion.LookRotation(path[car.seg] - path[car.seg - 1]);
                cars.Add(car);
            }
        }

        void SpawnTraffic()
        {
            // cross street at the far end (z 60..72) and near end (z -12..0); lanes 1.75 m either side of centre
            AddRoute(new[] { new Vector3(-170, 0, 64.3f), new Vector3(170, 0, 64.3f) }, 5, 9f, 13f);
            AddRoute(new[] { new Vector3(170, 0, 67.7f), new Vector3(-170, 0, 67.7f) }, 5, 9f, 13f);
            AddRoute(new[] { new Vector3(-170, 0, -4.3f), new Vector3(170, 0, -4.3f) }, 4, 9f, 13f);
            AddRoute(new[] { new Vector3(170, 0, -7.7f), new Vector3(-170, 0, -7.7f) }, 4, 9f, 13f);
            // the street continues beyond the cross streets: cars arrive and turn
            AddRoute(new[] { new Vector3(-1.75f, 0, 190), new Vector3(-1.75f, 0, 67.7f), new Vector3(-170, 0, 67.7f) }, 3, 8f, 11f);
            AddRoute(new[] { new Vector3(-170, 0, 64.3f), new Vector3(1.75f, 0, 64.3f), new Vector3(1.75f, 0, 190) }, 3, 8f, 11f);
            AddRoute(new[] { new Vector3(1.75f, 0, -110), new Vector3(1.75f, 0, -4.3f), new Vector3(170, 0, -4.3f) }, 3, 8f, 11f);
            AddRoute(new[] { new Vector3(170, 0, -7.7f), new Vector3(-1.75f, 0, -7.7f), new Vector3(-1.75f, 0, -110) }, 3, 8f, 11f);
        }

        void UpdateCars(float dt)
        {
            foreach (var c in cars)
            {
                Vector3 target = c.path[c.seg];
                Vector3 pos = c.t.position;
                Vector3 to = target - pos;
                float step = c.speed * dt;
                if (to.magnitude <= step)
                {
                    c.seg++;
                    if (c.seg >= c.path.Length) { c.seg = 1; c.t.position = c.path[0]; c.t.rotation = Quaternion.LookRotation(c.path[1] - c.path[0]); continue; }
                    c.t.position = target;
                    continue;
                }
                c.t.position = pos + to.normalized * step;
                c.t.rotation = Quaternion.RotateTowards(c.t.rotation, Quaternion.LookRotation(to), 140f * dt);
            }
        }

        void Update()
        {
            if (!active) return;
            float dt = Time.deltaTime;
            UpdateWalkers(dt);
        }

        public void Leave()
        {
            active = false;
            if (root != null) root.gameObject.SetActive(false);
        }
    }
}
