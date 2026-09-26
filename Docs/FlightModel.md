# Helicopter model

The utility helicopter uses a dynamic Rigidbody in SI units. All ordinary flight force and torque calls occur in `FixedUpdate`; only an explicit respawn writes the Rigidbody pose and clears velocity. Unity's collision solver supports the skids. Do not freeze rotation or position axes on the Rigidbody.

`FlightTuning` is the central ScriptableObject for mass, inertia, rotor thrust, control authority, rotor aerodynamics, aerodynamic drag, assist response and landing tolerances. When no asset is assigned, the controller creates a disposable runtime instance with the same defaults. The initial empty mass is 1,050 kg and maximum thrust is 23,000 N, giving an ideal level, still-air hover collective of 44.8%. Payload increases mass and scales the principal moments of inertia with it; 300 kg of payload raises ideal hover collective to 57.6%. The HUD marks the current hover collective on the collective gauge.

Moments of inertia are explicit tuning values (pitch 1,174, yaw 1,233, roll 690 kg·m² empty; formerly derived from the three collision boxes). Changing or adding colliders therefore never changes handling.

## Actuators and approximations

* Collective is a persistent 0–1 lift demand from the input provider. It moves a first-order rotor actuator and scales available thrust. It never targets altitude or vertical speed. Rotor speed is governed at a nominal 395 rpm unless realism enables power limits (below); engine start-up is omitted.
* Cyclic tilts the rotor disk up to 3 degrees relative to the aircraft and applies bounded pitch and roll moments. The aircraft attitude also tilts the thrust vector, so bank sacrifices vertical lift and produces horizontal acceleration. This is a simplified rotor/disk coupling, not a blade-element or flapping simulation.
* Positive pedals request right yaw. Main-rotor reaction is an opposing yaw moment proportional to rotor lift and to the share of rotor torque the engine is delivering; tail authority and optional compensation act through the same bounded yaw command.
* Linear and quadratic body-axis drag act against velocity *through the air*. Small passive angular drag represents the airframe. Neither mechanism overwrites velocity. Releasing cyclic retains momentum; a pilot must tilt back and plan a deceleration to stop.
* **Heave damping (rotor inflow).** Climbing or descending through the air changes rotor inflow and opposes the vertical motion: 400 N per m/s along world up, scaled by rotor speed. A collective change therefore settles toward a climb or descent rate within a few seconds (+1% collective ≈ +0.5 m/s, 90% settled in about 5 s empty and 6.7 s with 300 kg) instead of accelerating for tens of seconds. It also cushions a descent near touchdown.
* **Rotor rate damping.** Main-rotor flapping resists pitch and roll (2.5 per second × axis inertia) and the tail rotor resists yaw (1.0 per second × inertia), scaled by rotor speed squared like control authority. This is aerodynamics, present in every assist mode: in Unassisted a brief cyclic input produces a rate that decays within about a second, as with a real rotor, rather than a roll that continues until the pilot opposes it.
* **Weathervane.** The vertical fin produces a yaw moment of 3 N·m per (lateral airspeed × airspeed), turning the nose toward air arriving from the side. In forward flight this aligns the nose with the flight path; with neutral pedals and no torque compensation the aircraft trims at the sideslip where fin and rotor torque balance.
* Thrust acts through the center of mass; cyclic moments represent rotor control authority. Internal payload is treated as centered mass with the airframe's collider-derived inertia distribution. Fuel burn, moving passenger loads and sling loads are omitted.
* Every aerodynamic term uses velocity relative to the air mass (`IWindSource`); the island wind is described under Realism. The airspeed tape shows horizontal speed through the air. Ground speed is shown beside it, and the two differ in wind. Neither is indicated airspeed. Both follow the pilot's units (Metric or Aviation). AGL is the downward ray distance from the fuselage origin, minus the 1.5 m upright skid offset. It sees terrain and roofs, excludes the aircraft and triggers, and clamps at zero; it is not a forward terrain-warning system.
* Grounded state requires actual upward collision contacts, not proximity to a ray surface. Loading systems should additionally enforce landing-zone distance, low vertical and horizontal velocity, upright attitude and a continuous dwell. Hard landings or fast obstacle impacts mark the helicopter crashed and let its rotor spool down; they do not freeze the aircraft.

