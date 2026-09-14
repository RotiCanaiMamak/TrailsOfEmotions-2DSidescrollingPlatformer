# Trails of Emotions

A relaxing 2D side-scrolling platformer about navigating emotional landscapes, finding inner balance, and learning to understand feelings through movement and play.

Players guide Lina through a shifting world shaped by her emotions. Each biome represents a different emotional state, presenting unique environmental effects, traversal challenges, and visual atmosphere.

## Overview

*Trails of Emotions* combines casual platforming with emotional awareness themes. The game focuses on flow, timing, adaptation, and low pressure progression rather than intense competition.

As players travel through emotional biomes, they must maintain forward movement while managing an Emotion Meter. Higher emotional intensity changes the environment, introducing stronger visual effects, more obstacles, and more difficult movement conditions.

## Core Features

- 2D side-scrolling platforming with running, jumping, gliding, groundpounding, and zipline traversal
- Procedurally generated terrain chunks and terrain sequences
- Emotion-based biomes: Anger, Anxiety and Sadness
- Dynamic Emotion Meter with multiple intensity thresholds
- Biome changes based on the player's current emotional state
- Environmental obstacles that affect movement, visibility, gravity, and input timing
- Regulation mechanics that reduce emotion and help the player recover
- Runtime-generated ziplines with sagging rope geometry
- Biome specific visual effects, particles, lighting, vignettes, and material effects
- Score tracking, pause functionality, scene transitions, and game over cutscenes

## Gameplay

The game is built around continuous side-scrolling traversal. Players run, jump, glide, avoid obstacles, and adapt to changing environmental conditions.

Different emotion biomes affect gameplay in different ways, such as:

- Altered gravity
- Delayed movement input
- Reduced visibility
- Increased obstacle density
- Distorted visual effects

## Emotion Meter

The Emotion Meter represents Lina's emotional intensity during gameplay.

As the meter increases:

- The environment becomes more visually intense
- Controls may become more difficult to manage
- Obstacles become more demanding
- Biome effects become stronger

Managing the meter encourages players to stay calm, adapt to challenges, and maintain balance while progressing through each level.

## Genre

- Relaxing
- Casual
- 2D Side-Scrolling Platformer
- Educational

## Target Audience

*Trails of Emotions* is designed for:

- Casual players looking for a calm and meaningful game experience
- Fans of 2D platformers with creative movement mechanics
- Gen Z and millennial players interested in games teaching emotional well-being
- Teenagers learning about emotions and emotional regulation
- Educators seeking interactive tools for emotional awareness

## Built With

- Unity
- C#
- Unity 2D Physics
- Unity UI
- TextMeshPro

## How to Run

1. Clone or download this repository.
2. Open Unity Hub.
3. Select **Add** and choose the project folder.
4. Open the project using Unity Editor `6000.3.6f1`.
5. Open `Assets/Scenes/StartScene`.
6. Press the Play button in the Unity Editor.

## Technical Implementation

### Procedural Terrain Generation

The game uses a chunk based terrain system to generate the world during play.

- `TerrainManager` maintains a queue of active terrain chunks.
- New terrain is spawned ahead of the player while old chunks are removed behind them.
- `TerrainSequencePlanner` keeps multi chunk terrain sequences together, allowing intentional terrain patterns rather than fully random generation.
- Terrain definitions, sequence definitions, and spawn weights are configurable through Unity data assets.
- Terrain features, obstacles, regulation objects, ability orbs, and ziplines are placed dynamically on valid terrain surfaces.
- A world shift system preserves gameplay state when terrain is repositioned, helping avoid floating point precision issues during long runs.

### Emotion Meter and Biome State System

The Emotion Meter is the central gameplay state system.

- Emotion rises continuously while the game is running and can increase further when the player encounters obstacles.
- The meter uses configurable thresholds: Normal, Pre, Mid, and Peaked.
- State changes are broadcast through Unity events, allowing UI, biome selection, effects, and gameplay systems to react independently.
- Multiple systems can apply and remove their own emotion modifiers safely through source based runtime tracking.
- `BiomeManager` selects biome pools according to the current Emotion Meter phase and manages transitions between emotional biome families.

### Player Movement and Physics

The player controller is built around `Rigidbody2D` physics and custom movement logic.

- Raycasts are used for grounded checks, slope detection, and terrain alignment.
- Lina can run, jump, glide, groundpound, and travel on ziplines.
- Glide gravity is smoothly blended to prevent abrupt movement changes.
- Groundpounding detects nearby valid targets through 2D physics queries and triggers effects, scoring, and obstacle interactions.
- Movement modifiers can be stacked by gameplay systems to alter speed, gravity, jump force, and forward momentum.
- Input delay obstacles are implemented using buffered input snapshots, allowing anxiety based hazards to delay player input without breaking normal controls.

### Character and Ability Architecture

Character behaviour is implemented through reusable runtime and data components.

- `CharacterData` ScriptableObjects define movement speed, jump force, glide gravity, emotion sensitivity, weakness biome, and emotion limits.
- `CharacterRuntime` applies the selected character's data to the player controller during gameplay.
- Character abilities are separated into reusable ability components.
- Ability orbs can be collected during procedural traversal to charge active abilities.

### Regulation Mechanics

Regulation mechanics represent emotional coping strategies as interactive gameplay systems.

- Anger regulation generates a procedural wave with a custom polygon collider, animated particles, movement effects, and emotion reduction.
- Grounding regulation creates a sequence of collectible circles that launch the player forward, reduce emotion, and clear nearby obstacles on completion.
- Regulation objects integrate with terrain placement through shared interfaces, making them compatible with procedural chunk generation.
- These mechanics give the player recovery tools while reinforcing the game's emotional awareness theme.

### Runtime Zipline Generation

Ziplines are generated dynamically between terrain points.

- `ZiplineBuilder` creates poles and rope segments at runtime.
- The rope follows a configurable sag curve between arbitrary endpoints.
- Arc-length sampling is used to distribute rope chunks consistently across curved ziplines.
- Anchor points and base anchors allow prefabs to align correctly with the terrain.
- Ziplines can be spawned as part of procedural terrain traversal and can include ability orb paths.

### Visual and Environmental Effects

Biome atmosphere is supported by gameplay linked visual effects.

- Biome specific global lighting and volume control
- Heatwave material effects for Anger environments
- Blur and pulse effects for Anxiety environments
- Mist, rain lens, soot, smoke, and ice hail visual systems
- Vignette effects linked to obstacle status
- Particle trails, shockwaves, and obstacle destruction effects
- Parallax scrolling for background depth and movement

## Technical Highlights

- Designed and implemented a modular emotional state system that connects player performance, gameplay difficulty, visual effects, and biome selection.
- Built a procedural terrain pipeline using reusable terrain chunks, weighted definitions, and planned multi chunk sequences.
- Created reusable movement modifier and emotion modifier systems that allow obstacles and regulation mechanics to affect the player without tightly coupling scripts.
- Implemented emotion themed gameplay mechanics, including delayed input, altered movement, environmental hazards, procedural regulation objects, and dynamic ziplines.
- Developed the project in Unity using C#, 2D physics, runtime procedural generation, ScriptableObject configuration, Unity events, and URP visual effects.