# Plunderlust

High Seas Tactical Pirate Adventure and Naval Simulation Engine

Plunderlust is a stylized low-poly naval tactical action RPG and sailing simulation developed in Unity 6 using the Universal Render Pipeline (URP). Built for both desktop and mobile platforms, Plunderlust features multi-point buoyancy physics, Gerstner ocean wave dynamics, wind aerodynamics, localized subsystem damage, tactical broadside combat, an open-world chunk streaming pipeline, port trading economies, and a 6-tier ship progression system.

---

## Table of Contents

- Overview
- Key Gameplay Features
- Ship Progression Tiers
- Technical Architecture
  - Locomotion and Buoyancy Engine
  - Gerstner Ocean Simulation and Wind System
  - Subsystem Integrity and Flooding Mechanics
  - Naval Ballistics and Combat
  - Infinite World Streaming and Floating Origin
  - Port Economy and Tavern Crew Recruitment
  - Dual-Platform UI and Input Architecture
- Control Schemes
  - Desktop Controls
  - Mobile Touch Controls
- Project Structure
- Scenes
- Installation and Setup
  - Prerequisites
  - Opening the Project
  - Building for Windows
  - Building for Android
- Performance and Mobile Optimizations
- Third-Party Asset Attribution
- License

---

## Overview

Plunderlust casts the player as a stranded mariner aboard a humble leaky rowboat who must navigate dynamic seas, battle hostile corsairs and naval fleets, trade commodities across island ports, hire specialized tavern officers, and upgrade vessels up to legendary dreadnoughts and spectral ghost ships.

The project emphasizes mechanical depth over arcade abstractions: ships obey realistic hydrodynamic drag, sail efficiency varies based on apparent wind angles, cannon fire follows ballistic projectile trajectories with variable spread, and damage is partitioned across distinct physical ship components rather than a single monolithic health pool.

---

## Key Gameplay Features

