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

## Landing and impact effects

Rotor wash samples the surface below the aircraft and follows rotor speed, collective and distance from the surface. Pavement gets light gray grit, natural ground gets tan dust, and water gets mist. These particles are cosmetic; aerodynamic ground effect remains deferred.

Contact events carry the incoming normal speed, aircraft mass, position and direction. Increasing impact severity adds sparks, smoke and broken parts. A fireball requires a dry impact of at least **20 m/s** and **180 kJ** of normal impact energy. Water suppresses fire and adds spray; the crashed Rigidbody continues sinking. Slow tip-overs produce a grounded wreck without an explosion. This is a game effect classification, not a fuel or structural-failure simulation.

Effect systems have bounded particle counts, with at most four detached cosmetic assemblies. Debris can bounce off world surfaces without colliding with the aircraft or affecting delivery state. Reset restores hidden meshes, removes debris, clears particles and camera shake, and stops impact audio.
