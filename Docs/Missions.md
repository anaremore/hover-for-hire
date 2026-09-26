# Jobs, training and local progression

The vertical slice uses the same Rigidbody aircraft in every mode. `MissionSession` and `TrainingSession` consume physics observations; they do not move the aircraft or change its controls. `MissionDirector` adapts those observations, changes payload mass through `SetPayload`, and performs explicit respawns through `ResetAt` after clearing the persistent input command.

## Delivery shifts

A shift lasts 15 minutes of game time; pausing stops the timer. The first two offers are **Town connection** (160 kg of passengers, home base to town) and **Ferry provisions** (230 kg of internal cargo, town to the ferry dock). Both are immediately available. After two successful deliveries, workshop, clinic and orchard offers unlock. After four, ridge, summit, lighthouse and east cove offers unlock. The original helicopter remains unchanged.

From Free Flight, the Interact key starts a shift: immediately when the aircraft is on the home pad, otherwise after a second press within four seconds (the shift begins at home base). `BrowseNextJob()` cycles unlocked offers before acceptance, so the pilot can continue taking the generous beginner routes. `AcceptNextJob()` accepts the displayed offer; after a delivered job it selects and accepts the next offer. Loading and unloading are automatic when the service conditions are met. `Interact()` accepts an offer or retries a failed job; it never bypasses service conditions.

Each contract supplies pickup and destination, passenger/cargo type, payload mass, base pay, expected duration, deadline and service limits. Early contracts allow three times their expected duration before failure. Later contracts allow twice the expected duration. Expected duration includes generous takeoff and landing time plus route distance; it is a score target, not a countdown to failure. Retries keep the shift clock running.

The explicit lifecycle is:

```text
Available → Accepted → Pickup → Transport → Delivered
                 active states → Failed → Accepted (retry)
```

A service dwell requires all of the following continuously:

- Actual supported ground contact, no crash, and the aircraft origin within the pad radius.
- Origin 0.2–3.2 m above that pad's surface, preventing contact on another floor from counting. The modeled skid contact is 1.5 m below the fuselage origin.
- Ground speed ≤0.8 m/s, absolute vertical speed ≤0.5 m/s and tilt ≤8°.
- Three seconds without a failed condition. Advanced jobs tighten ground speed to 0.5 m/s, tilt to 6° and dwell to four seconds.

Any failed condition resets the dwell. Flyovers and brief collisions cannot load or deliver. Pickup applies payload mass; delivery, failure, mode change and reset clear it. A retry starts a fresh attempt at the pickup, requiring loading again. A completed attempt can be claimed only once, and retry cannot reopen it.

Scores weight elapsed time (30%), pad placement (25%), worst loaded touchdown speed (25%) and passenger comfort or cargo condition (20%). Routine acceleration is free; sustained harsh acceleration, high rotation rates and large bank angles reduce comfort. Strong acceleration and impacts reduce cargo condition. Grades are A ≥90, B ≥78, C ≥62, otherwise D. Payment is base pay multiplied by 0.65–1.30 according to score. Feedback identifies the most useful improvement. There are no sling loads or walking passengers.

## Training

Twelve drills offer immediate retry. The first seven teach basic handling and begin grounded at home base. Landing drills first require departure, so simply sitting on the starting pad cannot complete them.

The last five teach the realism skills. Each one layers the effect it teaches on top of the player's own realism settings, so a Relaxed pilot can still practise it without changing preset. Drills that begin in the air start level, at the stated speed, with collective already set to hold height.

| Drill | Observable completion condition |
| --- | --- |
| Takeoff | Hold 8–12 m above the home surface, ≤2 m/s ground speed and ≤1 m/s vertical speed, near the pad for 4 s. |
| Hover | Hold the same height within 6 m of pad center for 12 uninterrupted seconds. |
| Yaw | Hold a heading 90° right of the starting heading, within 10°, at 8–15 m for 5 s. |
| Forward flight | Travel 100 m forward, stay within 30 m of the track and hold 8–22 m/s with controlled height for 3 s. |
| Braking | Accelerate to 12 m/s above 6 m, then hold below 2 m/s at 6–25 m for 4 s; record stopping distance. |
| Approach | Depart 80 m and climb above 12 m, return, touch down at ≤1.8 m/s and remain stable for 3 s. |
| Precision landing | Depart 20 m and climb above 5 m, return within 2.5 m of pad center, touch down at ≤1 m/s and remain stable for 3 s. |
| Crosswind landing | Moderate gusty wind from the east (8 m/s at 10 m). Hover at 8–12 m within 6 m of pad center for 8 s. Then land within 4 m of center at ≤1.5 m/s and hold for 3 s. |
| Heavy lift | Maximum payload, with power limits on. Lift off and hold 20–30 m for 5 s. Rotor RPM below 95% for more than 3 s fails the drill; torque above 100% costs points. |
| Settling with power | Vortex-ring physics on. Starts in a hover 220 m above home. Lower collective into a vertical descent until settling develops. Then recover with forward airspeed (≥10 m/s, settling gone, sink ≤2 m/s for 2 s) while still above 20 m. |
| Autorotation | Power limits on. Starts 520 m east of home at 180 m, flying west at 25 m/s; the engine fails after 3 s. Touch down, stop level (≤1.5 m/s) and hold for 2 s. Scored on touchdown speed and time with rotor RPM outside 90–110%. |
| Confined area | Starts 150 m south of and 60 m above Ridge Station. Land within 3 m of its center among the slopes at ≤1.2 m/s, without a rotor strike, and hold for 3 s. |

