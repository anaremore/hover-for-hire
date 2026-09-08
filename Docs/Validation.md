# Validation and playtest checklist

Status snapshot: **8 September 2026, Unity 6000.3.22f1**. The project builds and runs on the available Windows host. Automated flight, camera and mission checks provide evidence of working behavior; a human has not yet judged whether the helicopter feels satisfying or whether the training transfers usefully to other games.

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
| Practice seven short drills | Takeoff, hover, yaw, forward flight, braking, approach and precision landing with measurable completion conditions and retries. | All seven core completion paths pass; unstable hover, hard precision landing and remaining on the starting pad cannot falsely complete. | Instructions and feedback should lead to useful corrections; complete at least hover and precision landing using real controls. |
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

Hover hold, ground effect and translational lift are deliberately deferred until basic hover, forward flight, braking and landing have human feedback. The hover-hold binding currently reports that the feature is unavailable; it is not an active assist. Wind physics, autorotation, complex rotor/failure regimes and aviation certification are outside this slice. The model and its units are detailed in [FlightModel.md](FlightModel.md); control processing and platform fallbacks are in [Controls.md](Controls.md).

No automated result establishes that the helicopter feels good. Native platform testing, comfortable camera tuning, controller ergonomics and satisfying approaches remain explicit human acceptance work.
