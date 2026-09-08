Build a focused Unity helicopter flying game for Windows, macOS, and Linux.

Working title: **Hover for Hire**.

Git: git@github.com:anaremore/hover-for-hire.git

The concept combines Crazy Taxi’s quick pickup-and-delivery loop with the grounded, skillful helicopter handling I enjoy in WARDOGS and Arma Reforger. Treat those games as references for feel, not specifications to reproduce exactly.

The goal is a fun standalone game that also helps players practice helicopter control skills on a computer. Prioritize handling, readable controls, camera quality, and satisfying landings.

Start implementing the game, not just describing a design. Make reasonable decisions autonomously, document important assumptions, and build a playable vertical slice before expanding scope.

**Core experience**

Fly a small civilian utility helicopter around a compact island containing a town, harbor, industrial area, hills, and scattered remote destinations.

Accept a job, fly to the pickup, land, load passengers or goods, then deliver them. Earn money and a performance grade based on completion time, landing precision, passenger comfort, and cargo condition.

Sessions should be enjoyable in 10–20 minutes, with fast access to flying and minimal menus.

Make the first helicopter available immediately. Progression should unlock more demanding jobs and locations without making the initial helicopter artificially weak.

**Flight model**

Build a tunable physics-based helicopter with:

* Collective controlling rotor lift demand.
* Cyclic pitch and roll controlling rotor thrust direction and aircraft attitude.
* Pedal/yaw control with main rotor torque compensation.
* Inertia, momentum, aerodynamic drag, and believable acceleration and braking.
* Coupling between bank angle, lift, altitude, and collective demand.
* Payload mass affecting performance and handling.
* Governed rotor speed during normal operation; collective is not a direct altitude or vertical-speed command.
* Stable ground contact and believable hard landings.

A helicopter should retain momentum when the pilot releases the controls. Stopping should require deliberate braking and an approach that accounts for speed and distance.

Use Rigidbody forces and torques during fixed physics steps. Do not implement flight by directly moving or rotating the transform.

Begin with a well-tuned simplified aerodynamic model. Add ground effect and translational lift once basic hover, forward flight, turns, and landing work reliably. Document approximations. Defer complex failure regimes and autorotation until the core model can support them convincingly.

Keep aircraft parameters centralized and editable, preferably using ScriptableObjects.

This is a game and computer control trainer, not a certified aviation simulator.

**Controls**

Support fully rebindable keyboard/mouse and gamepad controls. Structure input so joystick, pedals, and collective hardware can be added without rewriting the flight model.

Suggested keyboard/mouse defaults:

* Mouse: cyclic pitch and roll.
* W/S: alternate keyboard pitch.
* A/D: alternate keyboard roll.
* Q/E: yaw left/right.
* Left Shift/Left Ctrl: collective increase/decrease.
* Hold Alt + mouse: free look.
* V: switch third-person/cockpit.
* R: recenter view.
* Escape: pause.

Keyboard collective should increase or decrease a persistent 0–100% setting and hold that setting when released. Display the current value clearly. Support absolute collective axes for compatible hardware.

Provide two configurable mouse cyclic modes:

1. Relative mouse input with configurable return toward center.
2. Virtual joystick mode, where displacement from a visible center determines cyclic input.

Include sensitivity, inversion, deadzone, response curve, and recenter settings where relevant. Define how simultaneous keyboard and mouse inputs combine without causing abrupt jumps.

Mouse input must behave consistently across frame rates.

When free look is active, mouse movement must only move the camera. Make cyclic behavior during free look explicit and configurable: hold the current command or return toward neutral. Returning from free look must never apply accumulated mouse movement or produce a sudden control jump.

Use appropriate platform-specific fallbacks for shortcuts intercepted by the operating system. Support rebinding and settings persistence.

**Flight assists**

Use the same underlying aircraft physics in every mode.

Provide independently configurable:

* Pitch/roll rate stabilization.
* Auto-level when cyclic is centered.
* Yaw stabilization and torque compensation.
* Hover hold as an explicit, separate function.

Offer Beginner, Standard, and Unassisted presets, while allowing customization.

Clearly distinguish input processing from flight assistance. Turning assists off must remove stabilization forces, not just change sensitivity.

Assists should operate through bounded control commands, never teleport the helicopter or cancel all momentum. Avoid abrupt changes when toggling them. Display active assists on the HUD and record them in challenge results.

Implement basic stabilization first. Add hover hold only after the underlying flight model is stable.

**Cameras**

