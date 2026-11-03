# 🥽 Beyond The Surface (VR)

Beyond The Surface is an interactive Virtual Reality (VR) psychological and educational experience built in Unity. It explores human emotions, impulse control, and empathy through interconnected first-person scenarios and real-time perspective shifts.

> *"An interactive narrative and educational Virtual Reality (VR) experience that explores human emotions and cultivates empathy by placing the user in frustrating everyday situations, followed by revealing the painful emotional truth behind those obstacles."*

---

## 📖 Overview

In the fast-paced rhythm of daily life, people tend to judge surrounding events exclusively through the filter of their own immediate needs—reacting with anger, impatience, or superficial judgment whenever someone or something slows them down.

Beyond The Surface leverages the unique immersion of Virtual Reality to deconstruct egoism and impulsive frustration. Instead of a passive $360^\circ$ video, the application places the player inside interconnected everyday scenarios where their patience is tested, only to shift their perspective and confront them with the invisible human drama behind each obstacle.

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

## 🎭 Core Scenarios & Gameplay Flow

### 0. Onboarding & Subconscious Personalization (`Waiting Room`)
* **Calibration:** Adjusts `XROrigin` height and tests virtual hand interactions via a floating terminal/clipboard.
* **Personal Anchoring:** Through a 3D virtual keyboard or Speech-to-Text input, the user answers seemingly administrative setup questions (including their father's name, mother's name, and ages).
* **Dynamic Injection:** These strings are stored in the `GameManager` (`PlayerPrefs` / `ScriptableObject`) and dynamically injected into 3D TextMeshPro medical bracelets, charts, and NPC dialogues later in the experience.

### 1. Scenario I: The Crosswalk & The ER (`Anger, Rush & Deep Guilt`)
* **The Surface (The Rush):** The player is tasked with crossing the street to reach a crucial appointment before the timer on their virtual wristwatch expires (accompanied by escalating haptic vibrations). Seconds before crossing, a speeding ambulance cuts them off at the crosswalk, causing the timer to hit `00:00 - FAILED`. The system monitors frustration metrics (stepping toward the ambulance, repeatedly mashing the crosswalk button, rapid hand gestures, or staring at the watch).
* **Beyond The Surface (The Twist):** Time freezes, the siren morphs into an ECG flatline/monitor beep, and the player is transported to a hospital Emergency Room (UPU). Guided to Resuscitation Room 1, the player approaches the critical patient brought in by that exact ambulance—only to read **their own father's name** on the medical chart and wristband, while their mother weeps beside the bed over the critical seconds lost in traffic.

### 2. Scenario II: The Supermarket Queue (`Impatience, Judgment & Loneliness`)
* **The Surface (The Delay):** A familiar, timer-free everyday task: grabbing 3–4 groceries (`XRGrabInteractable` bread, milk, fruit) and waiting at the checkout. Ahead in line, an elderly woman slowly counts small coins and chats at length with the cashier about the weather and cake recipes. The player can choose to sigh, tap the counter, trigger impatient dialogue lines (*"Come on, ma'am, some of us have things to do!"*), or wait quietly.
* **Beyond The Surface (The Shift):** As the elderly woman apologizes with a trembling voice and exits, the supermarket walls dissolve (`Dissolve Shader`) into her silent, dimly lit apartment. Through environmental storytelling, the player inspects a framed photo of her deceased husband with a mourning ribbon, two plates set on the table out of habit, and a wall calendar where the only circled entry all week reads: *"Go buy bread — talk to the girl at the register."*

### 3. Scenario III: The Commute & The Mirror Room (`Behavioral Validation & Debriefing`)
* **The Empathy Test:** The player encounters a final tense social situation (e.g., a distressed courier/commuter dropping their belongings while crying on the phone). The system evaluates whether the player's reaction time and prosocial behavior (physically bending down to help pick up the items) have improved compared to Scenarios I and II.
* **The Mirror Room (`Debriefing Room`):** A calm analytical space displaying side-by-side holographic panels (*What You Saw on the Surface* vs. *What Was Beyond the Surface*) alongside an emotional profiling breakdown (Patience, Impulsivity, and Empathy) and an option to replay key moments.
* **Evaluator Spectator View:** Real-time desktop monitoring UI for instructors/psychologists and anonymized session summary exports.

---

## 🛠️ Tech Stack & Tools

* **Engine:** Unity 3D (Universal Render Pipeline - URP)
* **XR Framework:** [Unity XR Interaction Toolkit (XRI)](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest) & [Meta XR All-in-One SDK / XR Device Simulator](https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657)[cite: 1]
* **Target Hardware:** Meta Quest 2 / Meta Quest 3 / PCVR
* **3D Modeling & Animation:** [Blender](https://www.blender.org/) (custom interactive wristwatch, medical chart/wristband, mourning photo frame, shopping basket) & [Adobe Mixamo](https://www.mixamo.com/)[cite: 1]
* **VFX & Shaders:** Unity Shader Graph & URP Volume Overrides (Dynamic emotion post-processing: Red Vignette for anger/rush, Chromatic Aberration for shock, Desaturation for loneliness, Time-Freeze & Environment Dissolve shaders)
* **Audio & Voice:** Spatial Audio, Audacity (custom Foley & heartbeat recordings), [ElevenLabs](https://elevenlabs.io/) (AI voice acting), [Suno AI](https://suno.com/) (ambient piano score)
* **GenAI & Photogrammetry:** Meshy AI / Luma AI (3D props), Midjourney / DALL-E 3 (2D textures & photos)

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

### Suggested Unity Structure

```text
Assets/
├── _Project/
│   ├── Art/
│   │   ├── Models/          # Custom Blender props & scanned/Store 3D assets
│   │   ├── Materials/       # Dissolve & environment materials
│   │   └── UI/              # Virtual keyboard, medical chart layouts, calendar textures
│   ├── Audio/
│   │   ├── Foley/           # Custom footsteps, sighs, heartbeat recordings
│   │   ├── Voiceovers/      # ElevenLabs NPC dialogues & monologues
│   │   └── Music_SFX/       # Ambient piano, Doppler siren, ECG monitor, barcode scanner
│   ├── Prefabs/             # Interactive XR props, NPCs, Wristwatch HUD
│   ├── Scenes/
│   │   ├── 00_Onboarding_WaitingRoom.unity
│   │   ├── 01_Street_And_Hospital_UPU.unity
│   │   ├── 02_Supermarket_And_Apartment.unity
│   │   ├── 03_EmpathyTest_Commute.unity
│   │   └── 04_Debriefing_MirrorRoom.unity
│   ├── Scripts/
│   │   ├── Core/            # GameManager, PersonalDataStorage, TelemetryExporter
│   │   ├── EmpathyEngine/   # FrustrationTracker, DecisionEvaluator, PostProcessingController
│   │   ├── Interactions/    # WristwatchTimer, GrabItems, VirtualKeyboardInput
│   │   └── Transitions/     # TimeFreezeTransition, DissolveRoomController
│   └── VFX_Shaders/         # URP Volume Profiles, Dissolve Shader Graph, Volumetric Lights
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

