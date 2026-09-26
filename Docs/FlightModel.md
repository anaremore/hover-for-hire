# Helicopter model

The utility helicopter uses a dynamic Rigidbody in SI units. All ordinary flight force and torque calls occur in `FixedUpdate`; only an explicit respawn writes the Rigidbody pose and clears velocity. Unity's collision solver supports the skids. Do not freeze rotation or position axes on the Rigidbody.

`FlightTuning` is the central ScriptableObject for mass, inertia, rotor thrust, control authority, rotor aerodynamics, aerodynamic drag, assist response and landing tolerances. When no asset is assigned, the controller creates a disposable runtime instance with the same defaults. The initial empty mass is 1,050 kg and maximum thrust is 23,000 N, giving an ideal level, still-air hover collective of 44.8%. Payload increases mass and scales the principal moments of inertia with it; 300 kg of payload raises ideal hover collective to 57.6%. The HUD marks the current hover collective on the collective gauge.

Moments of inertia are explicit tuning values (pitch 1,174, yaw 1,233, roll 690 kg·m² empty; formerly derived from the three collision boxes). Changing or adding colliders therefore never changes handling.

## Actuators and approximations

* Collective is a persistent 0–1 lift demand from the input provider. It moves a first-order rotor actuator and scales available thrust. It never targets altitude or vertical speed. Normal rotor speed is governed at a nominal 395 rpm; engine startup and powertrain simulation are omitted.
* Cyclic tilts the rotor disk up to 3 degrees relative to the aircraft and applies bounded pitch and roll moments. The aircraft attitude also tilts the thrust vector, so bank sacrifices vertical lift and produces horizontal acceleration. This is a simplified rotor/disk coupling, not a blade-element or flapping simulation.
* Positive pedals request right yaw. Main-rotor reaction is an opposing-system yaw moment proportional to rotor lift; tail authority and optional compensation act through the same bounded yaw command.
* Linear and quadratic body-axis drag act against velocity *through the air*. Small passive angular drag represents the airframe. Neither mechanism overwrites velocity. Releasing cyclic retains momentum; a pilot must tilt back and plan a deceleration to stop.
* **Heave damping (rotor inflow).** Climbing or descending through the air changes rotor inflow and opposes the vertical motion: 400 N per m/s along world up, scaled by rotor speed. A collective change therefore settles toward a climb or descent rate within a few seconds (+1% collective ≈ +0.5 m/s, 90% settled in about 5 s empty and 6.7 s with 300 kg) instead of accelerating for tens of seconds. It also cushions a descent near touchdown.
* **Rotor rate damping.** Main-rotor flapping resists pitch and roll (2.5 per second × axis inertia) and the tail rotor resists yaw (1.0 per second × inertia), scaled by rotor speed squared like control authority. This is aerodynamics, present in every assist mode: in Unassisted a brief cyclic input produces a rate that decays within about a second, as with a real rotor, rather than a roll that continues until the pilot opposes it.
* **Weathervane.** The vertical fin produces a yaw moment of 3 N·m per (lateral airspeed × airspeed), turning the nose toward air arriving from the side. In forward flight this aligns the nose with the flight path; with neutral pedals and no torque compensation the aircraft trims at the sideslip where fin and rotor torque balance.
* Thrust acts through the center of mass; cyclic moments represent rotor control authority. Internal payload is treated as centered mass with the airframe's collider-derived inertia distribution. Fuel burn, moving passenger loads and sling loads are omitted.
* The atmosphere is calm in this slice, but every aerodynamic term already uses velocity relative to the air mass (`IWindSource`), so wind is a drop-in. The HUD shows the magnitude of 3D velocity relative to the air in km/h; with no wind this equals 3D ground-relative speed, and includes vertical movement. It is not indicated airspeed. The development overlay separately shows horizontal ground speed in m/s. AGL is the downward ray distance from the fuselage origin, minus the 1.5 m upright skid offset. It sees terrain and roofs, excludes the aircraft and triggers, and clamps at zero; it is not a forward terrain-warning system.
* Grounded state requires actual upward collision contacts, not proximity to a ray surface. Loading systems should additionally enforce landing-zone distance, low vertical and horizontal velocity, upright attitude and a continuous dwell. Hard landings or fast obstacle impacts mark the helicopter crashed and let its rotor spool down; they do not freeze the aircraft.

Ground effect, translational lift, wind, vortex-ring state, retreating-blade stall, complex failures and autorotation are deliberately deferred until human playtests establish the core hover, braking and landing tuning. Hover hold is also deferred. These omissions are significant for real aircraft operation: this game is not a certified simulator.

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

Setting `HeaveDampingNsPerM`, `RotorRateDampingPerSecond` and `WeathervaneCoefficient` to zero restores the 0.2 behavior for A/B comparison. The flight recorder (F3, or `-recordFlight`) writes a 50 Hz CSV of inputs and state to `persistentDataPath/flights/` for analyzing a playtest.

Human playtest checklist:

1. In Beginner, smoothly raise collective through approximately 45%, lift to 5–10 m, then trim collective until vertical speed is near zero. Repeat with a payload and verify the increased demand.
2. Pitch forward, release cyclic and confirm the aircraft levels while retaining forward speed. Brake with aft cyclic; judge whether stopping distance is readable.
3. Bank into a turn. Confirm altitude requires more collective, then lower collective as the aircraft levels.
4. Compare Standard and Unassisted in a safe open area. Verify Standard holds rates without auto-level; verify Unassisted requires opposing cyclic and pedals and never inherits hidden leveling.
5. Perform gentle, off-center, sloped and hard landings. Confirm stable skids at idle collective, no loading during a flyover or bounce, and consistent crash/retry behavior.
6. Repeat takeoff, a timed sustained control input, braking and landing with rendering capped at 30, 60 and 144 fps while retaining the configured fixed physics timestep. Record any control or camera differences.

Recommended tuning order is collision stability, empty hover, stabilized angular response, forward braking, loaded hover, landing tolerance, then optional aerodynamic refinements. Keep sufficient cyclic authority for recovery before increasing top speed or mission difficulty.