## Collision, blade strikes and water

The airframe collides through seven boxes: hull and two skids carry ground contact; nose, forward and aft tail boom, horizontal stabilizer and fin make the rest of the aircraft solid. Because inertia is an explicit tuning value, these shapes do not change handling.

Each rotor sweeps a **convex trigger disc** (main rotor: 4.62 m radius at the hub; tail rotor: 0.87 m, 0.3 m thick). Physics reports any overlap with terrain, buildings, props or trees every step, with no gaps between probe points, and a spinning rotor (above 25% of governed speed) that touches anything solid is a **blade strike**. The aircraft's own colliders and trigger volumes never count. Trees and rocks carry merged low-poly collision on their own layer, which rotors strike but the chase camera passes through.

The sea is a surface at −3.5 m (`WorldConstants.SeaLevel`, shared with the ocean shader and effects). Skid-ground altitude measures to the water instead of the seabed, and skids more than 0.3 m under the surface **ditch** the aircraft.

Every crash records a cause with the measured value and the limit it exceeded: hard landing (touchdown speed vs 5.5 m/s), rollover (tilt vs the realism limit: 65° Relaxed, 40° Realistic, 35° Expert), collision (impact speed vs 8 m/s), main or tail rotor strike (with the struck object), ditching, or leaving the island. The crash panel shows the cause, the numbers and one piece of advice.

## Realism (optional challenge)

Assists decide how much help the pilot gets; `RealismSettings` decides how much the aircraft and the weather push back. The player picks a preset on first launch (and later under Flight Desk → Assists / realism) or toggles effects individually (*Custom*). Every result records both the assists and the realism used. Nothing challenging is forced on: Relaxed keeps the governed rotor and calm air, and each realism drill switches on only the effect it teaches.

| Effect | Relaxed | Realistic | Expert |
| --- | --- | --- | --- |
| Ground effect, translational lift | ✓ | ✓ | ✓ |
| Speed stability (flapback, transverse flow) | – | ✓ | ✓ |
| Vortex ring state | – | ✓ | ✓, onset at 80% of the descent rate |
| Power limits (rotor RPM, torque) | – | ✓ | ✓ |
| Tail-rotor strike | crash | tail-rotor failure | tail-rotor failure |
| Wind at 10 m / gustiness | calm | 4 m/s / 0.3 | 8 m/s / 0.6 |
| Engine failures | off | drills only | drills, plus random (mean 30 min of flight above 30 m) |
| Rollover limit on the ground | 65° | 40° | 35° |

Each effect is a `FlightTuning` value where zero disables it, and `RealismSettings.None()` switches all of them off: that is exactly the base model above, which the handling tests still characterize.

* **Ground effect.** Cheeseman–Bennett, T/T∞ = 1/(1 − (R/4z)²), where z is the rotor hub's height above the surface below (terrain, roof, trees or sea). It is capped at +15% and fades out with horizontal airspeed by 15 m/s. Sitting on the skids (hub 3.6 m up) it adds 11.8%, so the in-ground-effect hover collective is 40% rather than 44.8%; at two rotor diameters it is under 2%. `HoverCollectiveHere` gives the hover setting at the current height.
* **Translational lift.** Up to +10% thrust, rising smoothly between 5 and 13 m/s of horizontal airspeed (hovering in a wind counts), with a shudder that peaks mid-band (`TransitionBuffet01`). At constant collective the aircraft climbs as it accelerates through the band.
* **Speed stability.** Flapback: 14 N·m of nose-up moment per m/s of forward airspeed, so the nose rises unless the pilot holds it down. Transverse flow: up to 150 N·m of roll through the 3–8 m/s transition.
* **Vortex ring state (settling with power).** Severity 0–1 = descent band (4.5 → 7 m/s through the air, scaled by the onset setting) × low airspeed (fading between 5 and 12 m/s) × power (collective 25% → 40%). An engine-out rotor has upward inflow and reaches at most 15%. The ring removes the heave-damping cushion and adds random pitch/roll buffet of up to 450 N·m. It costs up to 35% of thrust at or below the hover collective. Above hover the loss grows by twice the excess (capped at 70%): pulling collective makes the sink worse. Inside the ring the descent brings no upflow to reduce the power required. The way out is forward airspeed, or lowering collective.
* **Power and rotor speed.** With power limits on, the rotor is a flywheel (1,400 kg·m²). Power required comes from momentum theory: P = T·(vᵢ + v_axial)/FM + P₀(1 + 4.65μ²)·NR³.
  * FM = 0.7 and P₀ = 30 kW.
  * vᵢ is Glauert's forward-flight induced velocity.
  * T includes the heave (upflow) force, so a steady descent or a flare can drive the rotor.

  The engine produces 270 kW, which is 100% torque at 395 rpm. It is governed (gain 4/s) up to a 110% torque limit. Asking for more than it can supply droops the rotor. Thrust scales with NR² and collapses below 70% NR as the blades stall. Hover needs 54% torque empty and 85% at maximum weight. Without power limits the rotor stays governed and torque is display-only.
