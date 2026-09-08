# Controls and camera

All flight controls use Unity's Input System. Open the pause settings to change bindings and preferences. Keyboard composite directions, gamepad axes, individual buttons, mouse delta, and the initially unbound absolute collective are exposed separately. Press Escape to cancel an interactive rebind. Changes to a completed binding save immediately; save preferences after editing settings. Restoring defaults affects controls and camera preferences only, leaving progression intact.

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
| Accept / interact | Enter | South face button (A / Cross) |
| Cycle assist preset | F2 | D-pad up |
| Toggle explicit hover hold | H | West face button (X / Square) |
| Development overlay | F1 | D-pad right |

Mouse up commands forward pitch and mouse right commands right roll; either mouse axis can be inverted independently. Gamepad up commands forward pitch. Keyboard and trigger collective inputs change a persistent 0–100% setting, initially zero. Releasing them holds the setting. Pressing opposing inputs at equal strength cancels their adjustment; triggers adjust proportionally to pressure. Collective controls lift demand, not altitude or vertical speed.

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

Controls and camera preferences use the `HoverForHire.Controls.v1` PlayerPrefs key; binding override JSON uses `HoverForHire.Bindings.v1`. Unity stores PlayerPrefs in its platform-specific application preferences location. Overrides identify actions and original binding paths semantically, so regenerated action GUIDs at a fresh launch do not invalidate saved controls. Avoid changing action names without providing a save migration.

`InputProcessingTests` checks relative mouse integration at 30/60/144 FPS, virtual joystick displacement, return consistency, free-look hold/return and release suppression, simultaneous input arbitration, persistent and absolute collective, deadzone bounds, invalid preferences, and override restoration onto a freshly generated action asset. Human playtesting is still required for mouse feel, keyboard response, controller deadzones, visual camera clearance, cockpit visibility, and platform shortcut behavior.
