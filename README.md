# 🥽 Beyond The Surface — An Empathy & Emotional Awareness VR Experience

> *"An interactive narrative and educational Virtual Reality (VR) experience that explores human emotions and cultivates empathy by placing the user in frustrating everyday situations, followed by revealing the painful emotional truth behind those obstacles."*

---

## 📖 Overview

In the fast-paced rhythm of daily life, people tend to judge surrounding events exclusively through the filter of their own immediate needs—reacting with anger, impatience, or superficial judgment whenever someone or something slows them down.

**Beyond The Surface** leverages the unique immersion of Virtual Reality to deconstruct egoism and impulsive frustration. Instead of a passive $360^\circ$ video, the application places the player inside interconnected everyday scenarios where their patience is tested, only to shift their perspective and confront them with the invisible human drama behind each obstacle.

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

## 📂 Project Architecture (Suggested Unity Structure)

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
