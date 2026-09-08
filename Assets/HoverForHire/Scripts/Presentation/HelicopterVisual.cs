using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoverForHire
{
    /// <summary>Original imported utility-aircraft art. Flight contacts remain the three explicit boxes.</summary>
    public sealed class HelicopterVisual : MonoBehaviour
    {
        public Transform CockpitMount { get; private set; }
        public const string AircraftResource = "Art/Helicopter/HFH_Utility_Helicopter";
        Transform rotor, tailRotor;
        static readonly string[] GaugeNames = { "airspeed", "altitude", "vertical speed", "rotor rpm", "heading" };
        readonly Transform[] gauges = new Transform[5];
        readonly Quaternion[] gaugeRest = new Quaternion[5];
        Renderer rotorBlur;
        HelicopterController aircraft;
        readonly List<Material> ownedMaterials = new List<Material>();
        Mesh blurMesh;

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
                    if (part.name == "Main rotor (visual only)") rotor = part;
                    else if (part.name == "Tail rotor (visual only)") tailRotor = part;
                    for (int i = 0; i < GaugeNames.Length; i++)
                        if (part.name == "Gauge needle " + GaugeNames[i]) { gauges[i] = part; gaugeRest[i] = part.localRotation; }
                }
                CreateRotorBlur();
            }
            else Debug.LogError("The HFH-6 aircraft art is missing from Resources/" + AircraftResource, this);

            CockpitMount = new GameObject("Pilot eye").transform;
            CockpitMount.SetParent(transform, false);
            CockpitMount.localPosition = new Vector3(.43f, .63f, .10f);
            CockpitMount.localRotation = Quaternion.Euler(5f, 0, 0);
            foreach (var child in GetComponentsInChildren<Transform>()) child.gameObject.layer = 8;

            // Unchanged flight contact dimensions: decorative art never affects the flight model.
            var hull = gameObject.AddComponent<BoxCollider>();
            hull.center = new Vector3(0, .05f, 0);
            hull.size = new Vector3(2, 1.7f, 3.1f);
            foreach (float side in new[] { -1f, 1f })
            {
                var contact = gameObject.AddComponent<BoxCollider>();
                contact.center = new Vector3(side, -1.38f, .15f);
                contact.size = new Vector3(.2f, .24f, 3.9f);
            }
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
            if (name.Contains("OrangeEnamel")) { color = new Color(.98f, .25f, .035f); metal = .28f; smooth = .63f; }
            else if (name.Contains("CreamEnamel")) { color = new Color(.92f, .89f, .76f); metal = .18f; smooth = .55f; }
            else if (name.Contains("BrushedAlloy")) { color = new Color(.42f, .47f, .49f); metal = .84f; smooth = .61f; }
            else if (name.Contains("DarkAlloy")) { color = new Color(.11f, .13f, .14f); metal = .76f; smooth = .45f; }
            else if (name.Contains("Rubber")) { color = new Color(.018f, .025f, .027f); metal = 0; smooth = .12f; }
            else if (name.Contains("CabinFabric")) { color = new Color(.115f, .15f, .155f); metal = 0; smooth = .08f; }
            else if (name.Contains("CautionYellow")) { color = new Color(1f, .72f, .055f); metal = .12f; smooth = .5f; }
            else if (name.Contains("CanopyGlass"))
            {
                color = new Color(.12f, .27f, .31f, .17f); metal = .08f; smooth = .92f;
                Transparent(material);
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
            else if (name.Contains("LandingLight")) { color = new Color(.85f, .94f, 1); Emission(material, color * 2.2f); }
            else if (name.Contains("NavigationRed")) { color = new Color(1, .025f, .008f); Emission(material, color * 1.8f); }
            else if (name.Contains("NavigationGreen")) { color = new Color(.015f, .85f, .29f); Emission(material, color * 1.5f); }
            else if (name.Contains("DisplayCyan")) { color = new Color(.15f, .65f, .40f); Emission(material, color * .8f); metal = 0; smooth = .25f; }
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

        void CreateRotorBlur()
        {
            if (rotor == null) return;
            const int segments = 80;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var direction = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                vertices[i * 2] = direction * .65f;
                vertices[i * 2 + 1] = direction * 4.59f;
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            blurMesh = new Mesh { name = "Main rotor motion disc", vertices = vertices, triangles = triangles };
            blurMesh.RecalculateNormals();
            blurMesh.RecalculateBounds();
            var blur = new GameObject("Main rotor motion blur");
            blur.transform.SetParent(rotor, false);
            blur.AddComponent<MeshFilter>().sharedMesh = blurMesh;
            rotorBlur = blur.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) { name = "Rotor motion / translucent" };
            material.SetColor("_BaseColor", new Color(.12f, .16f, .17f, .045f));
            material.SetColor("_Color", new Color(.12f, .16f, .17f, .045f));
            Transparent(material);
            material.SetFloat("_Cull", (float)CullMode.Off);
            ownedMaterials.Add(material);
            rotorBlur.sharedMaterial = material;
            rotorBlur.shadowCastingMode = ShadowCastingMode.Off;
            rotorBlur.receiveShadows = false;
        }

        void Update()
        {
            if (aircraft == null) return;
            float rpm = aircraft.RotorRpm;
            SetGauge(0, Mathf.Lerp(-130, 130, Mathf.Clamp01(aircraft.Airspeed * 1.9438445f / 140)));
            SetGauge(1, Mathf.Lerp(-130, 130, Mathf.Clamp01(aircraft.AltitudeAGL / 1000)));
            SetGauge(2, Mathf.Lerp(-130, 130, Mathf.InverseLerp(-10, 10, aircraft.VerticalSpeed)));
            SetGauge(3, Mathf.Lerp(-130, 130, aircraft.RotorSpeed01));
            SetGauge(4, aircraft.Heading);
            if (rotor != null && rotor.IsChildOf(transform)) rotor.Rotate(Vector3.up, rpm * 6 * Time.deltaTime, Space.Self);
            if (tailRotor != null && tailRotor.IsChildOf(transform)) tailRotor.Rotate(Vector3.right, rpm * 18 * Time.deltaTime, Space.Self);
            if (rotorBlur != null) rotorBlur.enabled = rpm > 210 && !aircraft.Crashed;
        }

        void SetGauge(int index, float degrees)
        {
            if (gauges[index] != null) gauges[index].localRotation = gaugeRest[index] * Quaternion.AngleAxis(-degrees, Vector3.forward);
        }

        void OnDestroy()
        {
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            if (blurMesh != null) Destroy(blurMesh);
        }
    }
}