Default to a polished third-person chase camera with:

* An unobstructed view of the helicopter and landing area.
* Adjustable distance, height, field of view, and smoothing.
* Terrain/building collision handling.
* Free look and smooth recentering.
* Enough responsiveness for precision landing.

Also include a usable cockpit camera with free look and readable essential instruments.

Switching cameras must preserve flight controls and aircraft state. Keep camera movement separate from aircraft physics.

**Game modes**

1. **Free Flight:** Explore with no timer and easily reset at a helipad.
2. **Training:** Short drills for takeoff, hover, yaw control, forward flight, braking, approach, and precision landing.
3. **Delivery Shift:** Complete a sequence of passenger and cargo jobs within a timed session.

Training should introduce one skill at a time and offer immediate retries.

Grade observable outcomes: hover position error, heading control, touchdown vertical speed, landing accuracy, and completion time. Give brief, actionable feedback.

**Missions and scoring**

Start with passenger transport and internally carried cargo. Defer sling loads.

Each job should specify pickup, destination, payload, time expectations, and service requirements.

Loading and unloading require a grounded helicopter within the landing zone, low motion, and a short stable dwell period. Prevent completion through flyovers or brief collisions.

Reward:

* Efficient routes and timely completion.
* Smooth approaches and low-impact landings.
* Accurate placement within the landing zone.
* Comfortable passenger handling.
* Undamaged cargo.

Use forgiving early jobs and more demanding optional contracts. Avoid encouraging dangerous behavior in training.

Implement an explicit mission lifecycle covering acceptance, pickup, transport, delivery, failure, and retry. Prevent duplicate payouts and recover cleanly after a crash or reset.

**World and presentation**

Build one compact, distinctive map with about 8–12 landing locations. Include generous beginner pads, rooftop pads, docks, and confined rural destinations.

Use a clean, cohesive visual style that runs well on ordinary gaming hardware. Start with functional assets and improve presentation after the flight loop works.

Provide convincing rotor audio that responds to operating conditions, touchdown sounds, wind, and simple pickup/drop-off feedback. Avoid unnecessary passenger dialogue systems.

HUD essentials:

* Airspeed and vertical speed.
* Altitude above ground.
* Heading and collective percentage.
* Active assists.
* Current objective, distance, timer, and payload.
* Optional cyclic input indicator.

Use consistent, clearly labeled units. Explain whether displayed speed is airspeed or ground speed. Make the altitude measurement beneath the aircraft explicit.

Keep landing areas readable without filling the screen with markers.

**Technical requirements**

Use a current stable Unity LTS release and URP. Verify package compatibility before choosing dependencies.

Use Unity’s Input System and separate flight physics, input, assists, cameras, missions, scoring, UI, and persistence.

Save control bindings, preferences, and progression locally.

Target Windows, macOS, and Linux from the beginning. Avoid unnecessary platform-specific plugins. Validate available builds and clearly report platforms that still require native hardware testing.

Include a development overlay for velocities, angular rates, raw inputs, assisted commands, lift, mass, and grounded state.

**Implementation sequence**

1. Flight test scene, one helicopter, input, and third-person camera.
2. Tune hover, acceleration, braking, turning, and landing.
3. Add cockpit, free look, assist presets, and settings.
4. Complete one passenger job and one cargo job end to end.
5. Add the compact map, training drills, scoring, audio, and presentation.
6. Validate builds and resolve remaining gameplay issues.

Do not expand into multiplayer, combat, walking characters, multiple aircraft, a large economy, or a huge open world during the initial build.

**Acceptance criteria**

The vertical slice is ready when a player can:

* Launch into free flight quickly.
* Take off, hover, turn, brake, and land using keyboard/mouse.
* Complete the same flight loop with a gamepad.
* Switch cameras and free look without control jumps.
* Toggle assists and clearly feel the difference.
* Complete passenger and cargo deliveries.
* Crash, retry, and continue without broken mission state.
* Change controls, restart, and retain settings.

Verify that handling remains consistent at different rendering frame rates. Check mission completion, payout, reset, and persistence behavior with targeted tests.

Do not claim the helicopter feels good solely because the code compiles. Perform available runtime checks and provide a short human playtest checklist for handling decisions that require player feedback.

Deliver the playable project, setup/build instructions, implemented controls, known limitations, and a concise explanation of flight-model approximations.

The guiding priority is: **make one helicopter satisfying to fly and land, then build the game around that.**

Commit and push regularly as to not lose good progress.
