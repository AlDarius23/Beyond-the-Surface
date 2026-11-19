using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeyondTheSurface
{
    /// <summary>
    /// First-person walking for VR: left stick walks (relative to where you look), click the left stick (or Shift) to run,
    /// right stick snap-turns. Replaces teleportation completely. Also plays footsteps.
    /// Keyboard fallback for desktop testing: WASD + Shift, Q/E to turn.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CharacterController))]
    public class VRWalkLocomotion : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 2.0f;
        public float runSpeed = 3.8f;
        public float gravity = -9.81f;
        [Tooltip("Set by the scenario while text is shown / during fades")]
        public bool movementLocked;

        [Header("Turning (right stick)")]
        public bool snapTurn = true;
        public float snapAngle = 45f;
        public float smoothTurnSpeed = 100f;

        [Header("Footsteps")]
        public AudioClip[] streetWalk;
        public AudioClip[] streetRun;
        public AudioClip[] wetLayer;
        public AudioClip[] tileWalk;
        public AudioClip[] tileRun;
        public float stepVolume = 0.5f;
        [Tooltip("Positions with x above this are the hospital (tile floor)")]
        public float hospitalMinX = 150f;

        public bool IsMoving { get; private set; }
        public bool IsRunning { get; private set; }

        XROrigin rig;
        CharacterController cc;
        Transform cam;
        InputAction move, turn, run, kbTurn;
        float vy;
        bool turnArmed = true;
        bool runLatched;
        float stepAcc;
        AudioSource feet;
        Vector3 lastSafe;
        System.Random rnd = new System.Random();

        void Awake()
        {
            rig = GetComponent<XROrigin>();
            cc = GetComponent<CharacterController>();

            move = new InputAction("BTS Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            move.AddBinding("<XRController>{LeftHand}/thumbstick");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");

            turn = new InputAction("BTS Turn", InputActionType.Value, expectedControlType: "Vector2");
            turn.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
            turn.AddBinding("<XRController>{RightHand}/thumbstick");

            kbTurn = new InputAction("BTS KbTurn", InputActionType.Value, expectedControlType: "Axis");
            kbTurn.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");

            run = new InputAction("BTS Run", InputActionType.Button);
            run.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");
            run.AddBinding("<XRController>{LeftHand}/thumbstickClicked");
            run.AddBinding("<Keyboard>/leftShift");
        }

        void OnEnable() { move.Enable(); turn.Enable(); kbTurn.Enable(); run.Enable(); }
        void OnDisable() { move.Disable(); turn.Disable(); kbTurn.Disable(); run.Disable(); }

        void Start()
        {
            cam = rig != null && rig.Camera != null ? rig.Camera.transform : Camera.main.transform;
            cc.radius = 0.25f; cc.stepOffset = 0.3f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
            DisableTeleportAndOtherLocomotion();

            var f = new GameObject("Footsteps");
            f.transform.SetParent(transform, false);
            feet = f.AddComponent<AudioSource>();
            feet.playOnAwake = false; feet.spatialBlend = 0f; feet.volume = 1f;
            lastSafe = transform.position;
        }

        void DisableTeleportAndOtherLocomotion()
        {
            string[] kill = { "TeleportationProvider", "ContinuousMoveProvider", "DynamicMoveProvider", "SnapTurnProvider",
                              "ContinuousTurnProvider", "GrabMoveProvider", "TwoHandedGrabMoveProvider", "ControllerInputActionManager",
                              "CharacterControllerDriver", "GravityProvider", "JumpProvider" };
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb == this) continue;
                string n = mb.GetType().Name;
                foreach (var k in kill) if (n == k) { mb.enabled = false; break; }
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.Contains("Teleport Interactor") || n == "Teleport Stabilized Origin" || n.Contains("Climb Teleport"))
                    t.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;

            // keep the capsule under the head (room-scale safe)
            Vector3 headLocal = rig != null ? rig.CameraInOriginSpacePos : cam.localPosition;
            float h = Mathf.Clamp(headLocal.y, 1.0f, 2.1f);
            cc.height = h;
            cc.center = new Vector3(headLocal.x, h * 0.5f + cc.skinWidth, headLocal.z);

            // turning
            float tx = turn.ReadValue<Vector2>().x + kbTurn.ReadValue<float>();
            if (snapTurn)
            {
                if (Mathf.Abs(tx) > 0.75f && turnArmed) { Rotate(Mathf.Sign(tx) * snapAngle); turnArmed = false; }
                else if (Mathf.Abs(tx) < 0.3f) turnArmed = true;
            }
            else if (Mathf.Abs(tx) > 0.2f) Rotate(tx * smoothTurnSpeed * dt);

            // walking
            Vector2 v = movementLocked ? Vector2.zero : move.ReadValue<Vector2>();
            if (v.magnitude < 0.15f) { v = Vector2.zero; runLatched = false; }
            if (run.WasPressedThisFrame()) runLatched = true;
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) runLatched = v != Vector2.zero;
            v = Vector2.ClampMagnitude(v, 1f);

            Vector3 fwd = cam.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = cam.up; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            IsRunning = runLatched && v != Vector2.zero;
            float speed = IsRunning ? runSpeed : walkSpeed;
            Vector3 horiz = (fwd * v.y + right * v.x) * speed;

            if (cc.isGrounded) vy = -2f; else vy += gravity * dt;
            Vector3 before = transform.position;
            if (cc.enabled) cc.Move((horiz + Vector3.up * vy) * dt);
            Vector3 delta = transform.position - before; delta.y = 0f;

            IsMoving = v != Vector2.zero && delta.magnitude > 0.0005f;
            if (cc.isGrounded) lastSafe = transform.position;
            if (transform.position.y < -15f) { cc.enabled = false; transform.position = lastSafe + Vector3.up * 0.2f; cc.enabled = true; vy = 0f; }

            // footsteps by distance walked
            if (IsMoving && cc.isGrounded)
            {
                stepAcc += delta.magnitude;
                float stride = IsRunning ? 1.05f : 0.75f;
                if (stepAcc >= stride) { stepAcc = 0f; Step(); }
            }
            else stepAcc = Mathf.Min(stepAcc, 0.5f);
        }

        void Rotate(float angle)
        {
            if (rig != null) rig.RotateAroundCameraUsingOriginUp(angle);
            else transform.RotateAround(cam.position, Vector3.up, angle);
        }

        AudioClip Pick(AudioClip[] set)
        {
            if (set == null || set.Length == 0) return null;
            return set[rnd.Next(set.Length)];
        }

        void Step()
        {
            if (feet == null) return;
            bool hospital = cam.position.x > hospitalMinX;
            AudioClip main = hospital ? Pick(IsRunning ? tileRun : tileWalk) : Pick(IsRunning ? streetRun : streetWalk);
            feet.pitch = 0.92f + (float)rnd.NextDouble() * 0.16f;
            if (main != null) feet.PlayOneShot(main, stepVolume * (0.85f + (float)rnd.NextDouble() * 0.15f));
            if (!hospital)
            {
                var wet = Pick(wetLayer);
                if (wet != null) feet.PlayOneShot(wet, stepVolume * 0.45f);
            }
        }
    }
}
