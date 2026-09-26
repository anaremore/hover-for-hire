# Validation and playtest checklist

Status snapshot: **26 September 2026, Unity 6000.3.22f1** (phase 6 of the 0.3 overhaul; the 0.2.0 and 0.1.0 sections below are kept as the baseline). The project builds and runs on the available Windows host. Automated flight, camera and mission checks provide evidence of working behavior; a human has not yet judged whether the helicopter feels satisfying or whether the training transfers usefully to other games.

## Performance and release builds (0.3 development, phase 6)

**26 September 2026.**

**Measure first.** A new opt-in benchmark measures the game with the frame rate uncapped, in either player:
- **How it runs:** `Tools/Benchmark.ps1` (`RuntimeBenchmark`).
- **What it covers:** a view from the home pad, the town from 60 m in chase and cockpit views, and a low pass across the island at 60 m/s.
- **What it reports:** average and 1%-low frame rates, and CPU (main and render thread) and GPU frame times from `FrameTimingManager`. The development player also reports managed garbage per frame and the HUD's cost by instrument, using profiler markers.

All figures below come from 1920×1080 windowed, on an RTX 3080 (Direct3D 12) with a Ryzen 9 5900X.

**What costs what.** The baseline was measured with each part removed in turn (development player, town view):

| Removed | Main thread | Render thread | GPU | Garbage |
| --- | --- | --- | --- | --- |
| Nothing (baseline) | 3.30 ms | 2.24 ms | 1.50 ms | 13.8 KB/frame |
| Flight HUD | 1.16 ms | 0.95 ms | 1.51 ms | 0 |
| Shadows | 3.17 ms | 2.17 ms | 1.18 ms | 13.8 KB |
| Vegetation | 3.11 ms | 2.15 ms | 1.10 ms | 13.8 KB |
| Post-processing | 3.09 ms | 2.14 ms | 1.30 ms | 13.8 KB |
| MSAA | 3.24 ms | 2.20 ms | 1.33 ms | 13.8 KB |

The immediate-mode (IMGUI) flight HUD was two-thirds of the main thread's work, over half of the render thread's, and the source of all per-frame garbage. Its profiler markers showed where:
- **Chart:** 0.6 ms. It redrew every road as hundreds of rotated line quads.
- **Tapes and cluster:** 0.6 ms.
- **Compass:** 0.3 ms.
- **Cyclic indicator:** 0.25 ms. Its two small circles were drawn as 52 line segments.
- **Readouts:** every one was formatted every frame. The key hints looked up seven bindings, and the realism summary built three settings objects to find its preset.

**HUD changes.**
- **Drawing on repaint only.** In flight IMGUI skips its layout pass and input events. The Flight Desk and the crash panel's Retry button still get every event.
- **Baked chart.** The chart's roads and grid are baked into its texture (512 texels).
- **Circles.** Each circle is one cached antialiased ring texture.
- **Straight lines.** Horizontal and vertical marks skip the rotated matrix.
- **Text on dark panels** drops its invisible shadow copy.
- **Cached readouts.** Readouts are formatted from keys (the value as shown) and rebuilt only when that changes. `ReadoutFormatTests` checks that keyed text reads exactly as the direct formats did.
- **Slower-changing text.** Key hints refresh only when the device or mode changes (and every 2 s). Mission text refreshes at 10 Hz.

**Results.**

| Player, view | Average fps | 1% low | Main thread | Render thread | GPU | Garbage |
| --- | --- | --- | --- | --- | --- | --- |
| Development, before, town | 299 | 149 | 3.30 ms | 2.24 ms | 1.50 ms | 13.8 KB/frame |
| Development, after, town | 451 | 294 | 1.93 ms | 1.26 ms | 1.53 ms | 6.0 KB/frame |
| Development, before, low pass | 312 | 162 | 3.17 ms | 2.17 ms | 1.31 ms | 14.1 KB/frame |
| Development, after, low pass | 490 | 194 | 1.84 ms | 1.17 ms | 1.21 ms | 6.1 KB/frame |
| **Release, after, town** | **504** | **335** | **1.51 ms** | **1.07 ms** | 1.55 ms | not measured |
| **Release, after, low pass** | **568** | **275** | **1.40 ms** | **0.97 ms** | 1.24 ms | not measured |

The HUD now costs 0.7 ms in chase view and 0.5 ms in the cockpit, down from 1.9 and 1.35 ms. The remaining 6 KB/frame of garbage comes from Unity 6's IMGUI text drawing itself, about 70 bytes per label. Skipping HUD text in a test run removed it; turning off rich text did not. It now means one incremental collection every second or two at 60 fps.

**Graphics options.** The Flight Desk's View page now has graphics and display settings:
- **Presets:** Low, Medium, High (the default) and Ultra, covering shadow distance and cascades, MSAA, render resolution and how far trees are drawn.
- **Frame pacing:** VSync (on by default) and a frame-rate cap.
- **Display:** window mode and resolution.

