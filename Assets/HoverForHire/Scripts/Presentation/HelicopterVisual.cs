using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoverForHire
{
    /// <summary>
    /// Original imported utility-aircraft art plus the airframe collision boxes. Rotor presentation is decoupled
    /// from the true rotor speed so blades never strobe; handling uses explicit inertia, so colliders cannot change it.
    /// </summary>
    public sealed class HelicopterVisual : MonoBehaviour
    {
        public Transform CockpitMount { get; private set; }
        public const string AircraftResource = "Art/Helicopter/HFH_Utility_Helicopter";
        Transform rotor, tailRotor;
        static readonly string[] GaugeNames = { "airspeed", "altitude", "vertical speed", "rotor rpm", "heading" };
        readonly Transform[] gauges = new Transform[5];
        readonly Quaternion[] gaugeRest = new Quaternion[5];
        Renderer rotorBlur, tailBlur;
        Material rotorBlurMaterial, tailBlurMaterial;
        HelicopterController aircraft;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Texture2D blurTexture;
        const int MainBlades = 4, TailBlades = 3;
        // Largest apparent blade advance per rendered frame, as a fraction of blade spacing. Below one half the eye
        // reads forward motion; the true rotor speed would alias into frozen or reversed blades at common frame rates.
        const float MaximumApparentStep = 0.42f;
        const float BlurStartSpeed01 = 0.4f;

        public void Build(HelicopterController controller)
        {
            aircraft = controller;
            var asset = Resources.Load<GameObject>(AircraftResource);
            if (asset != null)
            {
                var model = Instantiate(asset, transform, false);
                model.name = "HFH-6 / utility air taxi";
                ConfigureMaterials(model);
                foreach (var part in model.GetComponentsInChildren<Transform>())
                {
                    if (part.name == "Main rotor (visual only)")
                        rotor = part;
                    else if (part.name == "Tail rotor (visual only)")
                        tailRotor = part;
                    for (int i = 0; i < GaugeNames.Length; i++)
                        if (part.name == "Gauge needle " + GaugeNames[i])
                        { gauges[i] = part; gaugeRest[i] = part.localRotation; }
                }
                blurTexture = MakeBlurTexture();
                ownedTextures.Add(blurTexture);
                float mainRadius = controller.Tuning != null ? controller.Tuning.MainRotorRadius : 4.62f;
                float tailRadius = controller.Tuning != null ? controller.Tuning.TailRotorRadius : .87f;
                if (rotor != null)
                    rotorBlur = CreateBlurDisc(rotor, "Main rotor motion blur", .65f, mainRadius, false, out rotorBlurMaterial);
                if (tailRotor != null)
                    tailBlur = CreateBlurDisc(tailRotor, "Tail rotor motion blur", .12f, tailRadius, true, out tailBlurMaterial);
            }
            else
                Debug.LogError("The HFH-6 aircraft art is missing from Resources/" + AircraftResource, this);

            CockpitMount = new GameObject("Pilot eye").transform;
            CockpitMount.SetParent(transform, false);
            CockpitMount.localPosition = new Vector3(.43f, .63f, .10f);
            CockpitMount.localRotation = Quaternion.Euler(5f, 0, 0);
            foreach (var child in GetComponentsInChildren<Transform>())
                child.gameObject.layer = 8;

            // Hull and skids carry ground contact; the nose and tail boxes make the rest of the airframe solid
            // against obstacles. Handling uses FlightTuning inertia, so these shapes do not change flight response.
            AddBox(new Vector3(0, .05f, 0), new Vector3(2, 1.7f, 3.1f));
            foreach (float side in new[] { -1f, 1f })
                AddBox(new Vector3(side, -1.38f, .15f), new Vector3(.2f, .24f, 3.9f));
            AddBox(new Vector3(0, -.05f, 1.87f), new Vector3(1.4f, 1.1f, .55f));      // Nose ahead of the hull box.
            AddBox(new Vector3(0, .2f, -2.4f), new Vector3(.55f, .75f, 1.9f));        // Forward tail boom.
            AddBox(new Vector3(0, .58f, -4.2f), new Vector3(.3f, .55f, 1.8f));        // Aft tail boom.
            AddBox(new Vector3(0, .57f, -4.33f), new Vector3(2.3f, .08f, .8f));       // Horizontal stabilizer.
            AddBox(new Vector3(0, 1.23f, -5.02f), new Vector3(.14f, 1.76f, .66f));    // Vertical fin.
        }

        /// <summary>Repaint the airframe enamel. Shared materials change in place, so every panel follows.</summary>
        public void ApplyLivery(Livery livery)
        {
            foreach (Material material in ownedMaterials)
            {
                if (material == null) continue;
                Color? color = material.name.Contains("OrangeEnamel") ? livery.Primary
                    : material.name.Contains("CreamEnamel") ? livery.Secondary
                    : material.name.Contains("CautionYellow") ? livery.Accent : (Color?)null;
                if (!color.HasValue) continue;
                material.SetColor("_BaseColor", color.Value);
                material.SetColor("_Color", color.Value);
            }
        }

        void AddBox(Vector3 center, Vector3 size)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        void ConfigureMaterials(GameObject model)
        {
            var materials = new Dictionary<string, Material>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] == null ? "HFH_Graphite" : slots[i].name;
                    if (!materials.TryGetValue(name, out Material material))
                    {
                        material = AircraftMaterial(name);
                        materials.Add(name, material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
                renderer.receiveShadows = true;
                if (renderer.name == "Glazed canopy")
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }

        Material AircraftMaterial(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name + " / flight" };
            Color color = new Color(.045f, .06f, .07f);
            float metal = .4f, smooth = .45f;
            if (name.Contains("OrangeEnamel"))
            { color = new Color(.98f, .25f, .035f); metal = .28f; smooth = .63f; }
            else if (name.Contains("CreamEnamel"))
            { color = new Color(.92f, .89f, .76f); metal = .18f; smooth = .55f; }
            else if (name.Contains("BrushedAlloy"))
            { color = new Color(.42f, .47f, .49f); metal = .84f; smooth = .61f; }
            else if (name.Contains("DarkAlloy"))
            { color = new Color(.11f, .13f, .14f); metal = .76f; smooth = .45f; }
            else if (name.Contains("Rubber"))
            { color = new Color(.018f, .025f, .027f); metal = 0; smooth = .12f; }
            else if (name.Contains("CabinFabric"))
            { color = new Color(.115f, .15f, .155f); metal = 0; smooth = .08f; }
            else if (name.Contains("CautionYellow"))
            { color = new Color(1f, .72f, .055f); metal = .12f; smooth = .5f; }
            else if (name.Contains("CanopyGlass"))
            {
                color = new Color(.12f, .27f, .31f, .17f);
                metal = .08f;
                smooth = .92f;
                Transparent(material);
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
            else if (name.Contains("LandingLight"))
            { color = new Color(.85f, .94f, 1); Emission(material, color * 2.2f); }
            else if (name.Contains("NavigationRed"))
            { color = new Color(1, .025f, .008f); Emission(material, color * 1.8f); }
            else if (name.Contains("NavigationGreen"))
            { color = new Color(.015f, .85f, .29f); Emission(material, color * 1.5f); }
            else if (name.Contains("DisplayCyan"))
            {
                color = new Color(.15f, .65f, .40f);
                Emission(material, color * .8f);
                metal = 0;
                smooth = .25f;
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smooth);
            material.enableInstancing = true;
            ownedMaterials.Add(material);
            return material;
        }

        static void Emission(Material material, Color color)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        static void Transparent(Material material)
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        readonly List<Texture2D> ownedTextures = new List<Texture2D>();

        /// <summary>A translucent streaked disc that reads as spinning blades. Tail discs lie in the rotor's YZ plane.</summary>
        Renderer CreateBlurDisc(Transform parent, string name, float inner, float outer, bool tail, out Material material)
        {
            const int segments = 72;
            var vertices = new Vector3[segments * 2];
            var uv = new Vector2[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                Vector2 radial = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                vertices[i * 2] = Disc(radial * inner, tail);
                vertices[i * 2 + 1] = Disc(radial * outer, tail);
                uv[i * 2] = radial * (inner / outer) * .5f + new Vector2(.5f, .5f);
                uv[i * 2 + 1] = radial * .5f + new Vector2(.5f, .5f);
                int next = (i + 1) % segments, t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = name + " disc", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            var blur = new GameObject(name);
            blur.transform.SetParent(parent, false);
            blur.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = blur.AddComponent<MeshRenderer>();
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) { name = name + " / translucent" };
            material.SetTexture("_BaseMap", blurTexture);
            material.SetTexture("_MainTex", blurTexture);
            Transparent(material);
            material.SetFloat("_Cull", (float)CullMode.Off);
            ownedMaterials.Add(material);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        static Vector3 Disc(Vector2 radial, bool tail) => tail ? new Vector3(0, radial.x, radial.y) : new Vector3(radial.x, 0, radial.y);

        /// <summary>Radial streaks in polar space: denser toward the tips, with the high-visibility tip ring.</summary>
        static Texture2D MakeBlurTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Rotor motion streaks", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), angle = Mathf.Atan2(dy, dx);
                    float streak = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * MainBlades * .5f)), 6f);
                    float body = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .35f, r)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.97f, 1f, r)));
                    float alpha = body * (.45f + .55f * streak) * (.55f + .45f * r);
                    float tip = Mathf.Exp(-Mathf.Pow((r - .95f) / .025f, 2));
                    Color shade = Color.Lerp(new Color(.10f, .12f, .13f), new Color(.92f, .89f, .76f), tip * .8f);
                    pixels[y * size + x] = new Color(shade.r, shade.g, shade.b, Mathf.Clamp01(alpha + tip * .35f));
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        void Update()
        {
            if (aircraft == null)
                return;
            float rpm = aircraft.RotorRpm, speed01 = aircraft.RotorSpeed01;
            SetGauge(0, Mathf.Lerp(-130, 130, Mathf.Clamp01(aircraft.Airspeed * 1.9438445f / 140)));
            SetGauge(1, Mathf.Lerp(-130, 130, Mathf.Clamp01(aircraft.AltitudeAGL / 1000)));
            SetGauge(2, Mathf.Lerp(-130, 130, Mathf.InverseLerp(-10, 10, aircraft.VerticalSpeed)));
            SetGauge(3, Mathf.Lerp(-130, 130, aircraft.RotorSpeed01));
            SetGauge(4, aircraft.Heading);
            if (rotor != null)
                rotor.Rotate(Vector3.up, ApparentStep(rpm * 6 * Time.deltaTime, MainBlades, speed01), Space.Self);
            if (tailRotor != null)
                tailRotor.Rotate(Vector3.right, ApparentStep(rpm * 18 * Time.deltaTime, TailBlades, speed01), Space.Self);
            float blur = aircraft.Crashed ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BlurStartSpeed01, 1, speed01));
            SetBlur(rotorBlur, rotorBlurMaterial, blur * .38f);
            SetBlur(tailBlur, tailBlurMaterial, blur * .38f);
        }

        /// <summary>Blade advance for this frame, capped below the strobing threshold once the rotor is at speed.</summary>
        public static float ApparentStep(float trueDegrees, int blades, float speed01)
        {
            if (speed01 < BlurStartSpeed01)
                return trueDegrees;
            return Mathf.Min(trueDegrees, 360f / blades * MaximumApparentStep);
        }

        static void SetBlur(Renderer renderer, Material material, float alpha)
        {
            if (renderer == null)
                return;
            renderer.enabled = alpha > .01f;
            if (renderer.enabled)
                material.SetColor("_BaseColor", new Color(1, 1, 1, alpha));
        }

        void SetGauge(int index, float degrees)
        {
            if (gauges[index] != null)
                gauges[index].localRotation = gaugeRest[index] * Quaternion.AngleAxis(-degrees, Vector3.forward);
        }

        void OnDestroy()
        {
            foreach (var material in ownedMaterials)
                if (material != null)
                    Destroy(material);
            foreach (var mesh in ownedMeshes)
                if (mesh != null)
                    Destroy(mesh);
            foreach (var texture in ownedTextures)
                if (texture != null)
                    Destroy(texture);
        }
    }
}