- Hydrodynamic Sailing Physics: Vessels respond to water resistance, angular damping, rudder deflections, rowing strokes, and wind vectors.
- Gerstner Wave Displacements: Real-time mathematical wave calculations driving both visual ocean meshes and physics buoyancy probes simultaneously.
- Subsystem Damage Model: Cannonballs inflict localized damage to the Hull, Rigging and Sails, Rudder, and Gun Deck, dynamically altering ship handling and firing capacity.
- Progressive Flooding and Bilge Management: Breached hulls take on ocean water, increasing displacement mass and causing the vessel to list, wallow, and eventually sink if not repaired.
- Tactical Ballistics: Broadside and swivel gun ballistics featuring parabolic arcs, travel times, splash effects on misses, and projectile pooling for zero-allocation performance.
- Dynamic Port Economy: Port Haven, Smuggler's Cove, and Tortuga feature localized supply and demand markets for trade goods including Rum, Sugar, Spices, Tobacco, Canvas, and Shot.
- Crew Management and Officers: Hire specialist officers (Sailing Master, Master Gunner, Ship's Carpenter, Quartermaster) providing passive stat enhancements and mechanical perks.
- Marine Wildlife and Atmosphere: Living ocean populated by dolphins, sharks, sea birds, and flotsam salvage crates.
- Cross-Platform Design: Unified codebase supporting standard desktop keyboard/mouse setups and specialized mobile touch layouts with virtual rudder wheels and engine order telegraphs.

---

## Ship Progression Tiers

Plunderlust features six distinct vessel classes defined through ScriptableObject hull profiles:

1. Tier 1: Leaky Rowboat (Tide Skimmer)
   - Role: Fragile starter vessel equipped with oars and a single forward swivel gun.
   - Propulsion: Pure rowing propulsion. Immune to headwind penalties; low top speed and high vulnerability to ocean leaks.
   - Crew Capacity: 2

2. Tier 2: Coastal Sloop (The Barnacle)
   - Role: Nimble single-masted fore-and-aft rigged vessel ideal for shallow waters.
   - Armament: Single port and starboard light guns plus a bow swivel gun.
   - Crew Capacity: 6

3. Tier 3: Armed Cutter (Wave Cutter)
   - Role: Fast pursuit vessel with deep keel and enhanced sail plan.
   - Armament: Dual port and starboard broadside cannons.
   - Crew Capacity: 10

4. Tier 4: Jamaican Sloop / Brigantine (Fortune's Favor)
   - Role: Heavy two-masted square-rigged warship with reinforced oak framing.
   - Armament: Quad broadside batteries with substantial hull durability.
   - Crew Capacity: 16

5. Tier 5: Royal Frigate (HMS Thunderer)
   - Role: Multi-deck naval warship capable of crushing pirate flotillas with devastating cannon volleys.
   - Armament: Full gun deck batteries with heavy iron plating.
   - Crew Capacity: 24

6. Tier 6: Spectral Ghost Ship (The Flying Dutchman)
   - Role: Supernatural vessel featuring spectral hull materials, ethereal lanterns, and formidable combat capabilities.
   - Armament: Spectral cursed cannons and rapid turn rates.
   - Crew Capacity: 30

---

## Technical Architecture

The project is structured under the `Plunderlust` root namespace and split into specialized submodules:

```
Assets/Scripts/
├── AI/           Autonomous vessel behaviors and combat state machines
├── Audio/        Positional and procedural nautical sound management
├── Camera/       Angled tactical naval camera controllers
├── Cargo/        Inventory management and commodity holds
├── Combat/       Broadside firing, ballistics, and projectile pooling
├── Core/         Data transfer objects, interfaces, and persistence
├── Crew/         Officer stats, perk assignment, and visual crew markers
├── Factions/     Faction alignment, reputation, and pennant flags
├── Loot/         Floating flotsam, cargo crates, and shipwreck salvage
├── Ocean/        Gerstner wave math, buoyancy probes, and wake trails
├── Port/         Tavern recruiting, commodity markets, and haven docks
├── Ship/         Locomotion, mobile input bridge, and repair logic
├── Shipyard/     Vessel customization, drydock repair, and ship upgrading
├── Subsystems/   Localized ship subsystem hit detection and flooding
├── UI/           Dual-mode desktop/mobile HUD, minimap, and sea chart
├── Wind/         True/apparent wind vector calculations
└── World/        Chunk streaming, procedural ports, and atoll builders
```

### Locomotion and Buoyancy Engine

- BuoyancyBody.cs: Samples water height dynamically across an array of hull sample points. Each probe computes buoyant force proportional to submersion depth and dampens vertical velocity to prevent oscillation.
- ShipLocomotionController.cs: Drives ship movement via physics forces applied directly to the Rigidbody. Computes rudder turning moments, forward propulsion based on throttle setting and sail trim, and bank angles during tight turns.
- Dynamic Flooding Mass: When the hull integrity drops below critical thresholds, flooding mass is dynamically added to the Rigidbody, lowering the waterline and reducing maneuverability.

### Gerstner Ocean Simulation and Wind System

- OceanWaveManager.cs: Implements multi-harmonic Gerstner wave equations calculating three-dimensional vertex displacements (X, Y, Z) and surface normal vectors based on amplitude, wavelength, speed, and directional vectors.
- WaterGrid.cs: Manages ocean mesh geometry centered around the active player, updating vertex heights to align with the underlying wave mathematics.
- WindManager.cs: Simulates regional wind velocity and heading. Ships calculate apparent wind angle relative to vessel heading to determine sail propulsion efficiency.

### Subsystem Integrity and Flooding Mechanics

- ShipSubsystemController.cs: Tracks four independent subsystem modules:
  - Hull: Structural strength; integrity loss causes progressive waterline flooding.
  - Sails and Rigging: Sail condition; damage drastically reduces maximum sailing speed.
  - Rudder: Steering responsiveness; damage reduces turning torque and induces drift.
  - Gun Deck: Artillery integrity; damage increases cannon reload times and can disable broadside cannons.
- EmergencyRepairsController.cs: Implements active repair mechanics allowing players to expend lumber and crew labor to patch leaks and restore subsystem functionality.

### Naval Ballistics and Combat

- NavalCombatController.cs: Coordinates port and starboard broadside batteries as well as bow/stern swivel guns. Calculates firing arcs, cooldowns, and crew reload cycles.
- CannonballProjectile.cs: Simulates physical projectile motion under gravity, water entry impacts, and collision tests against target ship subsystem bounding colliders.
- ProjectilePool.cs: High-capacity object pool reusing cannonball GameObjects and visual effect prefabs to prevent garbage collection allocation spikes during engagements.

### Infinite World Streaming and Floating Origin

- WorldChunkManager.cs: Divides the game sea into a modular coordinate grid, instantiating and unloading environment chunks, islands, and wildlife relative to player position.
- FloatingOrigin.cs: Resets world coordinate space when the player moves far from the origin, ensuring floating-point precision remains stable across long voyages.
- IslandLandmassBarrier.cs: Employs steering force fields and perimeter colliders around island landmasses to prevent player and NPC ships from running aground into geometry.

### Port Economy and Tavern Crew Recruitment

- DynamicMarketEconomy.cs: Simulates fluctuating commodity price indices influenced by supply, demand, and localized trade routes.
- PortIslandHaven.cs: Handles docking zones, drydock ship repairs, ship purchasing, and cargo unloading.
- Tavern Recruitment: Allows players to contract specialist officers who grant modifiers to sail repair speed, cannon reload rates, and trading margins.

### Dual-Platform UI and Input Architecture

- ShipPaperDollUI.cs: Visual tactical display illustrating real-time subsystem integrity, bilge water levels, and speed readings.
- NavalMinimapAndChart.cs & SeaChartMapUI.cs: Real-time compass rose displaying wind heading, player orientation, detected vessels, and uncharted islands.
- MobileInputBridge.cs: Connects the virtual steering wheel, throttle slider (REV, ANCH, HALF, FULL), and touch fire buttons to the core ship movement controllers without modifying physics code.

---

## Control Schemes

### Desktop Controls

- Steer Left / Right: A / D or Left Arrow / Right Arrow
- Throttle Up / Down: W / S or Up Arrow / Down Arrow (Reverse, Anchor, Half Sail, Full Sail)
- Fire Port Broadside: Q
- Fire Starboard Broadside: E
- Fire Swivel Gun: Spacebar or Left Mouse Button
- Emergency Repairs: R
- Open Sea Chart: M or Tab
- Open Inventory / Hold: I
- Dock at Port / Enter Drydock: F or Enter
- Pause Game: Escape

### Mobile Touch Controls

- Virtual Helm (Bottom Left): Touch-and-drag rotary wheel for continuous rudder control.
- Engine Order Telegraph (Bottom Left): Vertical four-position slider (REV, ANCH, HALF, FULL).
- Broadside Buttons (Bottom Right): Dedicated PORT FIRE and STARBD FIRE touch buttons.
- Swivel Button (Bottom Right): SWIVEL FIRE button for direct forward aimed shots.
- Quick Repair (Right Center): Instant-tap repair button with status timer ring.
- Tactical Minimap (Top Right): Tap minimap to open the full-screen interactive Sea Chart.
- Safe Area Adapter: Automatically offsets UI anchors to account for device notches and camera punch-holes.

---

## Project Structure

```
Plunderlust/
├── Assets/
│   ├── Editor/               Editor tools, build scripts, and settings fixers
│   ├── kenney_pirate-kit/    3D models, textures, and maritime visual assets
│   ├── Scenes/
│   │   ├── MainMenu.unity    3D live diorama title scene with shipyard showcase
│   │   └── SampleScene.unity Main open-world naval gameplay scene
│   ├── Scripts/              C# gameplay and engine architecture
│   └── Settings/             URP asset profiles, renderers, materials, and ship SOs
├── ProjectSettings/          Unity project configuration, physics, and input tables
├── README.md                 Comprehensive technical documentation
└── .gitignore                Standard Unity version control exclusion rules
```

---

## Scenes

1. MainMenu.unity (Build Index 0)
   - Features a fully-baked 3D harbor diorama with a moored pirate brigantine, shoreline beach, palm trees, and animated ocean water.
   - Contains the interactive Shipyard Showcase turntable where players can inspect all six ship classes.
   - Provides audio, display, and graphics fidelity configuration menus.

2. SampleScene.unity (Build Index 1)
   - The primary open-world gameplay scene.
   - Pre-baked with major island landmarks (Port Haven, Smuggler's Cove, Tortuga Haven, and Treasure Atolls) to ensure immediate visual representation in edit mode and runtime.
   - Hosts the ocean wave simulation, dynamic weather and wind cycles, wildlife director, and combat manager.

---

## Installation and Setup

### Prerequisites

- Unity Version: Unity 6 (6000.5.6f1 or later recommended).
- Render Pipeline: Universal Render Pipeline (URP).
- Git LFS: Recommended if tracking large 3D binaries or audio assets.

### Opening the Project

1. Clone or download the repository to your local machine:
   ```bash
   git clone https://github.com/YourUsername/Plunderlust.git
   ```
2. Launch Unity Hub.
3. Click Open and navigate to the root directory of the cloned project.
4. Select Unity 6 (6000.5.x) as the editor version.
5. Allow Unity to import asset packages and compile project scripts.

### Running in the Editor

1. Open `Assets/Scenes/MainMenu.unity`.
2. Press the Play button in the Unity Editor toolbar.
3. From the main menu, click New Voyage to transition into the gameplay scene.

### Building for Windows

1. In Unity, select File > Build Profiles (or Build Settings).
2. Ensure both scenes are included in the build list in order:
   - `Assets/Scenes/MainMenu.unity` (Index 0)
   - `Assets/Scenes/SampleScene.unity` (Index 1)
3. Set the target platform to Windows (x86_64).
4. Click Build and select your desired output directory.

### Building for Android

1. Open File > Build Profiles (or Build Settings) and switch platform to Android.
2. In Project Settings > Player > Android:
   - Target API Level: Android 13.0 (API Level 33) or Android 14.0 (API Level 34).
   - Graphics APIs: OpenGLES3 (recommended for universal Adreno / Mali GPU compatibility) or Vulkan.
   - Default Orientation: Landscape Left (auto-rotation to Landscape Right enabled).
3. Connect your Android device via USB with USB Debugging enabled.
4. Click Build and Run to generate and install the APK package directly to your device.

---

## Performance and Mobile Optimizations

Plunderlust has been optimized for stable framerates on mobile hardware:

- Low-Poly Geometry: Lightweight polygonal models from the Kenney Pirate Kit utilizing a unified texture palette to minimize draw calls via dynamic batching and GPU instancing.
- Point Filter Texture Sampling: Textures use Point (no filter) sampling with uncompressed settings to retain crisp retro pixel details while lowering VRAM overhead.
- No-Allocation Projectile Pooling: Pre-allocated object pools for cannonballs, water impact particles, and damage numbers eliminate runtime garbage collection pauses during naval battles.
- Optimized Gerstner Wave Calculations: Mathematical wave offsets are evaluated only at active physics probe locations rather than across the entire ocean mesh every frame.
- Screen Orientation and Safe Area Handling: `SafeAreaFitter.cs` dynamically conforms canvas elements to display cutouts, preventing UI buttons from overlapping notches or camera apertures.

---

## Third-Party Asset Attribution

- 3D Models and Textures: Kenney Pirate Kit (CC0 1.0 Universal - Public Domain Dedication).
- Fonts: Cinzel and Montserrat (Open Font License).

---

## License

This project is licensed under the MIT License. See the LICENSE file for details.