Presets change a runtime copy of the pipeline asset, never the project asset, and trees are culled by layer distance. `GraphicsQualityTests` checks both, and `-hover-graphics` overrides the saved preset for one run. Measured in the release player:

| Preset | GPU, town view | GPU, low pass | Main thread, town view |
| --- | --- | --- | --- |
| Low | 0.67 ms | 0.64 ms | 1.42 ms |
| Medium | 1.20 ms | 1.04 ms | 1.72 ms |
| High | 1.62 ms | 1.31 ms | 1.84 ms |
| Ultra | 1.83 ms | 1.50 ms | 1.62 ms |

Low roughly halves the GPU time of High. CPU times vary by about ±0.2 ms between runs, more than the difference between presets. The benchmark now waits 3 s after startup: the preset replaces the pipeline, and the first frames compile shaders.

**Startup.** The world's merged-mesh lists are now reused from one batch to the next instead of regrown for each of about 150 batches, and quads no longer allocate arrays. Rendered-only meshes drop their CPU copy once uploaded.

| Measure (development player) | Before | After |
| --- | --- | --- |
| World build | 2.1 s | 1.6 s |
| Garbage collections during it | 1,875 | 95 |
| Startup | 4.3 s | 3.8 s |
| Managed heap reserved | 19 MB | 65 MB |

The heap is larger because the pooled lists peak together, and Unity's collector does not return heap space.

**Final smoke runs.** These ran in the development player, 1600×900 windowed, with the art tour. The 21:9 run was 1680×720 without the art tour.

| Frame cap | Average fps | Scripted peak AGL / ground speed | Errors | Effects diagnostics | Result |
| --- | --- | --- | --- | --- | --- |
| 30 | 29.7 | 25.47 m / 3.56 m/s | 0 | all passed | pass |
| 60 | 59.3 | 25.45 m / 3.56 m/s | 0 | all passed | pass |
| 144 | 141.7 | 25.42 m / 3.56 m/s | 0 | all passed | pass |
| 60, 21:9 | 58.8 | 25.47 m / 3.56 m/s | 0 | — | pass |

The native crash seen once at 144 fps in phase 3 did not recur.

The art tour now opens with a real autopilot cruise toward the farthest pad (`09a-cruise.png`), with the chase camera, HUD and chart as a player sees them. The documentation screenshots were refreshed from these runs. The review found one HUD bug: the collective gauge read 0% under the autopilot. It now shows the collective the aircraft flies, which in normal play is the pilot's lever.

**CI.** The workflow is pushed and runs on every push. Without license secrets, its first job posts a notice and the test and build jobs are skipped, so the run stays green (run 36267519362). The runners are pinned to Ubuntu 24.04, ahead of GitHub moving `ubuntu-latest` to Ubuntu 26 on 19 October 2026.

**Tests.** **120 EditMode** and **63 PlayMode** tests passed.

**Release and development builds.**
- **Release by default.** Builds are release builds.
- **Development variant.** `Tools/Unity.ps1 WindowsDev` builds `Builds/Windows-Development`, which `Tools/Smoke.ps1` runs.
- **Diagnostics.** The smoke flight, its autopilot and its audio capture compile only into development builds and the editor. The benchmark and the flight recorder stay in release builds: both are opt-in, and useful for player reports and playtests.
- **Version.** 0.3.0 has one source (`ProjectSetup.Version`) and shows in the Flight Desk header.
- **Build record.** Each build records its version, commit and development flag in `build-info.json`. `Tools/package_builds.py` names archives by version and refuses development builds or builds of another commit.

## Loop, progression, cockpit and beginner aids (0.3 development, phase 5)

**26 September 2026.**

**Jobs.**
- **Offer board:** between jobs the director deals up to three available contracts. The first starts at the pad the aircraft is on whenever one does. The deal is seeded by progress, so the same record sees the same offers.
- **Par:** a minute for the terminals plus cruise at 25 m/s. The autopilot's flown Home → Town Green took 71 s against a par of 1:15.
- **Time limits:** 3× par for starter jobs, 2× for later ones. The HUD job clock shows elapsed time against par.

**Progression.**
- **Certifications:** earned in drills. Rooftop, Mountain, Coastal and Emergency open demanding pads, never a better aircraft. The first two jobs stay open, and delivery-count gating remains.
- **Logbook:** progression version 2 records flight time, landings, route bests and drill bests. Version 1 saves migrate once on load, rebuilding bests from the results they already hold.
- **Liveries:** four paint-only schemes, bought with earnings.

**Pad cues.**
- Passengers or crates wait at the pickup and board when loading completes.
- The target pad shows a strobe beacon, orange smoke that drifts with the wind, and edge lights.
- The cues are cosmetic and own no colliders.

**Beginner aids.**
- **Hover hold (H):** adds bounded cyclic, collective and pedal inputs to the pilot's own. It hands back on any deliberate input.
- **Attitude command** (off by default): the stick sets bank and pitch.

**Cockpit.** The cockpit was rebuilt with Blender 5.1:
- a lower, narrower instrument panel, with the side displays removed;
- chin windows;
- thinner pillars and frames;
- five large dials: airspeed (kt), radar altitude, vertical speed, torque and rotor speed.

