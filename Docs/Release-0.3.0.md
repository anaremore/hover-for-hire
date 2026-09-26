# Hover for Hire 0.3.0: a trainer's flight model, and a reason to fly

This release reworks the whole game around learning to fly a helicopter well. It is closer to Arma or WARDOGS than to an arcade game.
- **Flight:** the helicopter now climbs, turns and settles like one.
- **Realism:** optional effects teach real techniques.
- **HUD:** designed for landing.
- **Sound:** synthesized from the flight model.
- **Work:** jobs have a par time and certifications to earn.
- **Speed:** the game runs about half again as fast.

Your controls, settings and progress carry over from 0.2.

## Flying
- **Handling.**
  - Hover height holds instead of hunting: +1% collective settles to about 0.5 m/s of climb within about 6 s, where 0.2 ran away.
  - Banked turns turn instead of sliding.
  - Unassisted rolls stop when you stop.
- **Collective.** Fine-then-coarse keyboard collective and pedals, and a hover mark on the collective gauge.
- **Gamepad.** A *Sim pedals* layout with analog trigger pedals.
- **Complete collision.** Trees, rocks, props and roofs are solid. The main and tail rotors can strike them. The sea is a surface you can ditch in. The crash panel names the cause, the value and the limit.
- **Beginner aids.** Hover hold (H) stops drift and sink with small inputs and hands control back the moment you move a control. Attitude command (optional) makes the stick set bank and pitch instead of a rate.

## Realism: optional, in three presets
Choose **Relaxed**, **Realistic** or **Expert** on first launch or in the Flight Desk, or toggle each effect yourself. The effects:
- ground effect and translational lift;
- speed stability;
- vortex ring state;
- finite engine power with a governed rotor;
- engine failures and autorotation;
- tail-rotor failures;
- a seeded island wind with gusts.

Relaxed keeps 0.2's forgiving power and calm air. Five new drills teach these effects: crosswind landing, heavy lift, settling with power, autorotation and confined-area landing. That makes twelve drills in all.

## HUD, units and sound
- **HUD.** A decluttered chase HUD keeps the aircraft clear. It has:
  - airspeed through the air, with ground speed and vertical speed beside the tapes;
  - a compact attitude indicator;
  - a collective gauge with hover marks in and out of ground effect;
  - torque and rotor RPM gauges;
  - a wind arrow;
  - a job clock against par.
- **Hover display.** Near a pad it shows drift, drift trend, the pad's position and a service ring that turns green when a landing would count.
- **Units.** *Metric* or *Aviation* units everywhere: readouts, objectives, drill feedback and crash reports.
- **Sound.** Synthesized in real time from the flight model:
  - blade-pass rotor, with slap in descents and in the vortex ring;
  - a turbine that runs down after an engine failure;
  - gearbox, airflow, skid scrape and a low-rotor horn.

## Jobs and progression
- **Offer board.** Pick from up to three jobs, each with its load, distance, par, pay and the wind at the destination. The HUD's job clock counts against par.
- **Certifications.** Earned in the drills: Rooftop, Mountain, Coastal and Emergency. They open the demanding pads, including a new summit medical evacuation.
- **Logbook.** Flight time, landings, route and drill bests, and recent results.
- **Liveries.** Four paint schemes, bought with earnings.
- **Pads.** They now show the job: passengers or crates wait at the pickup, and the destination has a strobe beacon, marker smoke and edge lights.
- **Cockpit.** Rebuilt for looking out:
  - a low, narrow instrument panel;
  - chin windows and thinner frames;
  - five large live dials: airspeed, radar altitude, vertical speed, torque and rotor speed.

  The model was mirrored in every earlier build (dials reversed, tail rotor on the wrong side); it is now the right way round.

## Performance and options
- **Speed.** The HUD costs about a third of what it did. On the test machine's development player, the main thread fell from 3.3 to 1.9 ms per frame, and the town view's 1%-low frame rate roughly doubled. The release player runs at 1.5 ms.
- **Startup.** The world builds a quarter faster.
- **Graphics options.** Low, Medium, High and Ultra presets, plus VSync, a frame-rate cap, window mode and resolution. Low roughly halves the GPU time.
- **Benchmark.** Run the game with `-hover-benchmark <folder>` to measure it, and include the results in a performance report.

## Verification
- **Tests.** **183 automated tests** pass: 120 EditMode and 63 PlayMode. They cover:
  - handling envelopes on the shipped tuning;
  - each realism effect, flown by physics;
  - a complete passenger delivery flown by an autopilot;
  - settings and progress migrations;
  - graphics presets.
- **Smoke runs.** Windows runs at 30, 60 and 144 fps fly, land, run the menus and drills, and capture the art tour with zero recorded errors.
- **Benchmarks.** Recorded in [Validation.md](Validation.md).

## Downloads
The downloads are **release players** for:
- Windows x64;
- macOS (universal: Intel and Apple silicon);
- Linux x64.

Each archive, `Hover-for-Hire-0.3.0-<platform>.zip`, has a SHA-256 checksum. Extract the whole archive and keep each player's data beside its executable. The macOS app is signed ad hoc, not notarized; see [macOS.md](macOS.md) to open it.

## Known limits
- **No human has flown this build yet.** The tuning comes from simulation and tests; feel reports are the most useful feedback. Press F3 to record a flight (CSV) and attach it.
- **Native testing.** The macOS and Linux players were built on Windows and still need native checks: launch, controllers, audio and saves.
- **One unexplained crash.** A single development-build run at 144 fps crashed natively inside Unity during testing; it has not recurred in later runs.
- **Sound.** It is synthesized only; recorded rotor samples are not included yet.
