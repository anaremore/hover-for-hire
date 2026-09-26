using System.Collections.Generic;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Cues in the world instead of more HUD. Passengers or cargo wait beside the pickup pad and board once loading
    /// completes; the pad the pilot is flying to shows a strobe beacon, orange smoke that drifts with the wind, and
    /// amber edge lights. Cosmetic only: nothing here collides, and everything stands outside the rotor's reach.
    /// </summary>
    public sealed class PadCues : MonoBehaviour
    {
        public MissionDirector Missions;
        public HelicopterController Aircraft;

        /// <summary>Pad marked by the beacon, or null.</summary>
        public LandingZone BeaconZone { get; private set; }
        public bool BeaconVisible => beacon != null && beacon.activeSelf;
        /// <summary>People or crates still waiting (not yet boarded).</summary>
        public int WaitingCount { get; private set; }

        private const float BoardingSeconds = 1.4f;
        private static readonly Color[] Jackets =
        {
            new Color(.18f, .32f, .52f), new Color(.62f, .2f, .16f), new Color(.24f, .42f, .28f), new Color(.72f, .62f, .38f), new Color(.3f, .3f, .34f)
        };

        private readonly List<Material> materials = new List<Material>();
        private readonly List<Transform> waiting = new List<Transform>();
        private readonly List<Vector3> waitingHome = new List<Vector3>();
        private readonly List<Renderer> edgeLights = new List<Renderer>();
        private GameObject beacon, waitingGroup;
        private Material lampMaterial, edgeMaterial;
        private Light flash;
        private ParticleSystem smoke;
        private Texture2D puffTexture;
        private string waitingAttempt;
        private float boardingTime = -1f;

        private void Update() => Refresh(Time.deltaTime);

        /// <summary>Follow the mission state. Called every frame; tests call it directly.</summary>
        public void Refresh(float deltaSeconds)
        {
            if (Missions == null) return;
            UpdateBeacon(Missions.TargetZone);
            UpdateWaiting(deltaSeconds);
        }

        // ---- Beacon, smoke and edge lights at the target pad ----

        private void UpdateBeacon(LandingZone target)
        {
            if (target == null)
            {
                if (beacon != null) beacon.SetActive(false);
                BeaconZone = null;
                return;
            }
            if (beacon == null) BuildBeacon();
            if (target != BeaconZone)
            {
                BeaconZone = target;
                Vector3 pad = target.transform.position;
                float radius = target.Radius;
                beacon.transform.position = pad;
                // Opposite the windsock (which stands on +X): behind-left of the pad, beyond rotor reach.
                beacon.transform.GetChild(0).position = Ground(pad + new Vector3(-1f, 0f, -1f).normalized * (radius + 3.5f), pad.y);
                for (int i = 0; i < edgeLights.Count; i++)
                {
                    float angle = (i + 0.5f) / edgeLights.Count * Mathf.PI * 2f;
                    // Just outside the painted perimeter markers (which sit one metre inside the pad edge).
                    edgeLights[i].transform.position = pad + new Vector3(Mathf.Sin(angle) * (radius - 0.4f), 0.08f, Mathf.Cos(angle) * (radius - 0.4f));
                }
                smoke.Clear();
            }
            beacon.SetActive(true);
            // A double strobe every 1.2 s, and slowly breathing edge lights.
            float phase = Mathf.Repeat(Time.time, 1.2f);
            bool lit = phase < 0.08f || (phase > 0.2f && phase < 0.28f);
            lampMaterial.SetColor("_EmissionColor", lit ? new Color(6f, 5.2f, 4f) : new Color(.15f, .1f, .05f));
            flash.intensity = lit ? 6f : 0f;
            float glow = 1.2f + 0.8f * Mathf.Sin(Time.time * 2.2f);
            edgeMaterial.SetColor("_EmissionColor", new Color(1f, .62f, .16f) * glow);
            WindField wind = WindSystem.Current;
            Vector3 drift = wind != null ? wind.WindAt(smoke.transform.position + Vector3.up * 3f) : Vector3.zero;
            ParticleSystem.VelocityOverLifetimeModule velocity = smoke.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(drift.x * 0.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(drift.z * 0.8f);
        }

        private void BuildBeacon()
        {
            beacon = new GameObject("Target pad cues");
            beacon.transform.SetParent(transform, false);
            var post = new GameObject("Strobe beacon").transform;
            post.SetParent(beacon.transform, false);
            Part(PrimitiveType.Cylinder, post, new Vector3(0f, 1.5f, 0f), new Vector3(.12f, 1.5f, .12f), Lit(new Color(.2f, .21f, .22f)));
            lampMaterial = Lit(new Color(1f, .95f, .85f), true);
            Part(PrimitiveType.Sphere, post, new Vector3(0f, 3.15f, 0f), Vector3.one * .38f, lampMaterial);
            flash = new GameObject("Strobe light").AddComponent<Light>();
            flash.transform.SetParent(post, false);
            flash.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            flash.type = LightType.Point;
            flash.range = 28f;
            flash.color = new Color(1f, .92f, .8f);
            flash.shadows = LightShadows.None;

            smoke = new GameObject("Marker smoke").AddComponent<ParticleSystem>();
            smoke.transform.SetParent(post, false);
            smoke.transform.localPosition = new Vector3(.6f, .2f, 0f);
            ParticleSystem.MainModule main = smoke.main;
            main.startLifetime = 5f;
            main.startSpeed = 0.4f;
            main.startSize = 1.2f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, .45f, .1f, .55f);
            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .25f;
            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 3.5f));
            ParticleSystem.ColorOverLifetimeModule color = smoke.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(new Color(1f, .5f, .12f), 0f), new GradientColorKey(new Color(1f, .7f, .45f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.6f, .12f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystem.VelocityOverLifetimeModule velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SmokeMaterial();

            edgeMaterial = Lit(new Color(1f, .6f, .15f), true);
            for (int i = 0; i < 8; i++) edgeLights.Add(Part(PrimitiveType.Cube, beacon.transform, Vector3.zero, new Vector3(.4f, .12f, .4f), edgeMaterial));
        }

        // ---- People or crates waiting at the pickup ----

        private void UpdateWaiting(float deltaSeconds)
        {
            MissionSession job = Missions.Mode == GameMode.DeliveryShift ? Missions.CurrentMission : null;
            bool waitingPhase = job != null && (job.State == MissionState.Available || job.State == MissionState.Accepted || job.State == MissionState.Pickup);
            bool boardingPhase = job != null && job.State == MissionState.Transport && boardingTime >= 0f;
            if (job != null && job.State == MissionState.Transport && boardingTime < 0f && WaitingCount > 0) { boardingTime = 0f; boardingPhase = true; }

            if (waitingPhase)
            {
                string key = job.AttemptId + job.Contract.Id;
                if (key != waitingAttempt) BuildWaiting(job.Contract, key);
                return;
            }
            if (boardingPhase)
            {
                boardingTime += Mathf.Max(0f, deltaSeconds);
                Vector3 target = Aircraft != null ? Aircraft.transform.position : waitingGroup.transform.position;
                int remaining = 0;
                for (int i = 0; i < waiting.Count; i++)
                {
                    float t = Mathf.Clamp01((boardingTime - i * .18f) / BoardingSeconds);
                    Vector3 door = new Vector3(target.x, waitingHome[i].y, target.z);
                    waiting[i].position = Vector3.Lerp(waitingHome[i], door, t * t * (3f - 2f * t));
                    bool boarded = t >= 1f;
                    if (waiting[i].gameObject.activeSelf == boarded) waiting[i].gameObject.SetActive(!boarded);
                    if (!boarded) remaining++;
                }
                WaitingCount = remaining;
                if (remaining == 0) boardingTime = -1f;
                return;
            }
            ClearWaiting();
        }

        private void BuildWaiting(ContractDefinition contract, string key)
        {
            ClearWaiting();
            waitingAttempt = key;
            boardingTime = -1f;
            LandingZone zone = FindZone(contract.Pickup.Id);
            if (zone == null) return;
            if (waitingGroup == null)
            {
                waitingGroup = new GameObject("Waiting at pickup");
                waitingGroup.transform.SetParent(transform, false);
            }
            waitingGroup.SetActive(true);
            Vector3 pad = zone.transform.position;
            // Front-right of the pad, beyond the rotor's reach from anywhere on it.
            Vector3 outward = new Vector3(Mathf.Sin(40f * Mathf.Deg2Rad), 0f, Mathf.Cos(40f * Mathf.Deg2Rad));
            Vector3 across = new Vector3(outward.z, 0f, -outward.x);
            Vector3 spot = pad + outward * (zone.Radius + 6f);
            bool passengers = contract.Type == ContractType.Passengers;
            int count = Mathf.Clamp(Mathf.RoundToInt(contract.PayloadKg / (passengers ? 80f : 90f)), 1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector3 place = Ground(spot + across * ((i - (count - 1) * .5f) * 1.1f), pad.y);
                Transform item = passengers ? Person(place, pad, i) : Crate(place, i);
                waiting.Add(item);
                waitingHome.Add(item.position);
            }
            WaitingCount = waiting.Count;
        }

        private Transform Person(Vector3 feet, Vector3 facing, int index)
        {
            var person = new GameObject("Waiting passenger " + (index + 1)).transform;
            person.SetParent(waitingGroup.transform, false);
            person.position = feet;
            Vector3 look = new Vector3(facing.x - feet.x, 0f, facing.z - feet.z);
            if (look.sqrMagnitude > .01f) person.rotation = Quaternion.LookRotation(look);
            Part(PrimitiveType.Capsule, person, new Vector3(0f, .78f, 0f), new Vector3(.46f, .72f, .36f), Lit(Jackets[index % Jackets.Length]));
            Part(PrimitiveType.Sphere, person, new Vector3(0f, 1.66f, 0f), Vector3.one * .26f, Lit(new Color(.72f, .56f, .45f)));
            if (index % 2 == 0) Part(PrimitiveType.Cube, person, new Vector3(.3f, .45f, .05f), new Vector3(.18f, .34f, .42f), Lit(new Color(.16f, .16f, .18f)));
            return person;
        }

        private Transform Crate(Vector3 ground, int index)
        {
            var crate = new GameObject("Waiting cargo " + (index + 1)).transform;
            crate.SetParent(waitingGroup.transform, false);
            crate.position = ground;
            crate.rotation = Quaternion.Euler(0f, index * 17f, 0f);
            Part(PrimitiveType.Cube, crate, new Vector3(0f, .07f, 0f), new Vector3(1.1f, .14f, 1.1f), Lit(new Color(.42f, .3f, .18f)));
            Part(PrimitiveType.Cube, crate, new Vector3(0f, .6f, 0f), new Vector3(.9f, .9f, .9f), Lit(new Color(.64f, .49f, .3f)));
            return crate;
        }

        private void ClearWaiting()
        {
            foreach (Transform item in waiting) if (item != null) Destroy(item.gameObject);
            waiting.Clear();
            waitingHome.Clear();
            WaitingCount = 0;
            waitingAttempt = null;
            boardingTime = -1f;
        }

        // ---- Helpers ----

        private LandingZone FindZone(string id)
        {
            if (Missions.Zones == null) return null;
            foreach (LandingZone zone in Missions.Zones) if (zone != null && zone.Definition.Id == id) return zone;
            return null;
        }

        /// <summary>The surface under a point (terrain, pad or roof), ignoring the aircraft; the pad height if none.</summary>
        private static Vector3 Ground(Vector3 point, float fallback)
        {
            int mask = ~((1 << WorldConstants.AircraftLayer) | (1 << 2));
            return Physics.Raycast(new Vector3(point.x, fallback + 40f, point.z), Vector3.down, out RaycastHit hit, 80f, mask, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(point.x, fallback, point.z);
        }

        private Renderer Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return renderer;
        }

        private Material Lit(Color color, bool emissive = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Pad cue", enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", .35f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }
            materials.Add(material);
            return material;
        }

        private Material SmokeMaterial()
        {
            const int size = 64;
            puffTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Marker smoke puff", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            puffTexture.SetPixels(pixels);
            puffTexture.Apply();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "Marker smoke" };
            material.SetTexture("_BaseMap", puffTexture);
            material.SetTexture("_MainTex", puffTexture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            if (puffTexture != null) Destroy(puffTexture);
        }
    }
}
