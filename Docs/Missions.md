# Jobs, training and local progression

The vertical slice uses the same Rigidbody aircraft in every mode. `MissionSession` and `TrainingSession` consume physics observations; they do not move the aircraft or change its controls. `MissionDirector` adapts those observations, changes payload mass through `SetPayload`, and performs explicit respawns through `ResetAt` after clearing the persistent input command.

## Delivery shifts

A shift lasts 15 minutes of game time; pausing stops the timer. The first two offers are **Town connection** (160 kg of passengers, home base to town) and **Ferry provisions** (230 kg of internal cargo, town to the ferry dock). Both are immediately available. After two successful deliveries, workshop, clinic and orchard offers unlock. After four, ridge, summit, lighthouse and east cove offers unlock. The original helicopter remains unchanged.

`BrowseNextJob()` cycles unlocked offers before acceptance, so the pilot can continue taking the generous beginner routes. `AcceptNextJob()` accepts the displayed offer; after a delivered job it selects and accepts the next offer. Loading and unloading are automatic when the service conditions are met. `Interact()` accepts an offer or retries a failed job; it never bypasses service conditions.

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

All seven drills begin grounded at home base and offer immediate retry. Landing drills first require departure, so simply sitting on the starting pad cannot complete them. This also avoids dropping the player into an airborne aircraft with an unset collective.

| Drill | Observable completion condition |
| --- | --- |
| Takeoff | Hold 8–12 m above the home surface, ≤2 m/s ground speed and ≤1 m/s vertical speed, near the pad for 4 s. |
| Hover | Hold the same height within 6 m of pad center for 12 uninterrupted seconds. |
| Yaw | Hold a heading 90° right of the starting heading, within 10°, at 8–15 m for 5 s. |
| Forward flight | Travel 100 m forward, stay within 30 m of the track and hold 8–22 m/s with controlled height for 3 s. |
| Braking | Accelerate to 12 m/s above 6 m, then hold below 2 m/s at 6–25 m for 4 s; record stopping distance. |
| Approach | Depart 80 m and climb above 12 m, return, touch down at ≤1.8 m/s and remain stable for 3 s. |
| Precision landing | Depart 20 m and climb above 5 m, return within 2.5 m of pad center, touch down at ≤1 m/s and remain stable for 3 s. |

Training measures mean hover position error, heading error, touchdown impact, placement, braking distance and elapsed time as appropriate. Completion time contributes at most a five point deduction: accuracy and control take priority over rushing. Continuous holds restart when the target is lost. Drills fail on a crash, reset, overly hard required landing, or after five minutes, with actionable feedback. A failed drill never creates a completion record.

Training height bands measure upright skid clearance above the home pad, accounting for the 1.5 m origin-to-skid offset. The HUD measures skid clearance over the surface directly beneath the aircraft; those agree above the home pad, while terrain changes during departure can make local AGL differ from height above home.

## Persistence and result integrity

`progression.json` is stored in Unity's per-user `Application.persistentDataPath`, independently from input/settings persistence. Results record all distinct assist configurations used during a challenge, along with grade, score, time, condition and landing metrics. Training records are separate from paid deliveries and do not unlock contracts.

The save writes a fully flushed temporary file, replaces the primary file and retains a backup. Loading rejects missing/unsupported schema and invalid data, then tries the backup. A partial temporary write is never treated as completed progression. Platforms without `File.Replace` use a recoverable backup/move sequence. A corrupt primary does not overwrite the valid backup during recovery. Save failures keep progress in memory, expose `SaveWarning`, and retry on the next result, application pause or quit. The most recent 100 challenge details are retained; attempt IDs are retained for duplicate-payment protection.

Active missions and shift clocks are intentionally not restored after restart. Completed results, earnings, delivery unlocks and completed training drills persist. A restart starts free flight, allowing an immediate clean return to flying.

## UI adapter

The primary public surface is `Mode`, `CurrentObjective`, `StatusText`, `TargetZone`, `ObjectivePosition`, `RemainingSeconds`, `Payload`, `DwellProgress`, `Earnings`, `Grade`, `ShiftGrade`, `DeliveriesThisShift`, `CompletedDeliveries`, `LifetimeEarnings`, `LastResult`, `SaveWarning`, `TrainingName`, `TrainingFeedback` and `TrainingProgress`. `FeedbackEvent` provides pickup/delivery/retry announcements; `ResultRecorded` provides completed challenge details. The director samples in fixed updates and therefore follows Unity's paused/scaled game clock.

## Verification

Edit Mode tests cover passenger/cargo round trips, grounded/elevation checks, every stability condition resetting dwell, crash/deadline failure, retry requiring a fresh pickup, terminal payout guards, rough handling scoring, persisted duplicate IDs, JSON round trip, backup recovery after corruption or interrupted replacement, training without payment, every drill's completion path, uninterrupted hover, and rejection of stationary or hard landing completion.

Play Mode integration tests use the actual aircraft, skid/pad colliders and director with scripted physics. They exercise a passenger and cargo sequence, loaded mass, uninterrupted grounded dwell, persisted payouts, repeated post-delivery reset, external reset failure and retry, shift expiry while unloading, and an unrelated roof above the destination. Each fixture sets `ProgressionPathOverride` before its inactive manager awakens, keeping all test saves separate from player progression. Fixture relocation between pads isolates service integration from route handling.

Runtime playtesting should also check the first passenger and cargo job using actual physics contacts, holding just outside a pad, bouncing during loading, crashing with a payload, retrying after a completed delivery, and restarting after changed assists. Automated core tests establish state integrity; they do not establish handling quality.