**Mirrored model found and fixed.** The build script had mapped Unity coordinates into Blender without a handedness flip. Every export since 0.2 came out mirrored in Unity:
- the dials read right to left;
- the visual tail rotor sat on the opposite side of the fin from the flight model's tail-strike sensor;
- the main rotor's blades were shaped for the opposite rotation to the one shown.

The fixes:
- The script now flips X.
- The main rotor turns counter-clockwise seen from above, matching the flight model's torque reaction.
- A PlayMode test (`AircraftArtIsTheRightWayRound`) checks the tail rotor position, the doors and the dial order.
- The dial legends had never rendered in Unity: normal recalculation turned the flat lettering away from the pilot. They are now oriented explicitly towards the reader.
- The tail registration moved ahead of the horizontal stabilizer, which had hidden most of it.

**Tests.** **115 EditMode** and **62 PlayMode** tests passed. New tests cover:
- the offer board, par and certifications (`ProgressionLoopTests`);
- the logbook and migration (`LogbookTests`);
- pad cues;
- the beginner aids (`BeginnerAidsRuntimeTests`);
- the aircraft art's handedness.

**Smoke and screenshots** (60 fps, 1600×900).
- **Earlier phase 5 runs** captured the shift and offer board, the Logbook tab, the Coastguard livery and the pad cues at home base.
- **Final run:** passed with no errors at 59.3 fps on average.
- **In-game cockpit:** the dials read KT, RAD ALT, V/S, TQ and NR from left to right, with legible legends. Each needle agreed with the HUD: 7 kt, 25 m, +1.1 m/s and rotor speed 100%.
- **Exterior:** the tail rotor sits on the right of the fin.
- **Close Blender renders:** the lettering reads correctly on both sides, the red light is on the left, and the blades advance counter-clockwise.

## HUD, units and presentation refactor (0.3 development, phase 4)

**26 September 2026.**

**Refactor.** The 670-line HUD component was split into five parts:
- a coordinator;
- a per-frame model, which formats every value and string once per frame instead of once per IMGUI event;
- the instruments view;
- the Flight Desk menu;
- shared styles.

The pilot's settings (assists, realism, units, volume, first launch) now live under one versioned key, with a one-time migration from the 0.2 keys.

**Redesigned chase-view HUD.** The layout now keeps the aircraft clear:
- **Tapes** move outward, with ground speed and vertical speed beside them. Airspeed is now horizontal speed through the air.
- **Bottom cluster:** the pitch ladder and view label are gone. A compact attitude indicator, the collective gauge (free-air and in-ground-effect hover marks) and the torque and rotor-RPM gauges sit here instead.
- **Hover display:** near a pad, a heading-up display replaces the chart. It shows the drift vector, a trend cue, a service ring that turns green when the landing would count, and the pad's position.
- **Readouts:** a wind arrow with speed and recent peak, and a job clock against the job's target time.
- **Placement:** warnings and notices stack under the compass. The cockpit view keeps its readouts in the left column, above the instrument panel.
- **Units:** Metric or Aviation throughout, including drill objectives, feedback, mission status and crash reports.

**Tests.** **103 EditMode** and **57 PlayMode** tests passed. The new `PilotSettingsTests` cover:
- conversion and formatting;
- drill and crash text in both unit systems;
- migration from the old keys;
- round trip and sanitizing.

**Screenshot review** (60 fps smoke, 1600×900). The review covered the free-flight chase and cockpit views, the autorotation drill with engine failure and power gauges, and a crosswind drill in Aviation units. It found and fixed two bugs:
- the collective gauge's legend overflowed into the attitude indicator;
- the engine-failure notice still said m/s.

The re-run confirmed the fixes: IGE and HOVER labels either side of their marks, and "hold 72–90 km/h, flare near 30 m".

**Sound.** The rotor, turbine, gearbox, airflow, skid and horn sounds are synthesized in real time (`RotorSoundSynth`). `RotorSoundTests` (6 tests) check that:
- the 26.3 Hz blade-pass line moves with rotor speed;
- slap adds more than 1.8× the 1–4 kHz energy;
- airflow noise grows with airspeed;
- the turbine runs down after a failure;
- the output is bounded, deterministic and identical on every channel.

The smoke run now records the listener's final mix (`AudioTap`) through the scripted climb and the autorotation drill:

| Measurement | Result |
| --- | --- |
| Strongest low line in the climb | 26.40 Hz (blade pass is 26.33 Hz) |
| Turbine line | 4.02 kHz |
| Turbine line after the engine failure | falls 4,000× in power |
| 520 Hz low-rotor horn after the failure | rises 400× |
| Blade line after the failure | drops to 24.7 Hz (rotor at 94%, as the HUD showed) |
| Levels | RMS 0.12, peak 0.52 |

Whether it sounds good is a human judgement still to make. Totals are now **109 EditMode** and **57 PlayMode** tests.

## Arma-style realism (0.3 development, phase 3)

