using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BeyondTheSurface
{
    /// <summary>
    /// Two-way city traffic (right-hand driving) with car-following (IDM), junction traffic lights, "don't block the box",
    /// a pedestrian light at the crossing, cars that brake and honk when the player is on the road, and cars that pull over
    /// for the ambulance siren. Southbound (towards the player's start) is a rush-hour jam because the near junction favours
    /// cross traffic: that jam is what the ambulance gets stuck in.
    /// Layout: main street along Z, southbound lane x = -1.9 (heading -Z), northbound lane x = +1.9 (heading +Z).
    /// Cross streets at junction A (z -12..0) and junction B (z 60..72).
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class TrafficSystem : MonoBehaviour
    {
        public static TrafficSystem Instance { get; private set; }
        public int seed = 909;

        [Header("Crossing")]
        public float crosswalkZ = 30f;
        [Tooltip("Pedestrian light at the crossing. Red = cars drive through.")]
        public bool pedestrianGreen;

        [Header("Crash")]
        [Tooltip("Seconds between the moment the two cars start their manoeuvre and the impact")]
        public float crashLeadTime = 2.2f;
        public Vector3 crashImpactA = new Vector3(0.6f, 0f, 23.0f);   // southbound car pulling out to overtake
        public Vector3 crashImpactB = new Vector3(1.9f, 0f, 19.0f);   // northbound car running the red light
        public Vector3 wreckA = new Vector3(0.0f, 0f, 23.6f); public float wreckAYaw = 235f;
        public Vector3 wreckB = new Vector3(2.3f, 0f, 18.6f); public float wreckBYaw = -40f;
        public float CrashTime { get; private set; } = -1f;
        public Vector3 CrashPoint { get { return (crashImpactA + crashImpactB) * 0.5f; } }
        readonly List<Vector3> wrecks = new List<Vector3>();
        readonly List<Renderer> hazards = new List<Renderer>();
        Material hazardOn, hazardOff;

        // ---------------------------------------------------------------- junction signals
        public class Junction
        {
            public float zMin, zMax, mainGreen, crossGreen, clear = 1.5f, offset;
            public bool forceMainRed, forceCrossRed;
            float Cycle { get { return mainGreen + crossGreen + clear * 2f; } }
            float Phase { get { return Mathf.Repeat(Time.time + offset, Cycle); } }
            public bool MainGreen { get { return !forceMainRed && Phase < mainGreen; } }
            public bool CrossGreen { get { float p = Phase; return !forceCrossRed && p >= mainGreen + clear && p < mainGreen + clear + crossGreen; } }
            public bool Contains(float z) { return z > zMin && z < zMax; }
        }
        public Junction junctionA, junctionB;

        // ---------------------------------------------------------------- lanes and vehicles
        class StopLine { public float s; public float boxExitS = -1f; public System.Func<bool> red; }

        class Lane
        {
            public string name;
            public Vector3 start, dir, right;
            public float length, v0, spawnMin, spawnMax, nextSpawn;
            public bool isMain, northbound, southbound;
            public readonly List<Vehicle> cars = new List<Vehicle>();
            public readonly List<StopLine> stops = new List<StopLine>();
            public readonly Queue<Vehicle> pool = new Queue<Vehicle>();
            public Vector3 Pos(float s, float lat) { return start + dir * s + right * lat; }
            public float SOf(Vector3 p) { return Vector3.Dot(p - start, dir); }
        }

        class Vehicle
        {
            public Transform t;
            public Lane lane;
            public float s, v, len, v0, lat, latTarget;
            public bool yielding; public float yieldS;
            public bool forcedZebraStop;
            public float honkCooldown;
            public AudioSource horn;
            public Transform[] wheels;
        }

        readonly List<Lane> lanes = new List<Lane>();
        Lane laneS, laneN;
        System.Random rnd;
        Transform root;
        Camera cam;
        AmbulanceEvent ambulance;
        Transform ambT;
        AudioClip hornClip;
        Material tyre, glass, glowFront, glowRear, taxiGlow, plate;
        Material[] paints;
        float nextAmbientHonk;
        bool active = true;

        const float A_MAX = 1.6f, B_COMF = 2.8f, S0 = 1.8f, T_HEAD = 1.2f;

        float R() { return (float)rnd.NextDouble(); }
        float R(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }

        void Awake() { Instance = this; }

        void Start()
        {
            rnd = new System.Random(seed);
            var rig = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            cam = rig != null ? rig.Camera : Camera.main;
            ambulance = FindFirstObjectByType<AmbulanceEvent>();
            root = new GameObject("Traffic").transform;
            hornClip = MakeHorn();

            tyre = AtmosKit.Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f);
            glass = AtmosKit.Lit(new Color(0.01f, 0.012f, 0.02f), 0.95f, 0.3f);
            glowFront = AtmosKit.Glow(new Color(0.9f, 0.95f, 1f) * 6f);
            glowRear = AtmosKit.Glow(new Color(1f, 0.05f, 0.04f) * 3.5f);
            taxiGlow = AtmosKit.Glow(new Color(1f, 0.85f, 0.3f) * 3f);
            plate = AtmosKit.Lit(new Color(0.8f, 0.8f, 0.8f), 0.4f);
            paints = new[] {
                AtmosKit.Lit(new Color(0.02f, 0.02f, 0.025f), 0.9f, 0.6f), AtmosKit.Lit(new Color(0.25f, 0.02f, 0.03f), 0.9f, 0.6f),
                AtmosKit.Lit(new Color(0.55f, 0.56f, 0.6f), 0.9f, 0.8f), AtmosKit.Lit(new Color(0.02f, 0.06f, 0.14f), 0.9f, 0.6f),
                AtmosKit.Lit(new Color(0.8f, 0.8f, 0.82f), 0.85f, 0.3f), AtmosKit.Lit(new Color(0.08f, 0.12f, 0.08f), 0.85f, 0.5f),
                AtmosKit.Lit(new Color(0.85f, 0.65f, 0.05f), 0.85f, 0.4f) };

            junctionA = new Junction { zMin = -12f, zMax = 0f, mainGreen = 6f, crossGreen = 12f, offset = R(0, 10) };
            junctionB = new Junction { zMin = 60f, zMax = 72f, mainGreen = 12f, crossGreen = 8f, offset = R(0, 10) };

            BuildLanes();
            Populate();
        }

        // ---------------------------------------------------------------- setup
        Lane AddLane(string name, Vector3 a, Vector3 b, float v0, float spawnMin, float spawnMax)
        {
            var l = new Lane { name = name, start = a, dir = (b - a).normalized, length = Vector3.Distance(a, b), v0 = v0, spawnMin = spawnMin, spawnMax = spawnMax };
            l.right = Vector3.Cross(Vector3.up, l.dir).normalized;
            l.nextSpawn = Time.time + R(0f, spawnMax);
            lanes.Add(l);
            return l;
        }

        void BuildLanes()
        {
            // main street
            laneS = AddLane("South", new Vector3(-1.9f, 0, 200f), new Vector3(-1.9f, 0, -125f), 9f, 1.6f, 2.6f);
            laneS.isMain = true; laneS.southbound = true;
            AddStop(laneS, 200f - 72.6f, 200f - 59.4f, () => !junctionB.MainGreen);   // into junction B
            AddStop(laneS, 200f - (crosswalkZ + 3.9f), -1f, () => pedestrianGreen);      // crossing
            AddStop(laneS, 200f - 0.4f, 200f + 12.6f, () => !junctionA.MainGreen);     // into junction A

            laneN = AddLane("North", new Vector3(1.9f, 0, -125f), new Vector3(1.9f, 0, 200f), 11f, 3f, 6.5f);
            laneN.isMain = true; laneN.northbound = true;
            AddStop(laneN, 125f - 12.4f, 125f + 0.6f, () => !junctionA.MainGreen);
            AddStop(laneN, 125f + (crosswalkZ - 3.9f), -1f, () => pedestrianGreen || ZebraForcedForNorth());
            AddStop(laneN, 125f + 59.6f, 125f + 72.4f, () => !junctionB.MainGreen);

            // cross streets (stop lines before the sidewalk so walkers can cross)
            CrossLanes(junctionA, -6f);
            CrossLanes(junctionB, 66f);
        }

        void CrossLanes(Junction j, float zc)
        {
            var e = AddLane("CrossE" + zc, new Vector3(-175f, 0, zc - 1.7f), new Vector3(175f, 0, zc - 1.7f), 12f, 3f, 7f);
            AddStop(e, 175f - 6.3f, 175f + 6.3f, () => !j.CrossGreen);
            var w = AddLane("CrossW" + zc, new Vector3(175f, 0, zc + 1.7f), new Vector3(-175f, 0, zc + 1.7f), 12f, 3f, 7f);
            AddStop(w, 175f - 6.3f, 175f + 6.3f, () => !j.CrossGreen);
        }

        void AddStop(Lane l, float s, float boxExit, System.Func<bool> red)
        {
            l.stops.Add(new StopLine { s = s, boxExitS = boxExit, red = red });
        }

        bool zebraForced;
        bool ZebraForcedForNorth() { return zebraForced; }

        void Populate()
        {
            // southbound rush-hour queue from the near junction back up the street
            float front = 200f - 0.6f;
            while (front > 200f - 150f)
            {
                var c = Spawn(laneS, 0f);
                c.s = front - c.len * 0.5f;
                float z = laneS.Pos(c.s, 0).z;
                if (junctionB.Contains(z) || (z > 59f && z < 73.5f)) { front = 200f - 74f; Recycle(c, true); continue; }
                c.v = 0f;
                Place(c);
                front = c.s - c.len * 0.5f - R(1.8f, 3.2f);
            }
            // northbound: free flow, plus one car that will be waiting at the crossing when the ambulance comes
            for (float s = 10f; s < laneN.length - 10f; s += R(26f, 45f))
            {
                float z = laneN.Pos(s, 0).z;
                if (junctionA.Contains(z) || junctionB.Contains(z) || (z > 4f && z < 34f)) continue;
                var c = Spawn(laneN, 0f); c.s = s; c.v = c.v0 * 0.8f; Place(c);
            }
            var w = Spawn(laneN, 0f); w.s = 125f + 15f; w.v = 6f; Place(w);
            var w2 = Spawn(laneN, 0f); w2.s = 125f + 6f; w2.v = 6f; Place(w2);
            // cross streets
            foreach (var l in lanes)
            {
                if (l.isMain) continue;
                for (float s = R(5f, 30f); s < l.length - 5f; s += R(30f, 55f))
                {
                    float x = l.Pos(s, 0).x;
                    if (Mathf.Abs(x) < 9f) continue;
                    var c = Spawn(l, 0f); c.s = s; c.v = c.v0 * 0.85f; Place(c);
                }
            }
            foreach (var l in lanes) l.cars.Sort((a, b) => a.s.CompareTo(b.s));
        }

        // ---------------------------------------------------------------- vehicles
        Vehicle Spawn(Lane l, float s)
        {
            Vehicle c = l.pool.Count > 0 ? l.pool.Dequeue() : null;
            if (c == null)
            {
                float len;
                Transform[] wheels;
                var t = BuildCar("Car_" + l.name, out len, out wheels);
                c = new Vehicle { t = t, len = len, wheels = wheels, lane = l };
            }
            c.t.gameObject.SetActive(true);
            c.s = s; c.v = 0f; c.lat = 0f; c.latTarget = 0f; c.yielding = false; c.forcedZebraStop = false;
            c.v0 = l.v0 * R(0.85f, 1.1f);
            l.cars.Add(c);
            return c;
        }

        void Recycle(Vehicle c, bool silent = false)
        {
            c.lane.cars.Remove(c);
            c.t.gameObject.SetActive(false);
            c.lane.pool.Enqueue(c);
        }

        void Place(Vehicle c)
        {
            c.t.position = c.lane.Pos(c.s, c.lat);
            Vector3 fwd = c.lane.dir;
            if (Mathf.Abs(c.latTarget - c.lat) > 0.02f && c.v > 0.3f) fwd = (fwd + c.lane.right * Mathf.Sign(c.latTarget - c.lat) * 0.12f).normalized;
            c.t.rotation = Quaternion.LookRotation(fwd);
        }

        Transform BuildCar(string name, out float length, out Transform[] wheels)
        {
            var c = new GameObject(name).transform;
            c.SetParent(root, false);
            int pi = rnd.Next(paints.Length);
            bool taxi = pi == paints.Length - 1;
            bool van = !taxi && R() < 0.15f;
            var paint = paints[pi];
            float L = van ? R(4.8f, 5.3f) : R(4.0f, 4.6f); length = L;
            AtmosKit.Prim(PrimitiveType.Cube, "Body", c, new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.62f, L), paint);
            if (van)
                AtmosKit.Prim(PrimitiveType.Cube, "Cabin", c, new Vector3(0, 1.45f, -0.25f), new Vector3(1.75f, 1.05f, L * 0.78f), paint);
            else
            {
                AtmosKit.Prim(PrimitiveType.Cube, "Cabin", c, new Vector3(0, 1.17f, -0.15f), new Vector3(1.6f, 0.52f, L * 0.5f), glass);
                AtmosKit.Prim(PrimitiveType.Cube, "Roof", c, new Vector3(0, 1.44f, -0.15f), new Vector3(1.56f, 0.05f, L * 0.46f), paint);
            }
            var w = new List<Transform>();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    w.Add(AtmosKit.Prim(PrimitiveType.Cylinder, "Wheel", c, new Vector3(sx * 0.86f, 0.33f, sz * L * 0.32f), new Vector3(0.62f, 0.12f, 0.62f), tyre, Quaternion.Euler(0, 0, 90)).transform);
            wheels = w.ToArray();
            for (int sx = -1; sx <= 1; sx += 2)
            {
                AtmosKit.Prim(PrimitiveType.Cube, "HeadLight", c, new Vector3(sx * 0.62f, 0.7f, L * 0.5f + 0.01f), new Vector3(0.36f, 0.12f, 0.02f), glowFront);
                AtmosKit.Prim(PrimitiveType.Cube, "TailLight", c, new Vector3(sx * 0.66f, 0.74f, -L * 0.5f - 0.01f), new Vector3(0.3f, 0.1f, 0.02f), glowRear);
            }
            AtmosKit.Prim(PrimitiveType.Cube, "Plate", c, new Vector3(0, 0.45f, -L * 0.5f - 0.01f), new Vector3(0.5f, 0.11f, 0.02f), plate);
            if (taxi) AtmosKit.Prim(PrimitiveType.Cube, "TaxiSign", c, new Vector3(0, 1.55f, -0.15f), new Vector3(0.5f, 0.16f, 0.2f), taxiGlow);
            foreach (var r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            var bc = c.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.8f, 0); bc.size = new Vector3(1.8f, 1.6f, L);
            return c;
        }

        // ---------------------------------------------------------------- simulation
        static float Idm(float v, float v0, float gap, float dv)
        {
            float sStar = S0 + Mathf.Max(0f, v * T_HEAD + v * dv / (2f * Mathf.Sqrt(A_MAX * B_COMF)));
            float free = 1f - Mathf.Pow(v / Mathf.Max(0.1f, v0), 4f);
            float inter = sStar / Mathf.Max(0.05f, gap);
            return A_MAX * (free - inter * inter);
        }

        void Update()
        {
            if (!active) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateAmbulanceRules();
            Vector3 player = cam != null ? cam.transform.position : new Vector3(999, 0, 999);

            foreach (var l in lanes)
            {
                // spawn at lane start when there is room
                if (Time.time >= l.nextSpawn)
                {
                    Vehicle last = l.cars.Count > 0 ? l.cars[0] : null;
                    if (last == null || last.s - last.len * 0.5f > 7f)
                    {
                        var c = Spawn(l, 0f);
                        c.s = c.len * 0.5f; c.v = last == null ? c.v0 * 0.8f : Mathf.Min(c.v0 * 0.8f, last.v);
                        l.cars.Sort((a, b) => a.s.CompareTo(b.s));
                    }
                    l.nextSpawn = Time.time + R(l.spawnMin, l.spawnMax);
                }

                for (int i = l.cars.Count - 1; i >= 0; i--)
                {
                    var c = l.cars[i];
                    float front = c.s + c.len * 0.5f;
                    float gap = 9999f, leaderV = c.v0;
                    if (i < l.cars.Count - 1)
                    {
                        var ld = l.cars[i + 1];
                        gap = (ld.s - ld.len * 0.5f) - front; leaderV = ld.v;
                    }
                    // stop lines
                    foreach (var st in l.stops)
                    {
                        float g = st.s - front;
                        if (g < -0.3f || g > 60f) continue;
                        bool red = st.red();
                        if (!red && st.boxExitS > 0f && i < l.cars.Count - 1)
                        {
                            var ld = l.cars[i + 1];
                            if (ld.s - ld.len * 0.5f < st.boxExitS + c.len + 2f) red = true; // don't block the box
                        }
                        if (!red) continue;
                        float stopDist = c.v * c.v / (2f * 6f);
                        if (g < stopDist - 1f) continue; // too late to stop: go through
                        if (g < gap) { gap = g; leaderV = 0f; }
                    }
                    // crashed cars block the road
                    foreach (var wk in wrecks)
                    {
                        Vector3 dw = wk - l.Pos(c.s, c.lat); dw.y = 0f;
                        float al = Vector3.Dot(dw, l.dir), lt = Mathf.Abs(Vector3.Dot(dw, l.right));
                        if (lt < 2.4f && al > 0f && al < 70f)
                        {
                            float g = al - c.len * 0.5f - 2.7f;
                            if (g < gap) { gap = Mathf.Max(0.05f, g); leaderV = 0f; }
                        }
                    }
                    // yielding to the ambulance: pull over and stop
                    if (c.yielding)
                    {
                        float g = c.yieldS - front;
                        if (g < gap) { gap = Mathf.Max(0.05f, g); leaderV = 0f; }
                    }
                    // the player standing in the lane
                    Vector3 lp = l.Pos(c.s, c.lat);
                    Vector3 toP = player - lp; toP.y = 0f;
                    float along = Vector3.Dot(toP, l.dir), side = Mathf.Abs(Vector3.Dot(toP, l.right));
                    if (side < 1.5f && along > c.len * 0.5f && along < 30f && player.y < 3f)
                    {
                        float g = along - c.len * 0.5f - 1.5f;
                        if (g < gap) { gap = Mathf.Max(0.05f, g); leaderV = 0f; }
                        if (c.v > 1.5f || along < 6f) Honk(c, 2.5f);
                    }

                    float a = Idm(c.v, c.v0, gap, c.v - leaderV);
                    a = Mathf.Clamp(a, -9f, A_MAX);
                    c.v = Mathf.Max(0f, c.v + a * dt);
                    float step = c.v * dt;
                    if (gap < 9000f) step = Mathf.Min(step, Mathf.Max(0f, gap - 0.4f));
                    c.s += step;
                    c.lat = Mathf.MoveTowards(c.lat, c.latTarget, dt * 0.8f);
                    c.honkCooldown -= dt;
                    Place(c);
                    if (c.wheels != null && step > 0f)
                        foreach (var w in c.wheels) w.Rotate(Vector3.up, -step / 0.31f * Mathf.Rad2Deg, Space.Self);
                    if (c.s - c.len * 0.5f > l.length) Recycle(c);
                }
            }
            AmbientHonks();
            if (hazards.Count > 0)
            {
                bool on = Mathf.Repeat(Time.time * 1.4f, 1f) < 0.5f;
                foreach (var h in hazards) if (h != null) h.sharedMaterial = on ? hazardOn : hazardOff;
            }
        }

        // ---------------------------------------------------------------- ambulance
        void UpdateAmbulanceRules()
        {
            if (ambulance == null || !ambulance.Triggered) return;
            if (ambT == null) ambT = ambulance.Body;
            if (ambT == null) return;
            Vector3 ap = ambT.position;
            Vector3 af = ambulance.Heading;
            bool parked = ambulance.Parked;

            // keep the far junction clear while it turns in; freeze the jam once it is stuck
            junctionB.forceCrossRed = junctionB.forceMainRed = !parked && !ambulance.InStreet;
            junctionA.forceMainRed = false; // south of the crash the jam drains away, opening the view of the wreck
            zebraForced = false;

            foreach (var l in lanes)
                foreach (var c in l.cars)
                {
                    Vector3 cp = c.t.position;
                    Vector3 d = cp - ap; d.y = 0f;
                    float ahead = Vector3.Dot(d, af);
                    float lateral = Vector3.Dot(d, Vector3.Cross(Vector3.up, af));
                    float z = cp.z;

                    // (northbound cars south of the crash simply queue behind the wreck)
                    bool near = !parked && ahead > -3f && ahead < 55f && Mathf.Abs(lateral) < 4.5f;
                    if (l == laneS)
                    {
                        if (near || (parked && Mathf.Abs(z - ap.z) < 25f)) c.latTarget = 0.3f;
                        continue; // the jam can't stop more than it already has
                    }
                    if (near && !c.yielding)
                    {
                        c.yielding = true;
                        c.yieldS = c.s + c.len * 0.5f + Mathf.Max(2.5f, c.v * c.v / (2f * 4f));
                        c.latTarget = 0.7f;
                        Honk(c, 0.5f);
                    }
                    else if (c.yielding && (ahead < -4f || parked))
                    {
                        c.yielding = false; c.latTarget = 0f;
                    }
                }
        }

        // ---------------------------------------------------------------- the crash
        /// <summary>Starts the crash: the impact happens crashLeadTime seconds later.</summary>
        public void StageCrash() { if (CrashTime < 0f) StartCoroutine(Crash()); }

        IEnumerator Crash()
        {
            float lead = crashLeadTime;
            CrashTime = 0.0001f; // staged
            // A: an impatient driver in the southbound jam, near the crossing, pulls out to overtake
            Vehicle a = null;
            foreach (var c in laneS.cars)
            {
                float z = c.t.position.z;
                if (z > 25.5f && z < 36f && (a == null || Mathf.Abs(z - 28.5f) < Mathf.Abs(a.t.position.z - 28.5f))) a = c;
            }
            Transform at; Transform[] aw;
            if (a != null) { laneS.cars.Remove(a); at = a.t; aw = a.wheels; }
            else { float l0; at = BuildCar("Crash_A", out l0, out aw); at.position = new Vector3(-1.9f, 0f, 28.5f); at.rotation = Quaternion.LookRotation(Vector3.back); }
            // B: the front-most northbound car coming up from the near junction (runs the red light), or a new one
            Vehicle b = null;
            foreach (var c in laneN.cars) { float z = c.t.position.z; if (z > -9f && z < 13f && (b == null || z > b.t.position.z)) b = c; }
            Transform bt; Transform[] bw;
            if (b != null) { laneN.cars.Remove(b); bt = b.t; bw = b.wheels; }
            else { float l1; bt = BuildCar("Crash_B", out l1, out bw); bt.position = crashImpactB - Vector3.forward * 11f * lead; bt.rotation = Quaternion.LookRotation(Vector3.forward); }

            Vector3 a0 = at.position, b0 = bt.position;
            b0.x = crashImpactB.x;
            Vector3 aCtrl = new Vector3(a0.x, 0f, Mathf.Lerp(a0.z, crashImpactA.z, 0.45f));
            var aSrc = Src(at.gameObject); var bSrc = Src(bt.gameObject);
            aSrc.PlayOneShot(ProceduralAudio.Screech(), 0.35f);   // wheelspin pulling out of the queue
            bool bScreech = false;
            float t = 0f;
            Vector3 aPrev = a0, bPrev = b0;
            while (t < lead)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / lead);
                float ua = Mathf.Pow(k, 1.5f);
                Vector3 pa = (1 - ua) * (1 - ua) * a0 + 2 * (1 - ua) * ua * aCtrl + ua * ua * crashImpactA;
                Vector3 pb = Vector3.Lerp(b0, crashImpactB, k);
                if (!bScreech && k > 0.72f) { bScreech = true; bSrc.PlayOneShot(ProceduralAudio.Screech(), 0.9f); HonkOnce(bt, 1.1f); }
                Face(at, pa - aPrev); Face(bt, pb - bPrev);
                at.position = pa; bt.position = pb;
                SpinWheels(aw, (pa - aPrev).magnitude); SpinWheels(bw, (pb - bPrev).magnitude);
                aPrev = pa; bPrev = pb;
                yield return null;
            }

            // impact
            CrashTime = Time.time;
            Vector3 hit = CrashPoint + Vector3.up * 0.7f;
            var boom = new GameObject("CrashSound"); boom.transform.SetParent(root, false); boom.transform.position = hit;
            var bs = boom.AddComponent<AudioSource>(); bs.spatialBlend = 1f; bs.minDistance = 6f; bs.maxDistance = 150f; bs.volume = 1f;
            bs.PlayOneShot(MakeCrash(), 1f);
            Burst(hit, new Color(1f, 0.6f, 0.2f) * 4f, 45, 7f, 0.5f, 0.05f, true);   // sparks
            Burst(hit, new Color(0.7f, 0.85f, 1f), 70, 4f, 6f, 0.04f, false);        // glass
            if (ambulance != null) ambulance.CloseCrossing();

            // the two cars spin and slide to rest across both lanes
            Vector3 aS = at.position, bS = bt.position;
            Quaternion aR = at.rotation, bR = bt.rotation;
            Quaternion aE = Quaternion.Euler(0f, wreckAYaw, 0f), bE = Quaternion.Euler(0f, wreckBYaw, 0f);
            t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.9f), 3f);
                float bounce = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) * 0.12f;
                at.position = Vector3.Lerp(aS, wreckA, k) + Vector3.up * bounce;
                bt.position = Vector3.Lerp(bS, wreckB, k) + Vector3.up * bounce * 0.7f;
                at.rotation = Quaternion.Slerp(aR, aE, k) * Quaternion.Euler(0, 0, bounce * 25f);
                bt.rotation = Quaternion.Slerp(bR, bE, k) * Quaternion.Euler(bounce * -20f, 0, 0);
                yield return null;
            }
            at.position = wreckA; bt.position = wreckB; at.rotation = aE; bt.rotation = bE;
            wrecks.Add(wreckA); wrecks.Add(wreckB);

            Wreck(at); Wreck(bt);
            Debris();
            // a stuck horn on the northbound car
            var stuck = bt.gameObject.AddComponent<AudioSource>();
            stuck.clip = hornClip; stuck.loop = true; stuck.spatialBlend = 1f; stuck.minDistance = 3f; stuck.maxDistance = 70f; stuck.volume = 0.32f; stuck.pitch = 0.92f;
            stuck.Play();
            yield return new WaitForSeconds(4.5f);
            stuck.Stop();
        }

        AudioSource Src(GameObject g)
        {
            var s = g.AddComponent<AudioSource>();
            s.spatialBlend = 1f; s.minDistance = 4f; s.maxDistance = 90f; s.playOnAwake = false; s.dopplerLevel = 0f;
            return s;
        }

        static void Face(Transform t, Vector3 v) { v.y = 0f; if (v.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(v); }

        static void SpinWheels(Transform[] wheels, float dist)
        {
            if (wheels == null) return;
            foreach (var w in wheels) if (w != null) w.Rotate(Vector3.up, -dist / 0.31f * Mathf.Rad2Deg, Space.Self);
        }

        void HonkOnce(Transform car, float pitch)
        {
            var s = Src(car.gameObject);
            s.pitch = pitch; s.PlayOneShot(hornClip, 0.8f);
        }

        // hazard lights, crumpled bonnet, smashed headlight, smoke
        void Wreck(Transform car)
        {
            if (hazardOn == null) { hazardOn = AtmosKit.Glow(new Color(1f, 0.55f, 0.05f) * 5f); hazardOff = AtmosKit.Glow(new Color(0.15f, 0.07f, 0.01f)); }
            var body = car.Find("Body");
            float L = body != null ? body.localScale.z : 4.4f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    hazards.Add(AtmosKit.Prim(PrimitiveType.Cube, "Hazard", car, new Vector3(sx * 0.86f, 0.72f, sz * (L * 0.5f + 0.005f)), new Vector3(0.12f, 0.09f, 0.03f), hazardOff).GetComponent<Renderer>());
            AtmosKit.Prim(PrimitiveType.Cube, "Crumple", car, new Vector3(0.2f, 0.98f, L * 0.36f), new Vector3(1.5f, 0.12f, 0.9f),
                body != null ? body.GetComponent<Renderer>().sharedMaterial : glass, Quaternion.Euler(-18f, 8f, 4f));
            bool smashed = false;
            foreach (var hl in car.GetComponentsInChildren<Transform>())
                if (hl.name == "HeadLight" && !smashed) { hl.gameObject.SetActive(false); smashed = true; }

            var go = new GameObject("WreckSmoke");
            go.transform.SetParent(car, false);
            go.transform.localPosition = new Vector3(0f, 1.0f, L * 0.4f);
            go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.startLifetime = 4f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); m.maxParticles = 150; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var e = ps.emission; e.rateOverTime = 18f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 15f; sh.radius = 0.3f;
            var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3f));
            var co = ps.colorOverLifetime; co.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            co.color = g;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); v.y = new ParticleSystem.MinMaxCurve(0f, 0f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AtmosKit.Particles(new Color(0.45f, 0.45f, 0.5f, 0.16f), AtmosKit.SoftDot(), false);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        void Debris()
        {
            var dark = AtmosKit.Lit(new Color(0.03f, 0.03f, 0.035f), 0.5f, 0.3f);
            var chrome = AtmosKit.Lit(new Color(0.6f, 0.6f, 0.62f), 0.9f, 0.9f);
            var shard = AtmosKit.Lit(new Color(0.6f, 0.7f, 0.8f), 0.97f, 0.1f);
            Vector3 c = CrashPoint;
            for (int i = 0; i < 26; i++)
            {
                var m = i < 6 ? dark : (i < 9 ? chrome : shard);
                float s = i < 9 ? R(0.12f, 0.45f) : R(0.03f, 0.08f);
                Vector3 p = new Vector3(R(-3.1f, 3.1f), 0.015f, c.z + R(-3.5f, 3.5f));
                AtmosKit.Prim(PrimitiveType.Cube, "Debris", root, p, new Vector3(s, i < 9 ? 0.04f : 0.01f, s * R(0.5f, 1.5f)), m, Quaternion.Euler(0, R(0, 360), 0))
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            AtmosKit.Prim(PrimitiveType.Cube, "Bumper", root, c + new Vector3(-1.4f, 0.1f, 1.2f), new Vector3(1.6f, 0.18f, 0.2f), dark, Quaternion.Euler(0, 63, 6));
        }

        void Burst(Vector3 p, Color c, int count, float speed, float life, float size, bool sparks)
        {
            var go = new GameObject(sparks ? "Sparks" : "Glass");
            go.transform.SetParent(root, false); go.transform.position = p;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = false; m.duration = 0.2f; m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            m.gravityModifier = 1f; m.maxParticles = count; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startColor = sparks ? c : Color.white;
            var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.Planes; col.SetPlane(0, GroundPlane());
            col.bounce = 0.25f; col.dampen = 0.6f; col.lifetimeLoss = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (sparks)
            {
                r.sharedMaterial = AtmosKit.Particles(Color.white, AtmosKit.SoftDot(), true);
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1f;
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                r.sharedMaterial = AtmosKit.Lit(new Color(0.7f, 0.8f, 0.9f), 0.98f);
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        Transform groundPlane;
        Transform GroundPlane()
        {
            if (groundPlane == null) { groundPlane = new GameObject("CrashGroundPlane").transform; groundPlane.SetParent(root, false); groundPlane.position = new Vector3(0, 0.02f, 0); }
            return groundPlane;
        }

        static AudioClip MakeCrash()
        {
            const int sr = 44100; float dur = 1.6f;
            int n = (int)(sr * dur);
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            float[] ring = { 820f, 1290f, 2130f, 3070f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * 0.25f;
                float body = lp * Mathf.Exp(-t * 7f) * 1.2f;
                float thump = Mathf.Sin(2 * Mathf.PI * 55f * t) * Mathf.Exp(-t * 9f) * 0.9f;
                float metal = 0f; for (int k = 0; k < ring.Length; k++) metal += Mathf.Sin(2 * Mathf.PI * ring[k] * t + k) * Mathf.Exp(-t * (3f + k));
                d[i] = Mathf.Clamp(body + thump + metal * 0.12f, -1f, 1f);
            }
            for (int g = 0; g < 18; g++)      // glass tinkles
            {
                int start = (int)(sr * (0.1f + (float)rng.NextDouble() * 1.2f));
                float f = 3000f + (float)rng.NextDouble() * 4000f;
                for (int i = 0; i < 2500 && start + i < n; i++)
                    d[start + i] = Mathf.Clamp(d[start + i] + Mathf.Sin(2 * Mathf.PI * f * i / sr) * Mathf.Exp(-i / 500f) * 0.25f, -1f, 1f);
            }
            var clip = AudioClip.Create("CarCrash", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        public bool CrossGreen(float z)
        {
            if (junctionA != null && junctionA.Contains(z)) return junctionA.CrossGreen;
            if (junctionB != null && junctionB.Contains(z)) return junctionB.CrossGreen;
            return false;
        }

        // ---------------------------------------------------------------- horns
        void Honk(Vehicle c, float pitch = 1f)
        {
            if (c.honkCooldown > 0f || hornClip == null) return;
            c.honkCooldown = R(3f, 6f);
            if (c.horn == null)
            {
                c.horn = c.t.gameObject.AddComponent<AudioSource>();
                c.horn.spatialBlend = 1f; c.horn.minDistance = 3f; c.horn.maxDistance = 60f; c.horn.rolloffMode = AudioRolloffMode.Logarithmic;
                c.horn.playOnAwake = false; c.horn.dopplerLevel = 0f;
            }
            c.horn.pitch = R(0.85f, 1.15f);
            c.horn.PlayOneShot(hornClip, R(0.45f, 0.75f));
        }

        void AmbientHonks()
        {
            if (Time.time < nextAmbientHonk || cam == null) return;
            bool stress = ambulance != null && ambulance.Triggered;
            nextAmbientHonk = Time.time + (stress ? R(1.2f, 3.5f) : R(6f, 13f));
            // an impatient driver somewhere in the jam near the player
            Vehicle best = null; float bestD = 1e9f;
            foreach (var c in laneS.cars)
            {
                float d = Mathf.Abs(c.t.position.z - cam.transform.position.z) + R(0f, 25f);
                if (d < bestD && c.v < 2f) { bestD = d; best = c; }
            }
            if (best != null && bestD < 60f) { best.honkCooldown = 0f; Honk(best); }
        }

        static AudioClip MakeHorn()
        {
            const int sr = 44100; float dur = 0.55f;
            int n = (int)(sr * dur);
            var data = new float[n];
            float p1 = 0, p2 = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                p1 += 415f / sr; p2 += 523f / sr;
                float s = (Mathf.Repeat(p1, 1f) * 2f - 1f) + (Mathf.Repeat(p2, 1f) * 2f - 1f) * 0.8f; // two saw tones
                lp += (s - lp) * 0.18f;                                                             // soften
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((dur - t) / 0.08f);
                data[i] = Mathf.Clamp(lp * 0.45f * env, -1f, 1f);
            }
            var clip = AudioClip.Create("CarHorn", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Leave()
        {
            active = false;
            if (root != null) root.gameObject.SetActive(false);
        }
    }
}
