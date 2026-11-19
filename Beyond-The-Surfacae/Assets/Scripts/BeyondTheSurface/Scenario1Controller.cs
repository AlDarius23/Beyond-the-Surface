using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BeyondTheSurface
{
    /// <summary>
    /// Scenario 1 ("The Intersection and the Hospital"):
    /// timed street crossing -> ambulance blocks the crossing -> timer fails -> sudden "Context Shift"
    /// teleport into the hospital where the patient is the player's father -> reflection + replay.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Scenario1Controller : MonoBehaviour
    {
        [Header("Street")]
        public float timeLimit = 25f;
        public float crosswalkZ = 30f;
        public Vector3 startPosition = new Vector3(-4.4f, 0.15f, 19f);
        public float startYaw = 0f;
        [Tooltip("Ambulance appears when the player is this close to the crossing, or at this remaining time")]
        public float triggerDistance = 7f;
        public float triggerRemaining = 15f;
        [Tooltip("Seconds after the intro text appears that the ambulance sets off (siren heard far away)")]
        public float ambulanceLeadIn = 2.6f;
        [Tooltip("Seconds of walking after the countdown starts before the two cars crash")]
        public float crashAfterSeconds = 3.5f;

        [Header("Debug")]
        public bool autoStart = true;

        XROrigin rig;
        Camera cam;
        WristWatch watch;
        AmbulanceEvent ambulance;
        EKGMonitor ekg;
        GameObject fadeQuad;
        Material fadeMat;
        Volume volume;
        Vignette vignette;
        ChromaticAberration chroma;
        TextMeshPro hud;
        GameObject hudPanel;
        bool shifted;
        InputAction replayAction;
        CityAtmosphere city;
        CityLife life;
        SoundDirector sound;
        HospitalMood hospital;
        VRWalkLocomotion walker;
        TrafficSystem traffic;

        void Awake()
        {
            if (GetComponent<GameManager>() == null) gameObject.AddComponent<GameManager>();
            watch = GetComponent<WristWatch>() ?? gameObject.AddComponent<WristWatch>();
            ambulance = GetComponent<AmbulanceEvent>() ?? gameObject.AddComponent<AmbulanceEvent>();
            ambulance.crosswalkZ = crosswalkZ;
            ekg = GetComponent<EKGMonitor>() ?? gameObject.AddComponent<EKGMonitor>();
            city = GetComponent<CityAtmosphere>(); if (city == null) city = gameObject.AddComponent<CityAtmosphere>();
            life = GetComponent<CityLife>(); if (life == null) life = gameObject.AddComponent<CityLife>();
            sound = GetComponent<SoundDirector>(); if (sound == null) sound = gameObject.AddComponent<SoundDirector>();
            hospital = GetComponent<HospitalMood>(); if (hospital == null) hospital = gameObject.AddComponent<HospitalMood>();
            traffic = GetComponent<TrafficSystem>(); if (traffic == null) traffic = gameObject.AddComponent<TrafficSystem>();
        }

        void Start()
        {
            rig = FindFirstObjectByType<XROrigin>();
            cam = rig != null ? rig.Camera : Camera.main;
            if (rig == null || cam == null) { Debug.LogError("[BTS] XR Origin / camera not found."); return; }

            PrepareCamera();
            BuildFade();
            BuildVolume();
            BuildHospitalLights();
            PlaceRig(startPosition, startYaw);

            replayAction = new InputAction("Replay", InputActionType.Button);
            replayAction.AddBinding("<XRController>{RightHand}/primaryButton");
            replayAction.AddBinding("<Keyboard>/r");
            replayAction.performed += _ => { if (shifted && reflectionShown) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0); };

            watch.Expired += OnTimerExpired;
            if (autoStart) StartCoroutine(StreetFlow());
        }

        void OnDestroy()
        {
            if (replayAction != null) { replayAction.Disable(); replayAction.Dispose(); }
            if (watch != null) watch.Expired -= OnTimerExpired;
        }

        // ---------------------------------------------------------------- setup
        void PrepareCamera()
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            var bg = cam.GetComponent("ARCameraBackground") as Behaviour;
            if (bg != null) bg.enabled = false;
            var ud = cam.GetUniversalAdditionalCameraData();
            if (ud != null) ud.renderPostProcessing = true;
        }

        void BuildFade()
        {
            fadeMat = TextFactory.NewSpriteMaterial(new Color(0, 0, 0, 0));
            fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fadeQuad.name = "FadeQuad";
            Destroy(fadeQuad.GetComponent<Collider>());
            fadeQuad.transform.SetParent(cam.transform, false);
            fadeQuad.transform.localPosition = new Vector3(0, 0, cam.nearClipPlane + 0.12f);
            fadeQuad.transform.localScale = new Vector3(3f, 3f, 1f);
            fadeQuad.GetComponent<Renderer>().sharedMaterial = fadeMat;
            fadeQuad.SetActive(false);
        }

        void SetFade(float a)
        {
            fadeQuad.SetActive(a > 0.001f);
            fadeMat.color = new Color(0, 0, 0, a);
        }

        void BuildVolume()
        {
            var go = new GameObject("EmotionVolume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.6f, 0f, 0f));
            vignette.intensity.Override(0f);
            vignette.smoothness.Override(0.6f);
            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0f);
            volume.profile = profile;
        }

        void BuildHospitalLights()
        {
            walker = rig.GetComponent<VRWalkLocomotion>();
            if (walker == null) walker = rig.gameObject.AddComponent<VRWalkLocomotion>();
            hospital.Build();
        }

        void PlaceRig(Vector3 floorPos, float yaw)
        {
            var cc = rig.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (had) cc.enabled = false;
            rig.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            rig.MoveCameraToWorldLocation(new Vector3(floorPos.x, floorPos.y + rig.CameraInOriginSpaceHeight, floorPos.z));
            if (had) cc.enabled = true;
        }

        // ---------------------------------------------------------------- HUD
        void ShowHud(string text, float seconds = 0f, float width = 1.3f, float height = 0.35f, float distance = 1.7f, float yOffset = -0.12f)
        {
            ClearHud();
            hudPanel = TextFactory.Panel("HudPanel", cam.transform, new Vector3(0, yOffset, distance + 0.01f), Quaternion.identity,
                new Vector2(width + 0.1f, height + 0.08f), new Color(0, 0, 0, 0.55f));
            hud = TextFactory.Create("HudText", text, cam.transform, new Vector3(0, yOffset, distance), Quaternion.identity,
                new Vector2(width, height), Color.white, TextAlignmentOptions.Center);
            if (seconds > 0f) StartCoroutine(HideHudAfter(hudPanel, hud, seconds));
        }

        IEnumerator HideHudAfter(GameObject panel, TextMeshPro t, float s)
        {
            yield return new WaitForSeconds(s);
            if (panel != null) Destroy(panel);
            if (t != null) Destroy(t.gameObject);
        }

        void ClearHud()
        {
            if (hudPanel != null) Destroy(hudPanel);
            if (hud != null) Destroy(hud.gameObject);
        }

        // ---------------------------------------------------------------- street flow
        IEnumerator StreetFlow()
        {
            if (walker != null) walker.movementLocked = true; // read the briefing first
            yield return new WaitForSeconds(1.0f);
            ShowHud("BEYOND THE SURFACE\n\nAi o intalnire importanta pe partea cealalta a strazii.\nTrebuie sa ajungi la timp!", 4.5f, 1.4f, 0.5f);
            // the ambulance is already on its way: its siren rises at the end of the briefing
            yield return new WaitForSeconds(ambulanceLeadIn);
            ambulance.Trigger();
            StartCoroutine(PedestrianLightIrony());
            yield return new WaitForSeconds(Mathf.Max(0.1f, 4.8f - ambulanceLeadIn));
            watch.StartCountdown(timeLimit);
            if (walker != null) walker.movementLocked = false;
            replayAction.Enable();
            // a few seconds of walking, then two cars crash ahead and lock the street; the ambulance arrives into it
            if (traffic != null) StartCoroutine(StageCrashAfter(crashAfterSeconds - traffic.crashLeadTime));

            while (!shifted)
            {
                if (!ambulance.Triggered) ambulance.Trigger();

                // stress vignette rises as time runs out
                if (vignette != null)
                {
                    float stress = 1f - Mathf.Clamp01(watch.Remaining / 12f);
                    vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, 0.45f * stress, Time.deltaTime * 2f);
                }
                yield return null;
            }
        }

        IEnumerator StageCrashAfter(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            if (!shifted) traffic.StageCrash();
        }

        // the walk signal finally turns green... while the crossing is blocked by the stuck ambulance
        IEnumerator PedestrianLightIrony()
        {
            while (!ambulance.Parked && !shifted) yield return null;
            yield return new WaitForSeconds(6f);
            if (traffic != null && !shifted) traffic.pedestrianGreen = true;
        }


        void OnTimerExpired()
        {
            if (shifted) return;
            GameManager.Instance.timerFailed = true;
            StartCoroutine(ContextShift());
        }

        // ---------------------------------------------------------------- the context shift
        bool reflectionShown;

        IEnumerator ContextShift()
        {
            shifted = true;
            ShowHud("<color=#ff3030><b>TASK ESUAT</b></color>\nAi pierdut timpul.", 0f, 1.0f, 0.3f);
            yield return new WaitForSeconds(1.6f);

            // fade out while the siren sags
            ambulance.StopSiren(1.3f);
            if (walker != null) walker.movementLocked = true;
            if (sound != null) sound.EnterHospital(1.3f);
            float t = 0f;
            while (t < 1.3f) { t += Time.deltaTime; SetFade(t / 1.3f); yield return null; }
            SetFade(1f);
            ClearHud();

            // teleport to the hospital, facing the bed (-Z)
            var spawn = WristWatch.FindByName("Hospital_PlayerSpawn");
            Vector3 spawnPos = spawn != null ? spawn.position : new Vector3(200f, 0f, 2.4f);
            PlaceRig(spawnPos, 180f);
            if (city != null) city.Leave();
            if (life != null) life.Leave();
            if (traffic != null) traffic.Leave();
            ekg.Begin();
            if (hospital != null) hospital.Enter();
            if (vignette != null) { vignette.color.value = Color.black; vignette.intensity.value = 0.38f; }
            yield return new WaitForSeconds(0.6f);

            // fade in with the EKG already beeping; chromatic aberration pulse = shock
            t = 0f;
            while (t < 1.8f)
            {
                t += Time.deltaTime;
                SetFade(1f - t / 1.8f);
                if (chroma != null) chroma.intensity.value = Mathf.Lerp(1f, 0f, t / 1.8f);
                yield return null;
            }
            SetFade(0f);
            if (walker != null) walker.movementLocked = false;

            yield return new WaitForSeconds(2.0f);
            BuildChartText();
            if (sound != null) sound.HospitalTheme();
            yield return new WaitForSeconds(3.0f);
            BuildMotherText();
            yield return new WaitForSeconds(7.0f);
            ShowReflection();
        }

        void BuildChartText()
        {
            var gm = GameManager.Instance;
            Renderer board = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.name == "Hospital_ChartBoard") { board = r; break; }
            if (board == null) return;
            string txt = "<b>PACIENT: " + gm.fatherName.ToUpperInvariant() + "</b>\nVarsta: " + gm.fatherAge +
                         "\nAdus cu Ambulanta #04\n<color=#a00000>Intarziata in trafic</color>\nStare: CRITICA";
            var b = board.bounds;
            TextFactory.Create("ChartText", txt, null, new Vector3(b.center.x, b.center.y - 0.03f, b.max.z + 0.004f),
                Quaternion.Euler(0f, 180f, 0f), new Vector2(0.42f, 0.22f), new Color(0.08f, 0.08f, 0.1f),
                TextAlignmentOptions.TopLeft, true);
            // brief shock flash on the name reveal
            StartCoroutine(Pulse());
        }

        IEnumerator Pulse()
        {
            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; if (chroma != null) chroma.intensity.value = Mathf.Sin(t / 1.5f * Mathf.PI) * 0.8f; yield return null; }
            if (chroma != null) chroma.intensity.value = 0f;
        }

        void BuildMotherText()
        {
            var gm = GameManager.Instance;
            var mother = WristWatch.FindByName("Mother");
            Vector3 head = mother != null ? mother.position + new Vector3(0f, 2.05f, 0.25f) : new Vector3(199f, 2.05f, -1.3f);
            var rot = Quaternion.Euler(0f, 180f, 0f);
            TextFactory.Panel("MotherPanel", null, head + new Vector3(0, 0, 0.01f), rot, new Vector2(1.7f, 0.5f), new Color(0, 0, 0, 0.6f), true);
            TextFactory.Create("MotherText",
                "<b>" + gm.motherName + ":</b>\n\"" + gm.fatherName.Split(' ')[0] + "... ambulanta a intarziat cateva secunde critice, blocata in trafic.\"",
                null, head, rot, new Vector2(1.6f, 0.42f), new Color(1f, 0.95f, 0.85f), TextAlignmentOptions.Center, true);
        }

        void ShowReflection()
        {
            var gm = GameManager.Instance;
            string txt =
                "<b>Ce ai vazut la suprafata:</b> o intarziere banala.\n" +
                "<b>Ce era dincolo de suprafata:</b> tatal tau.\n\n" +
                "Te-ai uitat la ceas " + Mathf.RoundToInt(gm.watchLookSeconds) + " s si ai gesticulat nervos de " + gm.angryGestures + " ori.\n\n" +
                "<size=70%>Apasa A (sau R) pentru a relua scena si a actiona altfel.</size>";
            ShowHud(txt, 0f, 1.15f, 0.62f, 2.0f, -0.3f);
            reflectionShown = true;
        }
    }
}
