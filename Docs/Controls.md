# Controls and camera

All flight controls use Unity's Input System. Open the pause settings to change bindings and preferences. On-screen key hints (including collective) are built from the current bindings and switch between keyboard and gamepad names according to the device used last. On first launch a welcome panel explains collective and lets you choose a realism preset (Relaxed, Realistic or Expert; changeable later under Flight Desk → Assists / realism, where each effect can also be toggled). It then offers the Takeoff drill or free flight, and appears once. Keyboard composite directions, gamepad axes, individual buttons, mouse delta, and the initially unbound absolute collective are exposed separately. Press Escape or the gamepad menu Back binding to cancel an interactive rebind (Escape alone while changing MenuBack itself). Changes to a completed binding save immediately; save preferences after editing settings. Restoring defaults affects controls and camera preferences only, leaving progression intact.

The Flight Desk has six tabs:
* **Fly:** the offer board, modes and drills.
* **Logbook:** flight time, landings, certifications, personal bests, recent results and the livery shop.
* **Controls.**
* **Bindings.**
* **Assists / realism.**
* **View / sound:** units, camera and volume.

**Units** are *Metric* (km/h, m/s, metres) or *Aviation* (knots, feet per minute, feet, nautical miles). They apply to every HUD readout.

Pause menus support the gamepad without a mouse: D-pad or left stick up/down moves the highlighted control; left/right changes a tab, option, or slider; South selects; East resumes. Holding a direction repeats after a short delay. Long lists scroll the focused control into view. MenuMove, MenuSubmit, and MenuBack are also rebindable.

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Cyclic pitch / roll | Mouse; W/S pitch, A/D roll | Left stick |
| Yaw left / right | Q / E | Left / right shoulder |
| Increase / decrease collective | Left Shift / Left Ctrl | Right / left trigger |
| Free look | Hold Left Alt or middle mouse, then move mouse | Right stick always looks; left-stick press also holds mouse free look |
| Switch chase / cockpit | V | North face button (Y / Triangle) |
| Recenter view | R | Right-stick press |
| Center mouse cyclic | C | D-pad down |
| Pause / settings | Escape | Start / Menu |
| Reset at helipad | Backspace | Select / View |
| Accept / interact (Free Flight: start a shift) | Enter | South face button (A / Cross) |
| Cycle assist preset (announced) | F2 | D-pad up |
| Hover hold on / off | H | West face button (X / Square) |
| Development overlay | F1 | D-pad right |
| Record flight data (CSV) | F3 | — |

**Hover hold** (H) engages in the air below 5 m/s of ground speed. It stops drift and sink and keeps the heading by adding small, bounded cyclic, collective and pedal inputs to the pilot's own:
* **Authority limits:** cyclic 0.35, collective ±8%, pedals 0.3.
* **Hand-back:** moving the stick beyond 0.2, the collective beyond 3% or the pedals beyond 0.3 hands control back at once, as does pressing H again, touching down or resetting.
* **Indication:** the HUD shows HOVER HOLD, and results record HOLD among the assists.

**Attitude command** (an assist setting, off by default) makes the stick set bank and pitch, up to ±25°, instead of a rotation rate. Centering the stick flies level. It works through rate stabilization.

Mouse up commands forward pitch and mouse right commands right roll; either mouse axis can be inverted independently. Gamepad up commands forward pitch. Keyboard and trigger collective inputs change a persistent 0–100% setting, initially zero. Releasing them holds the setting. Pressing opposing inputs at equal strength cancels their adjustment; triggers adjust proportionally to pressure. Collective controls lift demand, not altitude or vertical speed. The HUD shows collective to 0.1% with an amber tick at the hover setting for the current weight.

**Digital ramps.** A held digital control (a key or gamepad button) starts gently and speeds up. Collective begins at 6%/s for fine trim and rises smoothly to the full 24%/s over 0.3 s, so one 60 fps key frame moves collective about 0.1% while a two-second hold still moves it about 45%. The travel is integrated exactly per frame, so it does not depend on frame rate. Digital pedals begin at 30% deflection and reach full deflection after 0.35 s, so a tap yaws gently. Analog triggers and axes are never ramped: they always pass their pressure straight through. All four values are adjustable in the flight desk.

**Gamepad layouts.** *Classic* (default): triggers adjust collective, shoulders are pedals. *Sim pedals*: the analog triggers become proportional pedals (left trigger = left pedal) and the shoulders adjust collective with the digital ramp. Proportional pedals make coordinated turns and precise hover heading possible on a gamepad. The layout is stored as ordinary binding overrides, so it persists with other bindings and can still be customized. Rudder-pedal hardware can bind the signed **YawAxis** action, which is added to the pedal buttons.