**26 September 2026.** Realism is optional. It comes as three presets (Relaxed / Realistic / Expert) plus per-effect toggles, covering:
- ground effect and translational lift;
- speed stability;
- vortex ring state;
- finite engine power with rotor RPM;
- engine failures and autorotation;
- tail-rotor failures;
- a seeded island wind with gusts, turbulence and live windsocks.

The phase also adds:
- five new drills: crosswind landing, heavy lift, settling with power, autorotation and confined area;
- a realism picker on the first-run page;
- a realism menu;
- a HUD warning stack;
- aerodynamic camera shudder;
- wind-blown rotor wash.

The shipped tuning asset now records every realism value.

Automated results: **98 EditMode** and **57 PlayMode** tests passed (87 and 47 before). Each effect was prototyped in Python before implementation. The flown results match the prototype's predictions to within a few percent.

| Flown check (`RealismRuntimeTests`, only the named effect on) | Result | Prototype |
| --- | --- | --- |
| 95% of hover collective, ground effect on / off | Holds a 1.61 m hover / stays on the ground | 1.61 m |
| Translational lift at hover collective, 16 → 13 m/s | Climbs 1.91 m/s (without it: 0.00) | ≈1.8 m/s |
| Flapback at 25 m/s, hands off | Nose 5.7° higher after 3 s | — |
| Vortex ring from a vertical descent at 76% of hover collective | Develops after 11.7 s. Holding collective sinks at 13.0 m/s; pulling full collective sinks faster, at 15.8 m/s; flying out recovers after losing 86 m | 11.7 s; 12.9 / 15.8 m/s; 90 m |
| Maximum-weight hover / 80% collective at maximum weight | Torque 85% / torque at the 110% limit, rotor droops to 92.0%, climbs 2.71 m/s (6.54 m/s with unlimited power) | 84.8% / 110%, 92.0% |
| Engine failure, collective held / lowered | Rotor at 90% after 1.88 s / never below 95.7% | 1.88 s |
| Autorotation from 180 m and 25 m/s (glide, flare at 35 m, level at 6 m, cushion at 3 m) | Glide descent 8.4 m/s, rotor 95–102%, touchdown 0.93 m/s with 7.3 m/s ground speed, slides to a stop | 0.86 m/s |
| Autopilot landing in an 8 m/s gusty crosswind (up to 11.1 m/s) | 0.90 m from centre, touchdown 0.16 m/s | — |
| Tail-rotor strike with failures on / off | Spins at 50°/s after 3 s, or 25°/s with collective lowered / crash | — |
| Relaxed vs the base model, hovering at 200 m on +1% collective | Largest vertical-speed difference 0.0007 m/s; climbs 0.478 m/s | identical |

**Prototype findings.** The prototype showed that with a fixed thrust loss, pulling collective escaped the vortex ring after only 20 m, which contradicts the drill's lesson. The loss now grows with collective above hover, and inside the ring the descent no longer lowers the power required. Pulling collective now makes the sink worse, while flying out still recovers. The autopilot gained a small wind trim near the pad. Without it, proportional control balanced wind drag against lean off-centre, and the autopilot never began its descent.

**Smoke runs.** The Windows smoke run now includes a realism segment. It starts the autorotation drill, confirms the engine fails 3 s in, captures the drill brief and the warning stack, and returns to free flight. The run also no longer pauses when another window takes focus.

Final Windows smoke runs (`Tools/Smoke.ps1`, 1600×900 windowed). All of them passed with zero recorded errors, and the 60 fps run's art tour passed all four effects diagnostics:

| Frame cap | Average FPS | Scripted peak AGL / ground speed | Autopilot return | Autorotation drill engine | Errors | Result |
| --- | --- | --- | --- | --- | --- | --- |
| 30 | 29.8 | 25.41 m / 3.56 m/s | landed home (0.26 m/s, 0.08 m, 46 s) | failed on cue | 0 | pass |
| 60 | 59.2 | 25.43 m / 3.56 m/s | landed home (0.26 m/s, 0.09 m, 46 s) | failed on cue | 0 | pass |
| 144 | 141.1 | 25.37 m / 3.56 m/s | landed home (0.25 m/s, 0.06 m, 46 s) | failed on cue | 0 | pass |
| 144 | 141.0 | 25.41 m / 3.56 m/s | landed home (0.26 m/s, 0.08 m, 46 s) | failed on cue | 0 | pass |
| 144 | 141.1 | 25.38 m / 3.56 m/s | landed home (0.27 m/s, 0.09 m, 46 s) | failed on cue | 0 | pass |

**Peak AGL.** The scripted segment's peak AGL rose from 19.9 m to 25.4 m. This is expected: Relaxed includes ground effect, so the same 49% collective lifts off more briskly for the first few metres. It remains inside the 8–32 m pass band.

**Unexplained crash.** One earlier 144 fps run of the first phase-3 build crashed natively in `UnityPlayer.dll`, 22 s into the autopilot's return flight.
- **What it was:** a read access violation of freed memory on the main thread, with no managed exception or log error.
- **What followed:** four more 144 fps runs (one on that build, three on the final build) passed, as did every 30 and 60 fps run.
- **Status:** the cause is unknown. The crash dump is kept locally, and symbolizing it needs Unity's player symbols. Any recurrence will be investigated.

