# Hover for Hire 0.2.0 — coastal graphics and dynamic impacts

This update replaces the first slice's blockout presentation with a detailed original helicopter, a richer coastal island, and a WARDOGS-inspired flight display. Flying, deliveries, training, bindings, and recovery retain their existing controls and rules.

- Original utility helicopter with a modeled cabin, canopy, rotor mechanisms, skids, livery, and five live cockpit gauges.
- Detailed airport, coastal town, harbor, freight yard, and destination landmarks; marked roads, clustered forests, boats, rocks and shore dressing.
- Textured grass/rock/sand terrain, animated coastal water, sky lighting, soft shadows and ACES grading.
- Transparent flight instruments, terrain minimap, quieter cockpit overlays and a refreshed Flight Desk, checked at 16:9 and ultrawide resolutions.
- Surface-aware rotor wash and landing dust; impact sparks, smoke, detached parts, scorched wreckage and camera shake. A fireball requires a dry impact of at least 20 m/s and 180 kJ. Water impacts produce spray and suppress fire.
- Reset restores aircraft parts and paint, clears particles and debris, and stops impact audio.

Verification: **89 automated tests passed** (62 EditMode, 27 PlayMode), including the impact/reset regression and production pad touchdown checks. Windows scripted flights, cockpit/menu captures, graded impact effects, water-fire suppression, and ultrawide layout passed with no recorded errors. The effects screenshot sequence injects explicit presentation events after the real Rigidbody flight check; it is separate from the real-collision regression test.

Downloads contain Windows x64, macOS universal (Intel/Apple silicon), and Linux x64 development players, each with a SHA-256 checksum. Extract the entire archive and keep each player's data alongside its executable. macOS/Linux players were cross-built on Windows; native launch, controller and audio checks remain outstanding. macOS signing/notarization has not been performed.

The game remains an early playable slice. Human handling feedback is still needed. Rotor wash and debris are cosmetic; aerodynamic ground effect, wind, autorotation and advanced rotor regimes remain deferred.

See [art sources and effect behavior](Art.md) and [validation detail](Validation.md).