* **Engine failure and autorotation.** The engine stops delivering torque, and the main-rotor yaw reaction goes with it.
  * With collective held, NR decays to 90% in about 1.9 s. Lowering collective at once keeps it in the green.
  * A steady autorotative glide at 20–25 m/s descends at 8–9 m/s with about 22% collective.
  * The taught technique: flare at 30–35 m, level at 6 m, then pull collective against the remaining descent. It lands at about 1 m/s, sliding on at 7–10 m/s.
* **Tail-rotor failure.** With the option on, a tail-rotor strike removes pedal authority and tail yaw damping instead of ending the flight. Main-rotor torque then spins the fuselage (about 50°/s after 3 s at hover power). Less collective means less torque and a slower spin, so the survivable landing is a running one.
* **Wind.** `WindField` is seeded and deterministic:
  * a mean wind with a log-law height profile (roughness 0.1 m): 0.65× at 2 m, 1× at 10 m, 1.3× at 40 m;
  * slow drift of ±12° in direction and ±15% in strength over minutes;
  * gusts that vary in space and time (0.8 × gustiness × speed per axis);
  * faster turbulence that adds up to 300 N·m of pitch/roll buffet.

  Every aerodynamic term (drag, heave damping, weathervane, translational lift, ground-effect fade, vortex ring) uses velocity through the air. The windsocks at each pad point downwind and lift with the wind's strength, and rotor-wash dust drifts downwind. The camera shudders subtly in the translational-lift transition, in a vortex ring and in turbulence.

The HUD adds a warning stack: ENGINE FAILURE, TAIL ROTOR FAILURE, LOW ROTOR RPM (below 95%, flashing), ROTOR OVERSPEED (above 108%), OVERTORQUE (above 100%) and SETTLING WITH POWER. With power limits on, the bottom cluster adds a torque gauge (red line at 100%) and a rotor-RPM gauge (green band 95–105%). A wind arrow near the top right shows where the wind comes from relative to the nose, with its speed and recent peak.

Still not modeled:
* retreating-blade stall;
* blade-element rotor aerodynamics;
* engine start-up and fuel;
* sling loads;
* failures other than engine and tail rotor.

Hover hold is not implemented yet. These omissions matter in a real aircraft, and this game is not a certified simulator.

## Assists

Input processing ends at `IFlightInput.Command`; `AssistSolver` runs in the flight subsystem. Every output is bounded to a unit cyclic disk, ±1 yaw and 0–1 collective. Control actuators and assist weight changes have separate exponential time constants. Toggling a setting blends toward its new authority instead of abruptly changing force.

Rate stabilization combines a feed-forward term (the command that sustains the requested rate against the passive rotor and airframe damping) with proportional feedback on the rate error, so Standard and Beginner reach the full 34°/s pitch/roll and 48°/s yaw rates despite the physical damping. At the maximum rate the feed-forward uses about 64% of pitch and 47% of roll travel, leaving headroom for corrections.

| Setting | Behavior |
| --- | --- |
| Rate stabilization | Cyclic requests pitch/roll rate. Rate error supplies bounded correction. |
| Auto-level | A centered cyclic adds a bounded leveling request, fading out as the pilot moves the stick. It does not hold location or cancel horizontal motion. |
| Yaw stabilization | Pedals request yaw rate. Centered pedals damp yaw through the tail command. Above 8 m/s forward airspeed (fully by 18 m/s) it also requests the coordinated-turn rate g·tan(bank)/airspeed, so banking at speed turns the nose instead of sliding sideways. |
| Torque compensation | Adds tail feed-forward against the modeled rotor reaction moment. |

