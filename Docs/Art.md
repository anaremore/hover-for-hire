# Art direction and sources

Version 0.2 adds a more grounded coastal aviation look: an original utility helicopter with a modeled cockpit, warm orange commercial livery, detailed island settlements, layered terrain, moving coastal water, and late-afternoon lighting. Flight instruments draw on the restrained compass and physical cockpit presentation in [WARDOGS' official helicopter screenshot](https://www.team17.com/hs-fs/hubfs/WD_Screenshot_Helicopter_1_WD1.jpg?length=2000&name=WD_Screenshot_Helicopter_1_WD1.jpg). No WARDOGS assets are included.

The helicopter mesh is authored with the reproducible Blender script in `Tools/Art/build_helicopter.py`; the FBX is included, so Blender is not needed to open or build the Unity project. Island architecture, roads, trees, rocks, landmark details, sky, water, HUD and effects are original procedural assets/code.

The following 1K albedo and OpenGL normal maps are from Poly Haven. They are distributed under [CC0](https://polyhaven.com/license), which permits redistribution and commercial use. Downloaded files were checked against the MD5 values returned by the official public API.

| Surface | Source |
| --- | --- |
| Grass and ground | [Aerial Grass Rock](https://polyhaven.com/a/aerial_grass_rock) |
| Rocky slopes | [Rocky Terrain 02](https://polyhaven.com/a/rocky_terrain_02) |
| Sand and shore | [Aerial Beach 01](https://polyhaven.com/a/aerial_beach_01) |
| Road/apron asphalt | [Asphalt 02](https://polyhaven.com/a/asphalt_02) |

Maps live in `Assets/HoverForHire/Resources/Art/Textures`. The terrain shader blends grass, triplanar rock and sand using mesh vertex weights. Texture imports use mipmaps, anisotropic filtering and desktop GPU compression. Water is opaque stylized shading with animated wave normals, sunlight, Fresnel reflections and foam driven by sampled island height; it is not a fluid simulation.

Lighting uses four cascaded soft sun shadows, a sky reflection probe, ACES tonemapping and restrained bloom. The custom sky and water shaders are written for URP and the three desktop graphics backends.

## Sound

All aircraft sound is synthesized in real time by `RotorSoundSynth` on the audio thread; no recordings are included. The layers follow the flight model:
* **Blade-pass pulses:** four blades at 395 rpm give a 26 Hz line with harmonics. They are low-passed into the rotor's "whop", with band-passed blade swish gated at the same rate. Pitch follows rotor RPM; level follows rotor load.
* **Blade slap:** an impulsive crack at every blade pass. It is driven by blade-vortex interaction in a moderate descent at 6–40 m/s, by a vortex ring, and by high load (pull-ups, steep turns, flares).
* **Turbine:** 4.1 and 6.2 kHz compressor tones and a hiss. After an engine failure they run down over a few seconds.
* **Other layers:** main-gearbox mesh whine; airflow noise that grows and brightens with airspeed and turbulence; skid scrape; a pulsed 520 Hz low-rotor-RPM horn.

Outside, the sound comes from the aircraft as a positional source. In the cockpit it is 2D and low-passed, with more rotor and gearbox and less turbine. The Flight Desk ducks it. The touchdown thump and service chime are short synthesized one-shots.

## Landing and impact effects

Rotor wash samples the surface below the aircraft and follows rotor speed, collective and distance from the surface. Pavement gets light gray grit, natural ground gets tan dust, and water gets mist. These particles are cosmetic (ground effect is modeled separately in the flight model), and the wind carries them downwind.

Contact events carry the incoming normal speed, aircraft mass, position and direction. Increasing impact severity adds sparks, smoke and broken parts. A fireball requires a dry impact of at least **20 m/s** and **180 kJ** of normal impact energy. Water suppresses fire and adds spray; the crashed Rigidbody continues sinking. Slow tip-overs produce a grounded wreck without an explosion. This is a game effect classification, not a fuel or structural-failure simulation.

Rotors never strobe: once the rotor is above 40% of governed speed, the blades advance by at most 42% of the blade spacing per rendered frame (below the rate where the eye reads frozen or reversed blades at 30–144 fps), and a procedural streaked motion disc fades in on both rotors. Detached wreckage never carries the motion discs.

Scenery rests on the actual triangulated terrain mesh (`IslandWorld.MeshHeight`), so roads and props no longer float or sink between terrain samples; road edges have skirts. Buildings beside the cut ridge and summit pads, the lighthouse keeper's house, the orchard farm and the cove houses take the flattest nearby footing. Trees, lamp posts and parked vehicles stay out of each pad's rotor-reach ring, and windsocks stand 7 m beyond the pad edge.

Effect systems have bounded particle counts, with at most four detached cosmetic assemblies. Debris can bounce off world surfaces without colliding with the aircraft or affecting delivery state. Reset restores hidden meshes, removes debris, clears particles and camera shake, and stops impact audio.
