# Hover for Hire

A single-player helicopter delivery game and computer control practice sandbox. Fly one civilian utility helicopter around Port Meridian, a compact island with ten landing locations. This is an early playable vertical slice: handling and camera tuning need human playtesting.

## Quick start

1. Open this folder in **Unity 6000.3.22f1 (Unity 6.3 LTS)**. Install the Windows, macOS, or Linux **Mono** build support module for your desired player.
2. Let Unity import packages. Open `Assets/HoverForHire/Scenes/PortMeridian.unity` and press Play. You start at home base in Free Flight, with the rotor governed and collective at zero.
3. Raise collective gently with **Left Shift**. Empty hover is around **45%**; loaded hover needs more. Use mouse or WASD cyclic, Q/E yaw, and Left Ctrl to lower collective. Brake early with aft cyclic, then reduce collective after touchdown.
4. **Escape** opens the flight desk. Choose a training drill or a 15-minute delivery shift. **Enter** accepts the offered contract. Land and remain level and still for the service dwell; loading/unloading is automatic.

If setup assets ever need regeneration, use **Hover for Hire → Prepare project**. The generated scene, tuning asset, renderer, URP settings, and input configuration are committed. No external art, sound, paid plugins, account login, or runtime downloads are needed to play.

## What is implemented

- Fixed-step Rigidbody lift, rotor torque, cyclic authority, inertia, aerodynamic drag, payload mass, compound skid contact, impact damage, and recovery.
- Independent rate stabilization, auto-level, yaw stabilization, and rotor torque compensation with Beginner, Standard, and Unassisted presets. Assists blend through bounded commands; turning them off removes their stabilization commands.
- Rebindable keyboard/mouse, gamepad, and absolute collective binding. Persistent collective, two mouse cyclic modes, free look hold/return options, saved bindings and preferences.
- Chase and cockpit cameras with free look, smoothing, collision handling, and recentering.
- Free Flight, seven training drills, passenger and internal cargo deliveries, stable loading/unloading, comfort/condition/landing/time scores, optional harder contracts, progression, and duplicate payout prevention.
- Ten pads across town, docks, industrial yard, rooftop clinic, hills and remote sites; procedural scenery, helicopter, rotor/wind/landing/service audio; HUD, map, and telemetry overlay.

## Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Cyclic | Mouse, W/S pitch, A/D roll | Left stick |
| Collective increase / decrease | Left Shift / Left Ctrl | Right / left trigger |
| Yaw | Q / E | Left / right shoulder |
| Free look | Hold Alt or middle mouse | Right stick; left-stick press for mouse-style hold |
| Chase / cockpit | V | North / Y |
| Recenter view | R | Right-stick press |
| Recenter cyclic | C | D-pad down |
| Accept / next job | Enter | South / A |
| Pause / flight desk | Escape | Start |
| Reset / retry | Backspace | Select |
| Cycle assists | F2 | D-pad up |
| Development overlay | F1 | D-pad right |

All bindings are editable from the flight desk. Mouse displacement is integrated without multiplying pixel delta by frame time. Keyboard/gamepad collective is a held power setting, not a vertical-speed command. See [control processing and hardware options](Docs/Controls.md).

## Build and test

Use **Hover for Hire → Build → Windows / macOS / Linux**. Development builds are written to `Builds/<platform>`. Windows uses x64; macOS uses Unity's standalone architecture setting; Linux uses x64. macOS signing/notarization and Linux execute permissions remain distribution tasks.

PowerShell helper (close the editor for this project first):

```powershell
$env:UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
.\Tools\Unity.ps1 Prepare
.\Tools\Unity.ps1 EditMode
.\Tools\Unity.ps1 PlayMode
.\Tools\Unity.ps1 Windows
.\Tools\Unity.ps1 macOS
.\Tools\Unity.ps1 Linux
```

On macOS/Linux use the Unity executable with equivalent arguments:

```sh
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/Artifacts/editmode.xml" -logFile "$PWD/Artifacts/editmode.log"
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -executeMethod HoverForHire.Editor.ProjectSetup.BuildLinux -quit -logFile "$PWD/Artifacts/build-linux.log"
```

The new [Unity CLI](https://unity.com/blog/meet-the-unity-cli) can also orchestrate editor builds/tests. This project uses the editor's batch interface so no experimental runtime pipeline package is required. Dependencies were checked against Unity's [6.3 URP](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.render-pipelines.universal.html) and [Input System compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.inputsystem.html) documentation, then resolved by the LTS editor: URP 17.3.0, Input System 1.20.0, Test Framework 1.6.0. `Packages/packages-lock.json` pins the resolved graph.

See [validation results and playtest checklist](Docs/Validation.md), [flight-model approximations](Docs/FlightModel.md), and [mission/scoring behavior](Docs/Missions.md).

## Limits of this slice

The flight model is deliberately simplified. Rotor RPM is governed; lift scales with collective and is tilted with the aircraft/disc. There is no wind simulation, ground effect, translational lift, autorotation, vortex-ring state, blade flapping, engine failures, or hover hold yet. Those remain deferred until the basic model has player feedback. Cockpit instruments use the shared screen HUD. Scenery, rotor audio, and aircraft art are functional procedural assets.

The project is a game and practice aid, not a certified aviation simulator. Automated checks establish invariants and detect regressions; they cannot establish that the helicopter feels satisfying. Native macOS/Linux controller, display, and audio checks require those machines.

## Layout and local data

`Assets/HoverForHire/Scripts/` separates Flight, Input, Cameras, Missions, Training, Persistence, Presentation, World, and Core. Edit `Assets/HoverForHire/Resources/UtilityHelicopter.asset` to tune the aircraft centrally. The world uses metres, mass kilograms, angular rates degrees/s, and physics forces newtons.

Progression lives in Unity's `Application.persistentDataPath` as `progression.json` with a recovery backup. Controls, binding overrides, assists, and volume use local Unity PlayerPrefs. No cloud save is used. Tests isolate their persistence data. The original brief is in [Docs/Brief.md](Docs/Brief.md).
