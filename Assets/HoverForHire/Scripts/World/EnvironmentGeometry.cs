using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoverForHire
{
    // Static scenery is merged by material and district, keeping hundreds of small details
    // out of the hierarchy and allowing normal frustum / shadow culling per district.
    internal sealed class EnvironmentGeometry
    {
        sealed class Surface
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        readonly Dictionary<Material, Surface> surfaces = new Dictionary<Material, Surface>();
        readonly string name;
        readonly Transform parent;
        static Mesh cube, cylinder, cone, roof, rock, foliage, evergreen;
        public EnvironmentGeometry(string name, Transform parent) { this.name = name; this.parent = parent; }

        public void Box(Vector3 position, Vector3 size, Material material, float yaw = 0)
        { Add(Cube, position, size, Quaternion.Euler(0, yaw, 0), material); }
        public void Cylinder(Vector3 position, float radius, float height, Material material, int unused = 0)
        { Add(CylinderMesh, position, new Vector3(radius, height, radius), Quaternion.identity, material); }
        public void Beam(Vector3 a, Vector3 b, float width, Material material, float depth = -1)
        { Add(Cube, (a + b) * .5f, new Vector3(width, depth < 0 ? width : depth, Vector3.Distance(a, b)), Quaternion.LookRotation(b - a), material); }
        public void Gable(Vector3 position, Vector3 size, Material material, float yaw = 0)
        { Add(Roof, position, size, Quaternion.Euler(0, yaw, 0), material); }
        public void Cone(Vector3 position, Vector3 size, Material material, float yaw = 0)
        { Add(ConeMesh, position, size, Quaternion.Euler(0, yaw, 0), material); }
        public void Rock(Vector3 position, Vector3 size, Material material, float yaw)
        { Add(RockMesh, position, size, Quaternion.Euler(7, yaw, 13), material); }
        public void Foliage(Vector3 position, Vector3 size, Material material, float yaw)
        { Add(FoliageMesh, position, size, Quaternion.Euler(7, yaw, 9), material); }
        public void Evergreen(Vector3 position, Vector3 size, Material material, float yaw)
        { Add(EvergreenMesh, position, size, Quaternion.Euler(0, yaw, 0), material); }

        public void Add(Mesh mesh, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            if (!surfaces.TryGetValue(material, out var data)) surfaces.Add(material, data = new Surface());
            Matrix4x4 transform = Matrix4x4.TRS(position, rotation, size);
            Matrix4x4 normalTransform = transform.inverse.transpose;
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv; var triangles = mesh.triangles;
            int start = data.Vertices.Count;
            for (int i = 0; i < vertices.Length; i++)
            {
                data.Vertices.Add(transform.MultiplyPoint3x4(vertices[i]));
                data.Normals.Add(normalTransform.MultiplyVector(normals[i]).normalized);
                data.UV.Add(uv.Length > i ? uv[i] : Vector2.zero);
            }
            foreach (int index in triangles) data.Triangles.Add(start + index);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material material, float uvWidth = 1, float uvLength = 1)
        {
            if (!surfaces.TryGetValue(material, out var data)) surfaces.Add(material, data = new Surface());
            int start = data.Vertices.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            data.Vertices.AddRange(new[] { a, b, c, d });
            for (int i = 0; i < 4; i++) data.Normals.Add(normal);
            data.UV.AddRange(new[] { new Vector2(0, 0), new Vector2(0, uvLength), new Vector2(uvWidth, uvLength), new Vector2(uvWidth, 0) });
            data.Triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        public void Finish(bool shadows = true)
        {
            var group = new GameObject(name).transform; group.SetParent(parent, false);
            var lifetime = group.gameObject.AddComponent<EnvironmentMeshLifetime>();
            foreach (var entry in surfaces)
            {
                var data = entry.Value;
                if (data.Vertices.Count == 0) continue;
                var mesh = new Mesh { name = name + " / " + entry.Key.name, indexFormat = data.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(data.Vertices); mesh.SetNormals(data.Normals); mesh.SetUVs(0, data.UV); mesh.SetTriangles(data.Triangles, 0); mesh.RecalculateBounds();
                var go = new GameObject(entry.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(group, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = entry.Key;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                lifetime.Meshes.Add(mesh);
            }
        }

        static Mesh Cube
        {
            get
            {
                if (cube != null) return cube;
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
                Face(v, uv, triangles, new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f));
                Face(v, uv, triangles, new Vector3(.5f,-.5f,-.5f),new Vector3(-.5f,-.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(.5f,.5f,-.5f));
                Face(v, uv, triangles, new Vector3(-.5f,-.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,.5f,.5f),new Vector3(-.5f,.5f,-.5f));
                Face(v, uv, triangles, new Vector3(.5f,-.5f,.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(.5f,.5f,.5f));
                Face(v, uv, triangles, new Vector3(-.5f,.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f));
                Face(v, uv, triangles, new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f),new Vector3(-.5f,-.5f,.5f));
                return cube = Make("Environment unit box", v, uv, triangles);
            }
        }
        static Mesh CylinderMesh { get { if(cylinder == null) cylinder = Radial(false); return cylinder; } }
        static Mesh ConeMesh { get { if(cone == null) cone = Radial(true); return cone; } }
        static Mesh Radial(bool pointed)
        {
            const int segments = 12; var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for(int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 x = new Vector3(Mathf.Sin(a), -.5f, Mathf.Cos(a)), y = new Vector3(Mathf.Sin(b), -.5f, Mathf.Cos(b));
                Vector3 topX = pointed ? new Vector3(.04f,.5f,0) : x + Vector3.up, topY = pointed ? topX : y + Vector3.up;
                Face(v, uv, t, x, y, topY, topX);
                Face(v, uv, t, Vector3.down * .5f, y, x, Vector3.down * .5f);
                if(!pointed) Face(v, uv, t, Vector3.up * .5f, topX, topY, Vector3.up * .5f);
            }
            return Make(pointed ? "Layered canopy cone" : "Environment unit cylinder", v, uv, t);
        }
        static Mesh Roof
        {
            get
            {
                if(roof != null) return roof;
                var v = new List<Vector3>();var uv = new List<Vector2>();var t = new List<int>();
                Vector3 a = new Vector3(-.5f,0,-.5f),b = new Vector3(.5f,0,-.5f),c = new Vector3(.5f,0,.5f),d = new Vector3(-.5f,0,.5f),e = new Vector3(0,1,-.5f),f = new Vector3(0,1,.5f);
                Face(v,uv,t,a,d,f,e);Face(v,uv,t,b,e,f,c);Face(v,uv,t,a,e,b,b);Face(v,uv,t,c,f,d,d);
                return roof = Make("Standing seam gabled roof",v,uv,t);
            }
        }
        static Mesh RockMesh
        {
            get
            {
                if(rock != null) return rock;
                var v = new List<Vector3>();var uv = new List<Vector2>();var t = new List<int>();
                const int n = 9;
                for(int i=0;i<n;i++)
                {
                    float a = i*Mathf.PI*2/n, b=(i+1)*Mathf.PI*2/n;
                    float ra=.86f+.12f*Mathf.Sin(i*3.17f),rb=.86f+.12f*Mathf.Sin((i+1)*3.17f);
                    Vector3 p=new Vector3(Mathf.Cos(a)*ra,-.28f,Mathf.Sin(a)*ra),q=new Vector3(Mathf.Cos(b)*rb,-.28f,Mathf.Sin(b)*rb);
                    Vector3 u=new Vector3(p.x*.7f,.25f+Mathf.Sin(i*2.1f)*.1f,p.z*.7f),w=new Vector3(q.x*.7f,.25f+Mathf.Sin((i+1)*2.1f)*.1f,q.z*.7f);
                    Face(v,uv,t,p,u,w,q);Face(v,uv,t,w,u,new Vector3(.07f,.62f,-.04f),new Vector3(.07f,.62f,-.04f));
                }
                return rock=Make("Weathered coastal granite",v,uv,t);
            }
        }
        static Mesh FoliageMesh
        {
            get
            {
                if(foliage!=null)return foliage;
                const int sides=16,rings=9;
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
                {
                    float v=ring/(float)rings,u=side/(float)sides,a=u*Mathf.PI*2,phi=v*Mathf.PI;
                    float radius=Mathf.Sin(phi)*(1+.095f*Mathf.Sin(a*3+v*4)+.065f*Mathf.Cos(a*5-v*8));
                    Vector3 p=new Vector3(Mathf.Cos(a)*radius,Mathf.Cos(phi)*.56f+Mathf.Sin(phi)*.055f*Mathf.Sin(a*4),Mathf.Sin(a)*radius);
                    vertices.Add(p);normals.Add(new Vector3(p.x,p.y*2.4f,p.z).normalized);uv.Add(new Vector2(u,v));
                    if(ring<rings&&side<sides)
                    {
                        int k=ring*(sides+1)+side;triangles.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});
                    }
                }
                foliage=new Mesh{name="Soft lobed broadleaf crown"};foliage.SetVertices(vertices);foliage.SetNormals(normals);foliage.SetUVs(0,uv);foliage.SetTriangles(triangles,0);foliage.RecalculateBounds();return foliage;
            }
        }
        static Mesh EvergreenMesh
        {
            get
            {
                if(evergreen!=null)return evergreen;
                const int sides=18,rings=16;
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
                {
                    float t=ring/(float)rings,a=side/(float)sides*Mathf.PI*2;
                    float profile=Mathf.Pow(1-t,.8f)*(.40f+.60f*Mathf.Sin(Mathf.Min(t*2.1f,1)*Mathf.PI*.5f));
                    float whorl=1+.12f*Mathf.Sin(t*43+.8f)+.10f*Mathf.Sin(a*5+t*16)+.075f*Mathf.Cos(a*7-t*9);
                    float radius=profile*whorl;vertices.Add(new Vector3(Mathf.Cos(a)*radius,(t-.5f),Mathf.Sin(a)*radius));uv.Add(new Vector2(side/(float)sides,t*2));
                    if(ring<rings&&side<sides)
                    {
                        int k=ring*(sides+1)+side;triangles.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});
                    }
                }
                // A shaded lower cap closes the crown when looking upward from the apron.
                int bottom=vertices.Count;vertices.Add(new Vector3(0,-.5f,0));uv.Add(new Vector2(.5f,0));
                for(int s=0;s<sides;s++)triangles.AddRange(new[]{bottom,s,s+1});
                evergreen=Make("Organic coastal evergreen crown",vertices,uv,triangles);return evergreen;
            }
        }
        static void Face(List<Vector3> v,List<Vector2> uv,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int k=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
            triangles.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
        }
        static Mesh Make(string name,List<Vector3> vertices,List<Vector2> uv,List<int> triangles)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }

    internal sealed class EnvironmentMeshLifetime : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();
        void OnDestroy() { foreach(var mesh in Meshes) if(mesh != null) Destroy(mesh); }
    }

    // Dynamic font atlases can be rebuilt when GUI fonts request another point size.
    // Keep the depth-tested world lettering material synchronized with that texture.
    internal sealed class EnvironmentSignAtlas : MonoBehaviour
    {
        public Font Font;
        public Material Material;
        void OnEnable(){UnityEngine.Font.textureRebuilt+=Refresh;}
        void OnDisable(){UnityEngine.Font.textureRebuilt-=Refresh;}
        void Refresh(Font changed){if(changed==Font&&Material!=null)Material.SetTexture("_BaseMap",Font.material.mainTexture);}
    }
}