**Screenshot review.**
- The first-run page offers the three presets, with Relaxed selected.
- The Assists / realism tab lists the realism toggles beneath the assists.
- In the autorotation drill, the red ENGINE FAILURE · AUTOROTATE warning, the failure notice, NR/TQ and REALISM / CUSTOM: GE ETL POWER are all readable.
- The review also caught a real layout bug: the longer drill briefs overflowed the two-line objective panel. The panel now grows to fit its text.

## Collision, water and onboarding (0.3 development, phase 2)

**26 September 2026.** This phase adds complete collision, blade strikes and water, and closes the onboarding gaps.
- **Collision:** seven airframe boxes; merged low-poly collision for trees, rocks and props; solid roofs, apron and freight slabs.
- **Blade strikes:** exact convex rotor-disc sensors for the main and tail rotors.
- **Water:** the sea is a surface and the aircraft can ditch.
- **Crash panel:** shows the cause, the measured value and the limit.
- **World fixes:** scenery on the triangulated terrain mesh; buildings on flat footing; props kept out of rotor reach; all 20 roads on the chart.
- **Visuals and onboarding:** non-strobing rotors with motion discs; a welcome panel; binding-aware hints; Enter starts a shift from Free Flight.
- **Autopilot:** a diagnostic autopilot flies automated routes.

Automated results: **87 EditMode** and **47 PlayMode** tests passed.
- **Test repairs:** the tests that could not fail now can:
  - momentum release applies and releases cyclic;
  - preference suppression asserts in the body;
  - the camera test has its own collider in the sweep path, plus a control case;
  - the deadline test really retries;
  - banking is checked with physics.
- **New coverage:**
  - rotor/tail strikes and trigger immunity;
  - ditching and altitude over water;
  - crash causes;
  - the production contract table;
  - a rotor-clear landing at eight edge positions on all ten pads;
  - approach columns;
  - road count;
  - apron collision;
  - a complete passenger delivery flown by physics.

| Flown check | Result |
| --- | --- |
| Passenger job, Home Base → Town Green (`FlownDeliveryTests`) | Loaded, flew about 390 m and delivered: 71 s flight, peak 36 m AGL, 1.5 m from pad centre, touchdown 0.52 m/s, grade A (99) |
| Smoke flight returns and lands home (autopilot, no teleport), 30 / 60 / 144 fps | Landed at all three: touchdown 0.06 m/s, 0.6 m from centre, 49 s return, identical across frame rates |
| Scripted smoke segment peak AGL / ground speed | 19.92–19.96 m / 3.55 m/s (average 29.9, 59.6 and 142.3 fps) |
| Effects diagnostics | Graded impact, explosion, reset restore and water suppression all passed |

Screenshots confirm the welcome panel and footer use the live bindings (Shift/Ctrl collective). They also confirm the crash panel reads "COLLISION · Hit the ground at 25.0 m/s. The limit is 8.0 m/s.", the minimap draws the full town grid, and the rotors show motion discs instead of strobing blades.

## Flight-feel foundation (0.3 development, phase 1)

**26 September 2026, Unity 6000.3.22f1.** This phase adds heave (inflow) damping, rotor pitch/roll/yaw damping with rate-loop feed-forward, tail-fin weathervaning, turn coordination in yaw stabilization, explicit inertia, fine-then-coarse digital collective/pedal ramps, a hover-power tick, the *Sim pedals* gamepad layout and a 50 Hz flight recorder. Each new physics term is a tuning value where zero restores 0.2 behavior.

Automated results: **78 EditMode** and **35 PlayMode** tests passed (62 and 27 before; 24 new). New characterization tests load the shipped `UtilityHelicopter` asset.

| Metric (shipped tuning) | 0.2.0 | Phase 1 | Evidence |
| --- | --- | --- | --- |
| Steady climb after +1% collective over hover | +2.33 m/s | +0.48 m/s | `HandlingEnvelopeTests`, `HandlingRuntimeTests` (band 0.40–0.58) |
| 90% vertical settling after a collective step | 19.7 s | ≈5.4 s empty, 6.7 s with 300 kg | same (≤6.5 s empty; loaded slower) |
| One 60 fps keyboard frame of collective | 0.40% | ≈0.10% | `InputProcessingTests`, `InputDeviceRuntimeTests` |
| Sideslip after 4 s in a 20° bank at 30 m/s, Standard | ≈22° (no turn) | < 5° while turning | `HandlingRuntimeTests` |
| Unassisted 0.25 s roll tap, 1.5 s later | still rolling ≈37°/s | < 5°/s, bank stops in 8–35° | `HandlingRuntimeTests` |
| Standard maximum pitch rate | 34°/s | 34°/s ± 1.7 (feed-forward) | `HandlingRuntimeTests` |