Beginner enables all four settings. Standard enables rate, yaw and torque settings. Unassisted disables all four; actuator response and passive aerodynamic drag still exist. Each flag can be changed independently. Yaw stabilization alone can counter some rotor reaction after it detects a yaw rate; torque compensation provides the separate feed-forward action. `Assists.Summary` identifies the actual flags for the HUD and result records.

## Verification and tuning

EditMode tests cover bounded/finite pilot demand, dissipative drag, payload performance, bank/lift coupling, truly disabled assistance, independent level and torque assistance, bounded rate correction, smooth toggles and elapsed-time actuator response. `HandlingEnvelopeTests` characterize the shipped tuning asset: hover collective, climb per 1% collective and settling time, the sign and scale of each rotor term, rate feed-forward, and turn coordination only at speed. `HandlingRuntimeTests` repeat the key metrics with the real Rigidbody: +1% collective settles to 0.40–0.58 m/s within 6.5 s (slower when loaded), Standard still reaches 34°/s, an Unassisted 0.25 s roll tap stops rolling within 1.5 s, disabling the new terms restores the old response, a 20° banked turn at 30 m/s stays within 5° of sideslip, and mirrored sideslips weathervane in opposite directions. Tests do not prove that the helicopter feels satisfying to a player.

`RealismEffectsTests` pins each realism curve: ground-effect gain, translational lift, the vortex-ring region and its collective-dependent loss, hover torque, induced velocity, autorotative power, the wind field, the presets and the five new drills. `RealismRuntimeTests` flies each effect with the real Rigidbody, a scripted test pilot and only that effect enabled. Measured values:
* 95% of the free-air hover collective holds a 1.6 m hover in ground effect, while the same aircraft without it stays down.
* Translational lift climbs 1.9 m/s at hover collective.
* Flapback raises the nose 5.7° in 3 s at 25 m/s.
* A vortex ring develops 12 s into a slow descent. Holding the collective sinks at 13 m/s; pulling it sinks at 16 m/s; flying out recovers after 86 m.
* Overpulling at maximum weight holds 110% torque and droops the rotor to 92%.
* After an engine failure, the rotor reaches 90% in 1.9 s with collective held and stays above 95% with collective lowered.
* The autorotation glides at 8.4 m/s descent and touches down at 0.9 m/s.
* The autopilot lands 0.9 m from center in a gusting 8 m/s crosswind.
* A tail-rotor failure spins at 50°/s, or 25°/s with collective lowered.
* Relaxed matches the base model to 0.001 m/s away from the ground.

Setting `HeaveDampingNsPerM`, `RotorRateDampingPerSecond` and `WeathervaneCoefficient` to zero restores the 0.2 behavior for A/B comparison. The flight recorder (F3, or `-recordFlight`) writes a 50 Hz CSV of inputs and state to `persistentDataPath/flights/` for analyzing a playtest.

Human playtest checklist:

1. In Beginner, smoothly raise collective through approximately 45%, lift to 5–10 m, then trim collective until vertical speed is near zero. Repeat with a payload and verify the increased demand.
2. Pitch forward, release cyclic and confirm the aircraft levels while retaining forward speed. Brake with aft cyclic; judge whether stopping distance is readable.
3. Bank into a turn. Confirm altitude requires more collective, then lower collective as the aircraft levels.
4. Compare Standard and Unassisted in a safe open area. Verify Standard holds rates without auto-level; verify Unassisted requires opposing cyclic and pedals and never inherits hidden leveling.
5. Perform gentle, off-center, sloped and hard landings. Confirm stable skids at idle collective, no loading during a flyover or bounce, and consistent crash/retry behavior.
6. Repeat takeoff, a timed sustained control input, braking and landing with rendering capped at 30, 60 and 144 fps while retaining the configured fixed physics timestep. Record any control or camera differences.

Recommended tuning order is collision stability, empty hover, stabilized angular response, forward braking, loaded hover, landing tolerance, then optional aerodynamic refinements. Keep sufficient cyclic authority for recovery before increasing top speed or mission difficulty.
