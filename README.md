# 🥽 Beyond The Surface (VR)

**Beyond The Surface** is an interactive Virtual Reality (VR) psychological and educational experience built in Unity. It explores human emotions, impulse control, and empathy through interconnected first-person scenarios and real-time perspective shifts.

---

## 📄 Design & Project Documentation

To avoid duplicating narrative specifications, storyboards, and asset checklists in this repository, all detailed design documents are maintained in Google Docs:

* 📘 **[Tema 4: State of the Art & Concept Overview](https://docs.google.com/document/d/1d-nIIZhbPmt3I08HsGRViVYe53sbYDcReCTyRhNGcoQ/edit?usp=sharing)** — *Vision, psychological rationale, similar VR works, and core mechanics.*
* 📗 **[Tema 5: Main Modules & Technical Design](https://docs.google.com/document/d/1DOPDS3eZHqFy5UXhNWhShu2oXauYvMbrf8QbMBnkx5s/edit?usp=sharing)** — *Complete User Story Map, UX Journey Map, First-Person POV Storyboards, and the Asset Creation Checklist.*

---

## ⚙️ Technical Architecture

Instead of passive $360^\circ$ video playback, the project is architected around three core technical systems in Unity:

1. **Dynamic Personal Data Injection (`PersonalDataManager`):**
   Captures user input during the calibration phase and dynamically binds strings to `TextMeshPro` 3D world-space elements across subsequent scenes via `ScriptableObjects`.
2. **Behavioral & Frustration Telemetry (`EmpathyEvaluator`):**
   Tracks implicit and explicit VR metrics—including head-gaze raycasting (`Camera.main`), controller velocity/agitation, proximity triggers, and dialogue/interaction choices—to compute the player's emotional profile and export session logs for the external evaluator view.
3. **Bio-Emotional Post-Processing & Shader Transitions (`EmotionVFXController`):**
   Manipulates URP Volume Overrides (vignette, chromatic aberration, color grading) and custom Shader Graph materials (environment dissolve and time-freeze transitions) in real time based on the active narrative state.

---

## 📂 Repository Structure

```text
BeyondTheSurface-VR/
├── Assets/
│   ├── _Project/
│   │   ├── Art/               # Custom 3D models, scanned props, and materials
│   │   ├── Audio/             # Spatial audio clips, voiceovers, and Foley
│   │   ├── Prefabs/           # XR Origin rig, interactive props, and UI canvases
│   │   ├── Scenes/            # Modular scenes for each stage of the experience
│   │   ├── Scripts/
│   │   │   ├── Core/          # State machines, data persistence, and scene loaders
│   │   │   ├── Telemetry/     # Gaze tracking, reaction timers, and CSV/JSON export
│   │   │   ├── Interactions/  # Custom XRI grab/socket behaviours and wristwatch HUD
│   │   │   └── VFX/           # Shader property controllers and URP volume blenders
│   │   └── Shaders/           # Custom Shader Graph files (Dissolve, TimeFreeze)
├── Packages/                  # Unity Package Manager manifest (XRI, URP, Meta XR)
└── ProjectSettings/           # XR Plug-in Management & URP rendering configurations
```

---

## 🚀 Setup & Installation

### Requirements
* **Unity Version:** Unity `2022.3 LTS` or `Unity 6` (with **Universal Render Pipeline - URP**).
* **Modules:** Android Build Support (OpenJDK, SDK & NDK) for standalone Meta Quest builds, or Windows Build Support for PCVR.
* **Hardware:** Meta Quest 2 / Pro / 3 (via Quest Link or APK) **OR** keyboard/mouse using the integrated **XR Device Simulator**.

### Running the Project Locally
1. **Clone the repository:**
   ```bash
   git clone https://github.com/<your-org-or-username>/beyond-the-surface-vr.git
   cd beyond-the-surface-vr
   ```
2. **Open in Unity Hub:**
   Click **Add -> Add project from disk** and select the root folder.
3. **Configure XR Plug-in Management:**
   Navigate to `Edit > Project Settings > XR Plug-in Management` and ensure **OpenXR** (or **Oculus**) is enabled for your target build platform.
4. **Play in Editor (No Headset Required):**
   Open the startup scene in `Assets/_Project/Scenes/`, ensure the `XR Device Simulator` prefab is active in the Hierarchy, and press **Play**.

---

## 🎮 Controls (VR & Simulator)

| Action | Meta Quest Controllers | XR Device Simulator (Keyboard/Mouse) |
| :--- | :--- | :--- |
| **Look / Rotate** | Physical Head Movement / Right Thumbstick | Right-Click + Drag Mouse |
| **Move / Locomotion** | Left Thumbstick | `W` `A` `S` `D` |
| **Grab / Interact** | Grip Button (`Axis1D.PrimaryHandTrigger`) | `G` (while hovering with hand) |
| **UI Click / Confirm** | Index Trigger (`Axis1D.PrimaryIndexTrigger`) | Left-Click |
| **Check Wristwatch** | Raise Left Wrist | Hold `Left Shift` (Left Hand control) + Rotate |

---

## 👥 Team & Contributors

* **<Numele Tău>** — *Role / Module*
* **<Coleg 1>** — *Role / Module*
* **<Coleg 2>** — *Role / Module*

---

*Developed for the **Introduction to Mixed Realities** course.*