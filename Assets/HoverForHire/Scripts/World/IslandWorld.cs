using System.Collections.Generic;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Deterministic, dependency-free blockout island. Coordinates and dimensions are metres.</summary>
    public static class IslandWorld
    {
        public static readonly Color Ink = new Color(.055f, .12f, .16f);
        public static readonly Color Orange = new Color(1f, .36f, .12f);
        public static Material Ground, Stone, White, Asphalt, Metal, Glass, Signal, Water, Wood, Roof;
        static Transform root;
        static readonly Vector3[] sites = {
            new Vector3(-420, 9, -510), new Vector3(-190, 9, -200), new Vector3(-770, 7, -170),
            new Vector3(450, 9, -350), new Vector3(-140, 43, 40), new Vector3(200, 20, 270),
            new Vector3(480, 61, 510), new Vector3(-350, 103, 450), new Vector3(-720, 17, 430), new Vector3(770, 10, 80)
        };
        static readonly string[] names = { "01 / HOME BASE", "02 / TOWN GREEN", "03 / FERRY DOCK", "04 / FREIGHT YARD", "05 / ROOFTOP CLINIC", "06 / ORCHARD", "07 / RIDGE STATION", "08 / SUMMIT LODGE", "09 / LIGHTHOUSE", "10 / EAST COVE" };
        public static LandingZone[] Build()
        {
            root = new GameObject("PORT MERIDIAN / island").transform;
            Ground = Mat("Meadow", new Color(.34f, .48f, .33f)); Stone = Mat("Sandstone", new Color(.71f, .69f, .53f));
            White = Mat("Ivory", new Color(.95f,.91f,.79f)); Asphalt = Mat("Slate", new Color(.13f,.21f,.23f));
            Metal = Mat("Graphite", Ink); Glass = Mat("Smoked canopy", new Color(.08f,.25f,.30f), .7f);
            Signal = Mat("Rescue orange", Orange); Water = Mat("Ocean",new Color(.13f,.39f,.46f), .65f);
            Wood = Mat("Cedar",new Color(.48f,.29f,.18f)); Roof = Mat("Clay roofs", new Color(.69f,.35f,.25f));
            Piece("Ocean", PrimitiveType.Cube, new Vector3(0,-6,0), new Vector3(10000,5,10000), Water, false);
            TerrainMesh();
            var zones = new LandingZone[sites.Length];
            for (int i=0;i<sites.Length;i++)
            {
                var p = sites[i]; float radius = i == 0 ? 17 : i == 4 ? 9 : i > 5 ? 10 : 14;
                if(i==4) Piece("Clinic tower",PrimitiveType.Cube,new Vector3(p.x,21.85f,p.z),new Vector3(34,41.7f,34),White);
                else Piece("Landing foundation",PrimitiveType.Cube,p-Vector3.up*.85f,new Vector3(radius*2+6,1.4f,radius*2+6),Stone);
                var pad = Piece(names[i],PrimitiveType.Cylinder,p-Vector3.up*.12f,new Vector3(radius*2,.12f,radius*2),Asphalt);
                var zoneObject = new GameObject(names[i]+" zone"); zoneObject.transform.SetParent(root); zoneObject.transform.position=p;
                var zone = zoneObject.AddComponent<LandingZone>(); zone.Id="pad-"+i; zone.DisplayName=names[i]; zone.Radius=radius; zones[i]=zone;
                // H marking and a broken perimeter ring remain legible from the approach.
                foreach(float dx in new[]{-2.2f,2.2f}) Piece("H",PrimitiveType.Cube,p+new Vector3(dx,.04f,0),new Vector3(.85f,.06f,7),White,false);
                Piece("H crossbar",PrimitiveType.Cube,p+Vector3.up*.04f,new Vector3(5,.06f,.85f),White,false);
                for(int n=0;n<24;n++) { float a=n*15*Mathf.Deg2Rad; var marker=Piece("Perimeter",PrimitiveType.Cube,p+new Vector3(Mathf.Sin(a)*(radius-1),.05f,Mathf.Cos(a)*(radius-1)),new Vector3(1.6f,.07f,.4f),Signal,false);marker.transform.rotation=Quaternion.Euler(0,n*15,0); }
                var pole=p+new Vector3(radius+3,3,0); Piece("Windsock pole",PrimitiveType.Cylinder,pole,new Vector3(.15f,3,.15f),White);
                var sock=Piece("Windsock",PrimitiveType.Capsule,pole+new Vector3(1.1f,2.8f,0),new Vector3(.55f,1.2f,.55f),Signal,false);sock.transform.rotation=Quaternion.Euler(0,0,80);
            }
            Road(new Vector3(-650,8,-350),new Vector3(660,8,-350),13);
            Road(new Vector3(-190,8,-530),new Vector3(-190,8,170),11);
            Road(new Vector3(-650,8,-350),new Vector3(-650,8,240),10);
            var random = new System.Random(42);
            for(int x=-3;x<=3;x++) for(int z=-2;z<=3;z++)
            {
                var p=new Vector3(-190+x*68,0,-130+z*62); if(NearPad(p,48) || Mathf.Abs(p.x+190)<20 || Mathf.Abs(p.z+350)<20) continue;
                float h=8+(float)random.NextDouble()*16; p.y=Height(p.x,p.z)+h/2;
                Piece("Town / building",PrimitiveType.Cube,p,new Vector3(29,h,25),x%2==0?White:Stone);
                Piece("Town / roof",PrimitiveType.Cube,p+Vector3.up*(h/2+.5f),new Vector3(31,1.2f,27),Roof);
                for(int floor=0;floor<h/4;floor++) Piece("Window band",PrimitiveType.Cube,p+new Vector3(0,-h/2+3+floor*4,12.6f),new Vector3(23,1.4f,.12f),Glass,false);
            }
            // Harbor piers, freight containers, and a crane establish distinct districts.
            for(int i=0;i<3;i++) Piece("Harbor pier",PrimitiveType.Cube,new Vector3(-850+i*40,4,-210),new Vector3(25,4,140),Wood);
            for(int i=0;i<18;i++) Piece("Shipping container",PrimitiveType.Cube,new Vector3(370+(i%6)*22,12,-460+(i/6)*19),new Vector3(17,7,11),i%3==0?Signal:Glass);
            Piece("Crane mast",PrimitiveType.Cube,new Vector3(-720,27,-270),new Vector3(3,40,3),Signal);
            Piece("Crane boom",PrimitiveType.Cube,new Vector3(-740,46,-270),new Vector3(45,3,3),Signal);
            Piece("Hangar",PrimitiveType.Cube,new Vector3(-490,19,-565),new Vector3(55,20,37),Asphalt);
            Piece("Hangar doors",PrimitiveType.Cube,new Vector3(-490,17,-546),new Vector3(45,15,.4f),Glass);
            Piece("Lighthouse",PrimitiveType.Cylinder,new Vector3(-752,32,450),new Vector3(8,17,8),White);
            Piece("Lantern",PrimitiveType.Cylinder,new Vector3(-752,50,450),new Vector3(10,2,10),Signal);
            for(int i=0;i<200;i++)
            {
                float x=(float)random.NextDouble()*1800-900,z=(float)random.NextDouble()*1700-700;
                float y=Height(x,z); if(y<6 || NearPad(new Vector3(x,0,z),55) || (z<220&&x>-480&&x<630))continue;
                float h=5+(float)random.NextDouble()*7; Piece("Tree trunk",PrimitiveType.Cylinder,new Vector3(x,y+h*.28f,z),new Vector3(.8f,h*.28f,.8f),Wood);
                Piece("Tree crown",PrimitiveType.Sphere,new Vector3(x,y+h*.72f,z),new Vector3(h*.75f,h,h*.75f),Ground,false);
            }
            return zones;
        }
        static bool NearPad(Vector3 p,float r) { foreach(var s in sites) if(Vector2.Distance(new Vector2(s.x,s.z),new Vector2(p.x,p.z))<r)return true;return false; }
        static float BaseHeight(float x,float z)
        {
            float edge=Mathf.Sqrt(x*x/(1080*1080f)+z*z/(950*950f));
            float hills=130*Mathf.Exp(-((x+300)*(x+300)+(z-490)*(z-490))/80000f)+72*Mathf.Exp(-((x-490)*(x-490)+(z-500)*(z-500))/100000f);
            return Mathf.Lerp(8+hills,-18,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.1f,edge)));
        }
        public static float Height(float x,float z)
        {
            float y=BaseHeight(x,z);
            for(int i=0;i<sites.Length;i++) { if(i==4)continue; var s=sites[i];float d=Vector2.Distance(new Vector2(x,z),new Vector2(s.x,s.z));y=Mathf.Lerp(s.y-.35f,y,Mathf.SmoothStep(0,1,Mathf.InverseLerp(33,75,d))); }
            return y;
        }
        static void TerrainMesh()
        {
            const int n=130; var verts=new Vector3[(n+1)*(n+1)];var colors=new Color[verts.Length];var tris=new int[n*n*6];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int k=z*(n+1)+x;float px=(x/(float)n-.5f)*2400,pz=(z/(float)n-.5f)*2200,y=Height(px,pz);verts[k]=new Vector3(px,y,pz);colors[k]=Color.white;}
            int t=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++){int k=z*(n+1)+x;tris[t++]=k;tris[t++]=k+n+1;tris[t++]=k+1;tris[t++]=k+1;tris[t++]=k+n+1;tris[t++]=k+n+2;}
            var mesh=new Mesh{name="Port Meridian terrain"};mesh.vertices=verts;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Island terrain",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(root);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Ground;
        }
        static void Road(Vector3 a,Vector3 b,float width) { var road=Piece("Road",PrimitiveType.Cube,(a+b)/2,new Vector3(width,.15f,Vector3.Distance(a,b)),Asphalt);road.transform.rotation=Quaternion.LookRotation(b-a); }
        public static Material Mat(string name,Color color,float smoothness=.15f)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");var m=new Material(shader){name=name,color=color};m.SetFloat("_Smoothness",smoothness);return m;
        }
        public static GameObject Piece(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,bool collision=true,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent!=null?parent:root,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision){var c=go.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);}
            else if(type==PrimitiveType.Cylinder)
            {
                // Unity gives primitive cylinders capsule colliders. A wide, thin helipad would
                // otherwise create a tall rounded obstacle; collide against the actual static mesh.
                var capsule=go.GetComponent<Collider>();capsule.enabled=false;Object.Destroy(capsule);
                go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }
    }
}