For an absolute hardware collective, rebind **AbsoluteCollective** to its axis and enable **UseAbsoluteCollective**. Choose signed (−1 to +1) or unsigned (0 to 1) mapping and inversion to match the hardware. The bound axis then has priority over keyboard / trigger adjustment. An unbound or disconnected absolute device falls back to persistent incremental collective. Hardware calibration beyond these range/inversion settings is deferred. The flight model only consumes `IFlightInput.Command`, so a dedicated joystick / pedal adapter can be added without changing aircraft physics.

## Mouse cyclic

**Relative** mode integrates mouse displacement into the cyclic position while it returns exponentially toward center. Return speed, sensitivity, deadzone, response curve, and inversion are configurable. Set return speed to zero to hold the displacement.

**Virtual joystick** mode holds a normalized displacement from a visible center. Moving the mouse farther moves the stick farther until it reaches its travel limit. Stopping the mouse holds that command. Press **C** to center it. The HUD indicator shows its visible center and command, so the mouse can remain captured without hitting the edge of the display.

Mouse displacement already measures movement during a frame; it is never multiplied by frame duration. Relative return uses the analytic integral of `dc/dt = sensitivity × mouse_velocity − return_rate × c`, assuming constant movement within each input sample. This produces the same command for a constant physical mouse velocity at 30, 60, and 144 FPS before travel-limit clipping. Extremely fast motion that reaches the stick limit, OS mouse acceleration, device sampling, and uneven physical movement can still create sampling differences. These are input processing choices, separate from flight assists.

Keyboard cyclic ramps toward its target with a short exponential response, then adds to the shaped mouse and gamepad command. The combined vector is limited to full stick travel. Devices never abruptly take ownership based on which moved last. Opposite commands cancel continuously; holding a key can intentionally offset a mouse command.

## Free look and camera

During mouse free look, every mouse delta goes only to the camera. Choose **Hold** to keep the current mouse cyclic contribution, or **ReturnToCenter** to let it decay using the configured return speed (minimum 0.1/s). Keyboard and gamepad cyclic remain available in both cases. The release frame's mouse delta is discarded; no free-look motion is accumulated for later flight input. Cursor lock changes, focus changes, pause transitions, and aircraft resets also discard the next mouse sample. Collective always persists while looking around.

The gamepad right stick moves the camera independently of cyclic. Camera sensitivity and look inversion are separate from flight sensitivity and inversion. Automatic smooth recentering has a configurable delay; **R** explicitly requests a smooth recenter even when automatic recentering is disabled.

The chase camera stays upright and follows heading, with configurable distance, height, field of view, and smoothing. A sphere cast from the aircraft to both the requested and damped camera positions shortens the camera distance around terrain and solid buildings. Aircraft colliders use layer 8 and are excluded. A nearby obstacle can temporarily bring the camera close to the helicopter. Cockpit view follows the cockpit mount and preserves all aircraft and input state.

On desktops where Alt combinations are intercepted by a window manager, use middle mouse or rebind free look. Gamepad alternatives avoid OS keyboard shortcuts. Mac keyboards that reserve F1/F2 can use Fn with those keys, change the bindings, or use the corresponding gamepad buttons. Focus loss pauses and releases the cursor; resuming requires an explicit pause action or menu selection.

## Local storage and checks

Controls and camera preferences use the `HoverForHire.Controls.v1` PlayerPrefs key; binding override JSON uses `HoverForHire.Bindings.v1`. Assists, realism, units, volume and first-launch state share one versioned key, `HoverForHire.Pilot.v1`. The older `hfh.assists`, `hfh.volume` and realism/first-run keys migrate into it once, then are removed. Unity stores PlayerPrefs in its platform-specific application preferences location. Overrides identify actions and original binding paths semantically, so regenerated action GUIDs at a fresh launch do not invalidate saved controls. Avoid changing action names without providing a save migration.

`InputProcessingTests` checks the collective ramp's frame-rate independence and tap resolution, the pedal ramp, the Sim pedals layout round trip, relative mouse integration at 30/60/144 FPS, virtual joystick displacement, return consistency, free-look hold/return and release suppression, simultaneous input arbitration, persistent and absolute collective, deadzone bounds, invalid preferences, and override restoration onto a freshly generated action asset. `InputDeviceRuntimeTests` injects actual Input System keyboard, gamepad, and mouse state events into isolated action/device rigs to check the resolved control commands, pause/reset, menu navigation input, and OS focus transitions. Fixture persistence is disabled before initialization, and tests verify that existing preference keys remain unchanged. `CameraRuntimeTests` checks aircraft exclusion, building collision, and camera switching without changing aircraft or input state. Human playtesting is still required for mouse feel, keyboard response, controller deadzones, visual camera clearance, cockpit visibility, and platform shortcut behavior.