Training measures the following, as appropriate to the drill:
* mean hover position error and heading error;
* touchdown impact and placement;
* braking distance;
* peak torque and time with low rotor RPM;
* height lost in a settling-with-power recovery;
* elapsed time. Completion time contributes at most a five point deduction: accuracy and control take priority over rushing. Continuous holds restart when the target is lost. Drills fail on a crash, reset, overly hard required landing, or after five minutes, with actionable feedback. A failed drill never creates a completion record.

Training height bands measure upright skid clearance above the home pad, accounting for the 1.5 m origin-to-skid offset. The HUD measures skid clearance over the surface directly beneath the aircraft; those agree above the home pad, while terrain changes during departure can make local AGL differ from height above home.

## Persistence and result integrity

`progression.json` is stored in Unity's per-user `Application.persistentDataPath`, independently from input/settings persistence. Results record all distinct assist configurations used during a challenge, the realism in force (for example `REALISTIC` or `CUSTOM: GE ETL POWER`), grade, score, time, condition and landing metrics. Older saves without a realism field load unchanged. Training records are separate from paid deliveries and do not unlock contracts.

The save writes a fully flushed temporary file, replaces the primary file and retains a backup. Loading rejects missing/unsupported schema and invalid data, then tries the backup. A partial temporary write is never treated as completed progression. Platforms without `File.Replace` use a recoverable backup/move sequence. A corrupt primary does not overwrite the valid backup during recovery. Save failures keep progress in memory, expose `SaveWarning`, and retry on the next result, application pause or quit. The most recent 100 challenge details are retained; attempt IDs are retained for duplicate-payment protection.

Active missions and shift clocks are intentionally not restored after restart. Completed results, earnings, delivery unlocks and completed training drills persist. A restart starts free flight, allowing an immediate clean return to flying.

## UI adapter

The primary public surface is `Mode`, `CurrentObjective`, `StatusText`, `TargetZone`, `ObjectivePosition`, `RemainingSeconds`, `Payload`, `DwellProgress`, `Earnings`, `Grade`, `ShiftGrade`, `DeliveriesThisShift`, `CompletedDeliveries`, `LifetimeEarnings`, `LastResult`, `SaveWarning`, `TrainingName`, `TrainingFeedback` and `TrainingProgress`. `FeedbackEvent` provides pickup/delivery/retry announcements; `ResultRecorded` provides completed challenge details. The director samples in fixed updates and therefore follows Unity's paused/scaled game clock.

## Verification

Edit Mode tests cover passenger/cargo round trips, grounded/elevation checks, every stability condition resetting dwell, crash/deadline failure, retry requiring a fresh pickup, terminal payout guards, rough handling scoring, persisted duplicate IDs, JSON round trip, backup recovery after corruption or interrupted replacement, training without payment, every drill's completion path (including crosswind, heavy lift, settling with power, autorotation and the confined area measured against its own pad), uninterrupted hover, and rejection of stationary or hard landing completion. `RealismRuntimeTests` flies the realism techniques with physics: an autorotation landing at 0.9 m/s and an autopilot crosswind landing 0.9 m from center.

`FlownDeliveryTests` flies a complete passenger job over the production island with no relocation: a diagnostic autopilot supplies ordinary bounded pilot commands (cyclic, pedals, collective) to the real aircraft, which loads at home base, flies about 390 m to Town Green, lands and unloads (65–71 s of flight, touchdown ≈0.5 m/s, grade A). The same autopilot lands the smoke flight back at home base instead of teleporting it.

Play Mode integration tests use the actual aircraft, skid/pad colliders and director with scripted physics. They exercise a passenger and cargo sequence, loaded mass, uninterrupted grounded dwell, persisted payouts, repeated post-delivery reset, external reset failure and retry, shift expiry while unloading, and an unrelated roof above the destination. Each fixture sets `ProgressionPathOverride` before its inactive manager awakens, keeping all test saves separate from player progression. Fixture relocation between pads isolates service integration from route handling.

Runtime playtesting should also check the first passenger and cargo job using actual physics contacts, holding just outside a pad, bouncing during loading, crashing with a payload, retrying after a completed delivery, and restarting after changed assists. Automated core tests establish state integrity; they do not establish handling quality.