Windows smoke flights (same scripted 0.49 then 0.47 collective inputs, 1600×900 windowed, `Tools/Smoke.ps1`), all passing with zero errors:

| Frame cap | Average FPS | Peak skid AGL | Peak ground speed | 0.2.0 peak AGL |
| --- | --- | --- | --- | --- |
| 30 | 29.6 | 19.92 m | 3.55 m/s | 54.86 m |
| 60 | 58.8 | 19.94 m | 3.55 m/s | 54.42 m |
| 144 | 139.0 | 19.94 m | 3.55 m/s | 54.41 m |

The smoke pass rule now requires a peak AGL of 8–32 m and ground speed of 1.5–10 m/s, so a return to the old runaway climb fails. The 60 fps run also repeated the effects diagnostics (graded impact, explosion, reset restore, water suppression: all passed). The screenshots show the one-decimal collective readout with the hover tick at 44.8%.

A prototype of the same equations predicted these results before implementation (it reproduced the 0.2.0 run's 18 m at +4.6 m/s after 8 s). Tuning values remain provisional: **no human has flown this build yet.** Use the flight recorder (F3) during playtests so feel reports come with data.

## Graphics and effects release (0.2.0)

The updated source passed **62 EditMode tests** at **15:55:43 UTC** and **27 PlayMode tests** at **16:03:14 UTC**, with no failures. New coverage checks impact thresholds, water suppression, incoming collision data, debris/particle cleanup, and restoration of scorched paint. All ten production pads still pass their geometry and actual-aircraft touchdown checks. The world fixture now excludes persistent font materials from teardown rather than trying to destroy Unity assets.

The final Windows graphics sequence at **1600×900** passed takeoff, forward flight, cockpit/chase switching, all Flight Desk pages, and grounded reset without errors or crashes. A separate **1680×720** run verified ultrawide HUD/menu alignment and passed the same flight sequence. The 1600×900 run measured **58.51 average FPS** at a 60 FPS cap, **54.420 m** peak skid AGL and **3.392 m/s** peak horizontal speed on the available RTX 3080 host. This short scripted run is not a general hardware benchmark. Runtime diagnostics confirmed post-processing enabled and ACES active.

The optional art tour captures fixed aircraft, town, harbor, highland and coastal viewpoints after saving the physics result. Its effect diagnostics then inject graded presentation events: a 7 m/s hit does not explode, a 25 m/s dry hit explodes and detaches parts, reset restores the aircraft, and a 30 m/s water impact does not explode. All four assertions passed with zero recorded errors. This screenshot tour is distinct from the real-collision PlayMode regression, and does not claim to be a flown landing/crash sequence.

Visual review corrected cockpit framing, mirrored/oversized world lettering, transparent sign depth behavior, overly dark sky fill, foliage shape/density, water aliasing/fog interpolation, and HUD rotation under screen scaling. Final captures are in `Docs/Screenshots`; detailed local evidence is in `Artifacts/release-smoke`, `Artifacts/ultrawide-smoke`, and `Artifacts/distribution-smoke`.

All three final desktop builds succeeded without shader or C# compilation errors. Archive CRCs and SHA-256 hashes passed; macOS universal and Linux x64 binary headers and executable permissions were verified. The archives are 72.7 MiB (Windows), 113.3 MiB (macOS), and 73.7 MiB (Linux).

The desktop players are development builds. Windows is runtime-verified here. macOS universal and Linux x64 are cross-builds, with native display/input/audio/saving checks and macOS distribution signing still outstanding.

## Earlier flight baseline (0.1.0)

| Check | Observed result | Evidence / limit |
| --- | --- | --- |
| Edit Mode | **55 passed, 0 failed** | `Artifacts/EditMode.xml`, completed **15:02:47 UTC**. Covers flight/input math, assist behavior, mission lifecycle, training and persistence. |
| Play Mode | **26 passed, 0 failed** | `Artifacts/PlayMode.xml`, completed **15:03:03 UTC**. All flight, camera, mission and seven real Input System device-event checks passed. |
| Windows standalone build | **Succeeded** | Latest `Artifacts/Windows.log` reports `Build Finished, Result: Success.` |
| Visible Windows player script | **All three runs passed with 0 recorded errors** | Takeoff, forward command, camera switching, all four menu pages and reset. All runs started and reset grounded, and none crashed. `Artifacts/smoke-30`, `smoke-60`, and `smoke-144` contain reports and eight rendered screenshots each. |
| macOS and Linux builds | **Both succeeded** | `Artifacts/macOS.log` and `Artifacts/Linux.log`. macOS binary header confirms a universal Intel/Apple silicon executable; Linux is x64. These are Windows cross-builds, not native runtime validation. |
| Windows 30/60/144 FPS player checks | **Passed; measured results below** | Peak altitude and horizontal speed differ by less than 1% across the scripted sequence. Capture overhead, frame scheduling and concurrent cross-build work affect achieved frame rate. This is a consistency check, not a hardware benchmark. |
| Human handling / comfort review | **Not performed** | The visible player sequence was scripted, not a human playtest. Its altitude/speed measurements describe that short sequence and are not performance benchmarks or handling-quality ratings. |

The earlier device-test failures came from input-event routing in an unfocused batch editor. The fixture now temporarily sets `IgnoreFocus` and `AllDeviceInputAlwaysGoesToGameView` so injected events reach its isolated controls, then restores both settings. This is a test-environment change; production focus-loss pause behavior remains enabled and its dedicated check passes. All seven device-event checks, including keyboard/gamepad commands, free look, menus, pause/reset, focus recovery and preference-write isolation, passed in the latest run.

| Requested FPS | Measured average FPS | Peak skid AGL | Peak horizontal speed | Errors / crashes |
| --- | --- | --- | --- | --- |
| 30 | 28.65 | 54.856 m | 3.419 m/s | 0 / 0 |
| 60 | 59.56 | 54.551 m | 3.392 m/s | 0 / 0 |
| 144 | 139.01 | 54.411 m | 3.420 m/s | 0 / 0 |

The smoke pilot supplies timed bounded control commands to the actual Rigidbody; it does not teleport during flight. Its explicit reset is tested separately. The sequence tests takeoff, a short forward maneuver, camera switches and reset, not a flown delivery route. Mission integration tests deliberately relocate between pads to isolate loading/scoring/state behavior. Complete routes with human keyboard/mouse and physical gamepad control remain on the checklist below.

The baseline views were inspected at 1280×720. That pass corrected pad surface flicker, default primitive pad collision, cyclic-indicator overlap and footer contrast. Version 0.2 replaces that presentation with the modeled cockpit and new HUD described above.

## Acceptance matrix

| Player outcome | Implemented behavior | Automated evidence | Human / platform check still needed |
| --- | --- | --- | --- |
| Launch quickly into free flight | Scene bootstrap starts grounded at home with collective at zero. | Windows build and visible player launch/reset completed. | Fresh launch on intended hardware; screen readability and immediate access to flying. |
| Take off, hover, turn, brake and land with keyboard/mouse | Persistent collective, mouse/WASD cyclic, pedals and physical forces/torques. | Real Rigidbody tests pass for empty/loaded hover, takeoff, braking, momentum and ground contacts. Pure processing and injected keyboard/mouse device checks pass. | Complete the entire loop with actual keyboard/mouse; assess effort, response, overshoot, braking distance and landing satisfaction. |
| Complete the same loop with a gamepad | Stick cyclic, incremental trigger collective, shoulders for yaw, independent right-stick camera and navigable pause menus. | Pure processing plus injected gamepad command/menu checks pass, including proportional persistent collective and navigation while paused. | Actual connected controller, deadzones, triggers, menu use and a complete delivery without mouse assistance. |
| Switch cameras and free look without control jumps | Separate chase/cockpit cameras, hold/return mouse cyclic choices, discarded release-frame mouse motion. | Camera switching preserves aircraft/collective; own-aircraft exclusion and building collision tests pass. Pure and device-event free-look, release suppression and focus-recovery checks pass. | View clearance around roofs, look/recenter comfort, both free-look options, focus loss and repeated switching while hovering. |
| Toggle independent assists and feel a difference | Rate, auto-level, yaw stabilization and torque compensation; Beginner, Standard and Unassisted. | Bounds, disabled assistance, smooth transitions and physical rate damping pass. Results retain used assist configurations. | Compare presets and individual flags; confirm readable control changes without sudden motion and restore Beginner before difficult landing work. |
| Complete passenger and cargo deliveries | Explicit lifecycle, grounded stable dwell, payload mass, time/placement/impact/condition scoring. | Real pad/skid integration completes both jobs, applies loaded mass and saves two payouts. Wrong-floor contact cannot unload. Fixture relocation between pads isolates mission behavior. | Fly both actual routes end to end; judge objective readability, approach workload, loaded handling and service feedback. |
| Crash, retry and continue | Crash/reset failure clears payload; retry restarts pickup; completed attempts cannot reopen payment. | Hard-contact crash/reset, external reset/retry, repeated post-delivery reset and duplicate payout protection pass. | Recover using the player controls after a hard landing and after a loaded mission failure; verify the next objective is clear. |
| Change controls, restart and retain settings | Local preferences, semantic binding overrides and separate progression with recoverable JSON backup. | Input/settings round trips and regenerated-action binding restoration pass; mission save/reload, backup recovery and persistent duplicate IDs pass. | Rebind a useful action, change sensitivity/assists, restart the standalone player, confirm restoration, then restore desired settings. |
| Practice twelve short drills | Takeoff, hover, yaw, forward flight, braking, approach and precision landing, plus crosswind, heavy lift, settling with power, autorotation and confined area, with measurable completion conditions and retries. | All twelve completion paths pass; unstable hover, hard precision landing and remaining on the starting pad cannot falsely complete. The realism techniques are flown by physics (autorotation touchdown 0.93 m/s, vortex-ring recovery, crosswind landing). | Instructions and feedback should lead to useful corrections; complete at least hover, precision landing and the autorotation drill using real controls. |
| Choose how hard the aircraft and weather push back | Relaxed / Realistic / Expert realism or individual toggles, recorded with every result; each realism drill switches on only the effect it teaches. | Each effect is flown with only it enabled; Relaxed matches the base model to 0.001 m/s away from the ground; presets and custom summaries round-trip. | Fly the settling-with-power and autorotation drills in Realistic; judge whether the warnings, shudder and feedback teach the technique, and whether Expert wind is fun rather than tiring. |
| Choose jobs and build a record | An offer board of up to three jobs with par and time limits; certifications earned in drills open demanding pads; a logbook of time, landings and bests; paint-only liveries bought with earnings. | Offers are seeded and start at the current pad; every contract has par, limits and its certification; version 1 records migrate and reload; livery purchases cannot overspend; pad cues follow the job and own no colliders. | Judge whether offers, par and certifications give a reason to fly the next job and to practice the drills, and whether the logbook is worth opening. |
| Get help in the hover without losing control | Hover hold (H) and an optional attitude-command mode, both with bounded authority; hover hold hands back on any deliberate input. | The hold arrests a 3.2 m/s drift and a sink within 12 s, keeps the heading and hands back on stick input; full stick holds 25° of bank and a centered stick flies level. | Use the hold on a first landing: does it help without surprising you when it hands back? Try attitude command against rate command. |
| Maintain consistent handling across rendering rates | Fixed 50 Hz physics; elapsed-time actuator response; frame-aware mouse processing. | Pure mouse/return tests and rendered player sequences at 30/60/144 FPS pass; flight metrics vary by less than 1%. | Repeat a complete route at these frame rates with actual controls. Judge frame pacing, camera response and landing consistency. |
| Play on Windows, macOS and Linux | Standalone build entry points and cross-platform Unity/Input System code. | All three builds succeeded; Windows player launched and completed scripted flights. | Native macOS/Linux launch, display, controller mapping, focus/OS shortcuts, audio, local saves and practical performance. |

## A focused 10–15 minute human session

Use Beginner assists initially. Take longer or split the session if approaching a pad needs practice; this is a handling check, not a timed skill test. Record controller model, assist flags, mouse mode, sensitivity, display/frame rate and one specific improvement after each phase.

1. **Minutes 0–3: keyboard/mouse flight.** Raise Left Shift gradually through roughly 45% empty collective; release it and confirm the setting holds. Hover at 5–10 m, yaw with Q/E, accelerate with mouse or W, release cyclic and observe retained momentum. Brake with S/aft mouse cyclic, then land with a descent below 1 m/s. Record whether drift, stopping distance and touchdown are readable.
2. **Minutes 3–6: passenger service.** Start a shift from the flight desk and accept the first job with Enter. Confirm a short touch or moving contact does not load; remain still for the dwell. Fly home-to-town, notice the higher collective demand with payload, and deliver. Check the payout/grade and useful landing feedback.
3. **Minutes 6–10: gamepad cargo service.** Use the gamepad to accept the next offer. Left stick controls cyclic, triggers change held collective, shoulders control yaw, right stick looks and North switches camera. Fly town-to-dock with cargo, brake early and unload. Check neutral-stick drift, trigger precision and whether menus can be used without a mouse.
4. **Minutes 10–12: camera, pause and assists.** In a safe open hover, try both chase and cockpit views, Alt or middle-mouse free look, R recenter, and both hold/return cyclic options. Release free look without a cyclic jump. Pause, move inputs, resume, and switch focus away/back. Compare Beginner and Standard; briefly try Unassisted with ample room, then restore Beginner. Record visibility or unexpected motion.
5. **Minutes 12–15: recovery and persistence.** Reset with Backspace or gamepad Select and confirm collective is zero. If practical, retry a failed loaded job and verify pickup is required again with no extra payment. Rebind one action, change a preference, save and quit. Relaunch to confirm bindings/settings and completed earnings persist. Finish with the precision-landing or hover drill if time allows.

Run a separate short comparison at 30/60/144 rendering FPS with the same settings and fixed timestep. Use the same takeoff height, forward input duration, release point and braking maneuver. Record frame pacing, control/camera differences and landing consistency rather than relying on recollection.

On native macOS and Linux, repeat launch, a brief gamepad/keyboard flight, camera/free look, pause/focus recovery, audio and save/restart. Try middle mouse if Alt is intercepted by the window manager; try Fn or rebinding for reserved F-keys. Verify the distributed application's signing/launch requirements and executable permissions as appropriate to its platform.

## Scope limits that affect interpretation

Hover hold is a bounded assist that hands back on any deliberate input (see [Controls.md](Controls.md)). The realism effects are compact models chosen to teach the right technique. Blade-element aerodynamics, retreating-blade stall, failures beyond the engine and tail rotor, and aviation certification are outside this slice. The model and its units are detailed in [FlightModel.md](FlightModel.md); control processing and platform fallbacks are in [Controls.md](Controls.md).

No automated result establishes that the helicopter feels good. Native platform testing, comfortable camera tuning, controller ergonomics and satisfying approaches remain explicit human acceptance work.
