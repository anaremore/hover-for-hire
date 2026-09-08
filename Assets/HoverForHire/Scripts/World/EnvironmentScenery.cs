using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoverForHire
{
    /// <summary>Authored coastal districts built from reusable, material-batched geometry.</summary>
    internal static class EnvironmentScenery
    {
        static Transform root;
        static Vector3[] pads;
        static Material concrete, joint, paint, yellow, glazing, trim, brick, teal, blue, coral, ochre, plaster, leaf, leafLight, pine, bark, rock, steel, tire;
        static Font signFont;
        static Material signMaterial;
        static readonly List<Vector2[]> roads=new List<Vector2[]>();
        static Material Wood=>IslandWorld.Wood;
        static Material Roof=>IslandWorld.Roof;
        static Material Metal=>IslandWorld.Metal;
        static Material Signal=>IslandWorld.Signal;
        static float Height(float x,float z)=>IslandWorld.Height(x,z);

        public static void Build(Transform parent,Vector3[] sites)
        {
            root=parent;pads=sites;roads.Clear();Palette();RoadNetwork();Airport();Town();Harbor();Freight();Landmarks();Vegetation();
        }
        static Material Mat(string name,float r,float g,float b,float smoothness=.15f)=>IslandWorld.Mat(name,new Color(r,g,b),smoothness);
        static void Palette()
        {
            concrete=Mat("Weathered apron concrete",.56f,.58f,.55f);joint=Mat("Concrete joints",.39f,.42f,.41f);
            paint=Mat("Sunbleached paint",.94f,.92f,.77f);yellow=Mat("Taxiway yellow",1,.68f,.075f);
            glazing=Mat("Coastal glazing",.08f,.20f,.245f,.87f);trim=Mat("Cream painted trim",.92f,.91f,.83f);
            brick=Mat("Harbor brick",.49f,.25f,.19f);teal=Mat("Marina teal",.12f,.43f,.43f);blue=Mat("Faded freight blue",.17f,.31f,.43f);
            coral=Mat("Town coral",.73f,.38f,.28f);ochre=Mat("Town ochre",.78f,.61f,.35f);plaster=Mat("Limestone plaster",.76f,.74f,.62f);
            leaf=Mat("Shaded foliage",.17f,.30f,.105f);leafLight=Mat("Sunlit foliage",.35f,.44f,.17f);pine=Mat("Coastal pine",.105f,.24f,.16f);
            bark=Mat("Tree bark",.28f,.22f,.15f);rock=Mat("Coastal granite",.46f,.465f,.415f);tire=Mat("Rubber and dark vents",.055f,.066f,.067f);
            steel=Mat("Galvanized steel",.42f,.47f,.48f,.48f);steel.SetFloat("_Metallic",.55f);
            var foliageTexture=new Texture2D(128,128,TextureFormat.RGB24,true){name="Canopy leaf variation",wrapMode=TextureWrapMode.Repeat,anisoLevel=2};
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float shade=.66f+Mathf.PerlinNoise(x*.115f,y*.115f)*.40f+Mathf.PerlinNoise(x*.43f+9,y*.43f+3)*.20f;
                pixels[y*128+x]=new Color(shade,shade,shade*.94f);
            }
            foliageTexture.SetPixels(pixels);foliageTexture.Apply(true,false);
            foreach(var material in new[]{leaf,leafLight,pine})material.SetTexture("_BaseMap",foliageTexture);
            signFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            signFont.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /+-",64);
            var lettering=Shader.Find("Hover for Hire/World Lettering");
            signMaterial=lettering!=null?new Material(lettering):new Material(signFont.material);
            signMaterial.name="Depth-tested world sign lettering";
            signMaterial.SetTexture("_BaseMap",signFont.material.mainTexture);
            var atlas=root.gameObject.AddComponent<EnvironmentSignAtlas>();atlas.Font=signFont;atlas.Material=signMaterial;
        }
        static void RoadNetwork()
        {
            Route("Coast highway",12,new Vector2(-850,-320),new Vector2(-690,-350),new Vector2(-490,-350),new Vector2(-190,-350),new Vector2(110,-350),new Vector2(365,-350),new Vector2(401,-395),new Vector2(553,-395));
            Route("Airport approach",10,new Vector2(-355,-502),new Vector2(-340,-434),new Vector2(-288,-391),new Vector2(-250,-350));
            Route("Ferry approach",10,new Vector2(-690,-350),new Vector2(-710,-275),new Vector2(-735,-222),new Vector2(-785,-219),new Vector2(-846,-219));
            Route("Western coast drive",9,new Vector2(-690,-350),new Vector2(-655,-140),new Vector2(-643,95),new Vector2(-660,260),new Vector2(-699,370),new Vector2(-701,400));
            Route("Central avenue",12,new Vector2(-190,-350),new Vector2(-190,-248),new Vector2(-149,-242),new Vector2(-144,-151),new Vector2(-190,-151),new Vector2(-190,-64),new Vector2(-190,113));
            foreach(float x in new[]{-394f,-326,-258,-54,14})Route("Town street",8,new Vector2(x,-316),new Vector2(x,118));
            foreach(float z in new[]{-316f,-254,-130,-68,-6,88})Route("Town cross street",8,new Vector2(-437,z),new Vector2(48,z));
            Route("Orchard valley road",9,new Vector2(-54,88),new Vector2(45,148),new Vector2(144,202),new Vector2(241,224),new Vector2(292,318),new Vector2(363,419),new Vector2(440,471));
            Route("Ridge lane",6,new Vector2(440,471),new Vector2(499,471),new Vector2(514,529));
            Route("Summit switchbacks",7,new Vector2(-394,88),new Vector2(-478,165),new Vector2(-490,262),new Vector2(-432,312),new Vector2(-425,365),new Vector2(-323,387),new Vector2(-303,474));
            Route("East cove road",8,new Vector2(365,-350),new Vector2(550,-238),new Vector2(649,-127),new Vector2(708,-28),new Vector2(728,72));
        }
        static Vector3 Ground(Vector2 p,float offset=0)=>new Vector3(p.x,Height(p.x,p.y)+offset,p.y);
        static void Route(string name,float width,params Vector2[] points)
        {
            roads.Add(points);var mesh=new EnvironmentGeometry(name,root);
            for(int s=0;s<points.Length-1;s++)
            {
                Vector2 start=points[s],end=points[s+1],direction=(end-start).normalized,side=new Vector2(direction.y,-direction.x);
                int n=Mathf.CeilToInt(Vector2.Distance(start,end)/5);
                for(int j=0;j<n;j++)
                {
                    Vector2 a=Vector2.Lerp(start,end,j/(float)n),b=Vector2.Lerp(start,end,(j+1)/(float)n),mid=(a+b)*.5f;
                    if(NearPad(new Vector3(mid.x,0,mid.y),25))continue;
                    Strip(mesh,a,b,side,-width*.5f-1,width*.5f+1,.025f,joint);
                    Strip(mesh,a,b,side,-width*.5f,width*.5f,.06f,IslandWorld.Asphalt);
                    Strip(mesh,a,b,side,-width*.5f+.34f,-width*.5f+.47f,.079f,paint);
                    Strip(mesh,a,b,side,width*.5f-.47f,width*.5f-.34f,.079f,paint);
                    if(j%3!=0)Strip(mesh,Vector2.Lerp(a,b,.1f),Vector2.Lerp(a,b,.88f),side,-.075f,.075f,.082f,yellow);
                }
            }
            mesh.Finish(false);
        }
        static void Strip(EnvironmentGeometry batch,Vector2 a,Vector2 b,Vector2 side,float left,float right,float y,Material material)
        {
            batch.Quad(Ground(a+side*left,y),Ground(b+side*left,y),Ground(b+side*right,y),Ground(a+side*right,y),material,(right-left)/3,Vector2.Distance(a,b)/3);
        }
        static void Airport()
        {
            var b=new EnvironmentGeometry("MERIDIAN AIR / terminal apron",root);
            b.Box(new Vector3(-425,8.18f,-513),new Vector3(194,1f,122),concrete);
            for(int x=0;x<17;x++)b.Box(new Vector3(-520+x*12,8.694f,-513),new Vector3(.05f,.015f,122),joint);
            for(int z=0;z<11;z++)b.Box(new Vector3(-425,8.695f,-573+z*12),new Vector3(194,.015f,.05f),joint);
            b.Box(new Vector3(-425,8.704f,-454),new Vector3(190,.02f,.2f),yellow);
            b.Box(new Vector3(-329,8.704f,-513),new Vector3(.2f,.02f,118),yellow);
            for(int i=0;i<12;i++)b.Box(new Vector3(-482+i*12,8.712f,-566),new Vector3(.2f,.02f,12),paint);
            for(int i=0;i<7;i++)
            {
                float x=-470+i*18;b.Box(new Vector3(x,8.716f,-464),new Vector3(9,.02f,.3f),yellow);
                b.Box(new Vector3(x+5,8.716f,-465),new Vector3(3,.02f,.3f),yellow,40);b.Box(new Vector3(x+5,8.716f,-463),new Vector3(3,.02f,.3f),yellow,-40);
            }
            Vector3 hangar=new Vector3(-491,8.7f,-545);Box("Airport hangar",hangar+Vector3.up*7,new Vector3(52,14,33),blue);
            b.Gable(hangar+Vector3.up*14,new Vector3(54,4,35),steel);
            b.Box(hangar+new Vector3(0,5.6f,16.6f),new Vector3(47,10.9f,.2f),tire);
            for(int i=0;i<6;i++)
            {
                float x=-23+i*9.2f;b.Box(hangar+new Vector3(x,5.6f,16.75f),new Vector3(7.6f,10.8f,.12f),steel);
                b.Box(hangar+new Vector3(x,9.66f,16.85f),new Vector3(6.4f,1.1f,.06f),glazing);
            }
            for(int i=0;i<22;i++)b.Box(hangar+new Vector3(-26.05f,7,-16.5f+i*1.57f),new Vector3(.1f,14,.07f),steel);
            b.Box(hangar+new Vector3(0,13.2f,16.78f),new Vector3(51.5f,1.2f,.3f),teal);
            Sign("MERIDIAN  /  04",hangar+new Vector3(0,13.2f,17),0,.75f,trim);
            Building(b,new Vector3(-350,8.7f,-510),26,31,8,trim,teal,0,false);
            b.Box(new Vector3(-365.4f,13,-506),new Vector3(4.5f,.35f,23),teal);
            for(int i=0;i<4;i++)b.Cylinder(new Vector3(-367,10.9f,-516+i*7),.13f,4.4f,steel);
            Sign("MERIDIAN AIR",new Vector3(-350,15.35f,-493.9f),0,1.05f,teal);
            Sign("ISLAND FLIGHT OPERATIONS",new Vector3(-350,10.3f,-493.8f),0,.43f,Metal);
            Building(b,new Vector3(-502,8.4f,-474),28,13,5.8f,plaster,blue,0,true);
            Sign("RESCUE + SERVICE",new Vector3(-502,12.15f,-467.35f),0,.58f,trim);
            for(int i=0;i<2;i++)
            {
                Vector3 p=new Vector3(-493+i*11,10.6f,-458);b.Cylinder(p,2.7f,4.2f,trim);b.Cylinder(p+Vector3.up*2.25f,2.78f,.22f,Signal);
                b.Box(p+new Vector3(0,.9f,2.72f),new Vector3(2,.5f,.08f),Signal);
            }
            for(int i=0;i<6;i++)Bollard(b,new Vector3(-504+i*4,8.73f,-454));
            Vehicle(b,new Vector3(-471,8.73f,-478),Signal,90,true);Vehicle(b,new Vector3(-343,8.73f,-551),yellow,0,false);Vehicle(b,new Vector3(-355,8.73f,-551),teal,0,false);
            for(int i=0;i<3;i++)Crate(b,new Vector3(-472+i*4,8.73f,-536));
            Fence(b,new Vector3(-520,8.65f,-447),new Vector3(-370,8.65f,-447),1.8f);Fence(b,new Vector3(-530,8.65f,-575),new Vector3(-530,8.65f,-449),1.8f);
            for(int i=0;i<3;i++)StreetLight(b,new Vector3(-522,8.65f,-546+i*43),90);
            b.Finish();
        }
        static void Town()
        {
            var random=new System.Random(74);Material[] walls={plaster,ochre,coral,trim,teal};
            for(int district=0;district<3;district++)
            {
                var b=new EnvironmentGeometry("Port Meridian / town district "+district,root);
                for(int x=0;x<6;x++)for(int z=0;z<2;z++)
                {
                    float px=-360+x*68,pz=-286+(district*2+z)*62;
                    if(NearPad(new Vector3(px,0,pz),44)||NearRoad(new Vector3(px,0,pz),26))continue;
                    float y=Height(px,pz),h=8+random.Next(0,4)*3.3f,w=30+random.Next(0,8),d=29+random.Next(0,8);
                    b.Box(new Vector3(px,y+.04f,pz),new Vector3(51,.12f,49),joint);b.Box(new Vector3(px,y+.13f,pz),new Vector3(45,.20f,43),concrete);
                    Building(b,new Vector3(px,y+.24f,pz),w,d,h,walls[(x+z+district*2)%walls.Length],(x+z)%2==0?Roof:blue,0,(x+z+district)%3==0);
                    if(district<2)
                    {
                        float front=pz+d*.5f+.19f;b.Box(new Vector3(px,y+2.65f,front+1),new Vector3(w-3,.25f,2.4f),x%2==0?teal:coral);
                        for(int s=0;s<4;s++)b.Box(new Vector3(px-w*.36f+s*w*.24f,y+1.25f,front),new Vector3(w*.16f,2.1f,.16f),glazing);
                        string[] shops={"COASTAL CAFE","ISLAND MARKET","MERIDIAN MOTORS","FERRY SUPPLY","PALM HOTEL","PACIFIC RADIO"};
                        Sign(shops[x],new Vector3(px,y+3.7f,front+.07f),0,.54f,trim);
                    }
                    for(int t=0;t<2;t++)Tree(b,new Vector3(px+(t==0?-22:22),y,pz+20),6.5f+t,2+(x+t)%3,false);
                    if(x%2==0)Vehicle(b,new Vector3(px+17,y+.27f,pz-21),x%3==0?yellow:coral,90,false);
                    else StreetLight(b,new Vector3(px-23,y+.24f,pz+23),0);
                }
                b.Finish();
            }
            var park=new EnvironmentGeometry("Town green / gardens",root);Vector3 center=pads[1];
            for(int i=0;i<4;i++)
            {
                float angle=i*90*Mathf.Deg2Rad;Vector3 p=center+new Vector3(Mathf.Sin(angle)*31,-.2f,Mathf.Cos(angle)*31);
                park.Box(p,new Vector3(i%2==0?5:19,.15f,i%2==0?19:5),concrete);
                Tree(park,p+new Vector3(-5,0,0),11,i,true);Bench(park,p+new Vector3(5,0,0),i*90);
            }
            park.Finish();
        }
        static void Building(EnvironmentGeometry b,Vector3 p,float w,float d,float h,Material wall,Material roof,float yaw,bool pitched)
        {
            var body=Box("Coastal building",p+Vector3.up*h*.5f,new Vector3(w,h,d),wall);body.transform.rotation=Quaternion.Euler(0,yaw,0);
            Quaternion q=Quaternion.Euler(0,yaw,0);System.Action<Vector3,Vector3,Material> part=(v,s,m)=>b.Box(p+q*v,s,m,yaw);
            part(new Vector3(0,.35f,0),new Vector3(w+.2f,.7f,d+.2f),joint);part(new Vector3(0,h-.32f,0),new Vector3(w+.5f,.55f,d+.5f),trim);
            if(pitched)
            {
                b.Gable(p+Vector3.up*h,new Vector3(w+1.4f,3.4f,d+1.4f),roof,yaw);
                for(int i=0;i<Mathf.CeilToInt(d/1.6f);i++)
                {
                    float z=-d*.5f+i*1.6f;b.Beam(p+q*new Vector3(-w*.5f,h+.06f,z),p+q*new Vector3(0,h+3.43f,z),.045f,joint);
                    b.Beam(p+q*new Vector3(w*.5f,h+.06f,z),p+q*new Vector3(0,h+3.43f,z),.045f,joint);
                }
            }
            else
            {
                part(new Vector3(0,h+.05f,0),new Vector3(w,.18f,d),joint);
                foreach(float s in new[]{-1f,1f})
                {
                    part(new Vector3(0,h+.45f,s*d*.5f),new Vector3(w+.45f,.8f,.4f),wall);part(new Vector3(s*w*.5f,h+.45f,0),new Vector3(.4f,.8f,d+.45f),wall);
                }
                part(new Vector3(-w*.24f,h+.75f,-d*.2f),new Vector3(3,1.5f,2.6f),steel);part(new Vector3(w*.19f,h+.5f,-d*.17f),new Vector3(2.8f,.95f,2.2f),trim);
                for(int j=0;j<5;j++)part(new Vector3(-w*.24f-.95f+j*.47f,h+1.52f,-d*.2f),new Vector3(.2f,.04f,1.9f),tire);
            }
            int floors=Mathf.Max(1,Mathf.FloorToInt((h-1)/3.3f)),columns=Mathf.Max(2,Mathf.FloorToInt((w-3)/4.4f)),sideColumns=Mathf.Max(2,Mathf.FloorToInt((d-3)/4.4f));
            for(int floor=0;floor<floors;floor++)
            {
                float level=2.1f+floor*3.3f;
                for(int col=0;col<columns;col++)foreach(float s in new[]{-1f,1f})
                {
                    float x=(col-(columns-1)*.5f)*4.4f;
                    part(new Vector3(x,level,s*(d*.5f+.055f)),new Vector3(2.05f,1.9f,.13f),trim);
                    part(new Vector3(x,level+.03f,s*(d*.5f+.135f)),new Vector3(1.7f,1.58f,.035f),glazing);
                    part(new Vector3(x,level+.03f,s*(d*.5f+.16f)),new Vector3(.065f,1.6f,.04f),trim);
                    part(new Vector3(x,level-.95f,s*(d*.5f+.25f)),new Vector3(2.16f,.11f,.42f),trim);
                }
                for(int col=0;col<sideColumns;col++)foreach(float s in new[]{-1f,1f})
                {
                    float z=(col-(sideColumns-1)*.5f)*4.4f;part(new Vector3(s*(w*.5f+.06f),level,z),new Vector3(.14f,1.9f,2.05f),trim);
                    part(new Vector3(s*(w*.5f+.145f),level+.03f,z),new Vector3(.04f,1.58f,1.7f),glazing);
                }
            }
            part(new Vector3(0,1.15f,d*.5f+.18f),new Vector3(1.6f,2.3f,.14f),teal);part(new Vector3(.5f,1.1f,d*.5f+.29f),new Vector3(.09f,.4f,.05f),steel);
            part(new Vector3(0,2.52f,d*.5f+.65f),new Vector3(2.7f,.15f,1.3f),trim);
        }
        static void Harbor()
        {
            var b=new EnvironmentGeometry("Ferry terminal / working harbor",root);
            // The quay follows the existing land surface and stays below the ferry pad.
            // The shoreline is around x=-1000 here, so piers extend to actual open water.
            for(int x=0;x<13;x++)for(int z=0;z<10;z++)
            {
                Vector2 a=new Vector2(-887+x*10,-254+z*10),c=a+new Vector2(10,10);
                b.Quad(Ground(a,.05f),Ground(new Vector2(a.x,c.y),.05f),Ground(c,.05f),Ground(new Vector2(c.x,a.y),.05f),concrete,2,2);
            }
            Box("Harbor seawall",new Vector3(-897,1,-196),new Vector3(5,9,114),joint);
            for(int i=0;i<3;i++)
            {
                float z=-251+i*49;Box("Harbor pier",new Vector3(-973,-.3f,z),new Vector3(174,10.4f,18),concrete);
                for(int p=0;p<17;p++)
                {
                    b.Box(new Vector3(-1054+p*10,4.92f,z),new Vector3(.065f,.03f,18),joint);Bollard(b,new Vector3(-1052+p*10,4.95f,z-7));
                    b.Cylinder(new Vector3(-1052+p*10,-3,z+7),.55f,16,Wood);
                }
                Boat(b,new Vector3(-1032+i*4,-2.25f,z+19),i==0?trim:teal,i==0?24:15,-90);
            }
            Building(b,new Vector3(-820,7.5f,-188),30,18,7.5f,plaster,teal,90,true);
            Sign("PORT MERIDIAN / FERRIES",new Vector3(-829.2f,12.75f,-188),-90,.55f,trim);
            Building(b,new Vector3(-817,8.1f,-250),45,19,9.5f,brick,steel,0,true);Sign("HARBOUR STORES",new Vector3(-817,15.05f,-240.15f),0,.72f,trim);
            Crane(b,new Vector3(-862,6.35f,-264),31,26,0);
            for(int i=0;i<8;i++){float x=-867+i%2*10,z=-218+i/2*10;Crate(b,new Vector3(x,Height(x,z)+.1f,z));}
            Fence(b,new Vector3(-853,Height(-853,-151)+.1f,-151),new Vector3(-784,Height(-784,-151)+.1f,-151),1.15f);
            Vehicle(b,new Vector3(-800,Height(-800,-220)+.1f,-220),yellow,90,false);b.Finish();
        }
        static void Freight()
        {
            var b=new EnvironmentGeometry("Freight yard / container logistics",root);b.Box(new Vector3(440,7.88f,-447),new Vector3(208,1f,133),joint);
            for(int i=0;i<8;i++)b.Box(new Vector3(354+i*25,8.4f,-447),new Vector3(.15f,.02f,125),paint);
            for(int row=0;row<4;row++)for(int col=0;col<7;col++)
            {
                Vector3 p=new Vector3(367+col*22,8.42f,-487+row*19);Material color=(row+col)%4==0?coral:(row+col)%4==1?blue:(row+col)%4==2?teal:ochre;
                Container(b,p,color,col%2==0?12.2f:9.2f);if((row+col)%3==0)Container(b,p+Vector3.up*2.95f,(row+col)%2==0?blue:trim,12.2f);
            }
            Building(b,new Vector3(548,8.1f,-468),45,82,13,plaster,steel,0,true);Sign("MERIDIAN FREIGHT",new Vector3(548,18.3f,-426.8f),0,.85f,teal);
            for(int i=0;i<4;i++)
            {
                b.Box(new Vector3(524.9f,11.8f,-497+i*20),new Vector3(.3f,6.8f,10),tire);b.Box(new Vector3(524.7f,11.5f,-497+i*20),new Vector3(.1f,6,8.5f),steel);
                b.Box(new Vector3(520.8f,8.7f,-497+i*20),new Vector3(8,1,12),concrete);
            }
            Crane(b,new Vector3(394,8.4f,-417),30,37,90);Vehicle(b,new Vector3(518,8.42f,-418),trim,0,true);Vehicle(b,new Vector3(493,8.42f,-504),yellow,90,true);
            Fence(b,new Vector3(335,8.4f,-512),new Vector3(580,8.4f,-512),2.3f);Fence(b,new Vector3(335,8.4f,-512),new Vector3(335,8.4f,-397),2.3f);
            for(int i=0;i<4;i++)StreetLight(b,new Vector3(347+i*61,8.4f,-506),0);b.Finish();
        }
        static void Container(EnvironmentGeometry b,Vector3 p,Material color,float length)
        {
            b.Box(p+Vector3.up*1.45f,new Vector3(length,2.9f,2.45f),color);
            for(int i=0;i<Mathf.FloorToInt(length/.42f);i++)foreach(float s in new[]{-1f,1f})
                b.Box(p+new Vector3(-length*.5f+.22f+i*.42f,1.46f,s*1.26f),new Vector3(.08f,2.64f,.11f),color);
            foreach(float s in new[]{-1f,1f})
            {
                b.Box(p+new Vector3(s*(length*.5f+.02f),1.45f,0),new Vector3(.1f,2.82f,.075f),steel);
                for(int j=0;j<2;j++)b.Box(p+new Vector3(s*(length*.5f+.07f),1.45f,-.6f+j*1.2f),new Vector3(.08f,2.5f,.055f),steel);
                b.Box(p+new Vector3(s*length*.5f,2.85f,0),new Vector3(.1f,.12f,2.5f),steel);
            }
            b.Box(p+new Vector3(0,1.7f,1.33f),new Vector3(3,.6f,.04f),trim);
        }
        static void Crane(EnvironmentGeometry b,Vector3 p,float height,float reach,float yaw)
        {
            Quaternion q=Quaternion.Euler(0,yaw,0);System.Func<Vector3,Vector3> at=v=>p+q*v;b.Box(p+Vector3.up*.7f,new Vector3(9,1.4f,9),concrete);
            for(int i=0;i<4;i++)b.Beam(at(new Vector3(i%2==0?-1.8f:1.8f,1,i<2?-1.8f:1.8f)),at(new Vector3(i%2==0?-1.8f:1.8f,height,i<2?-1.8f:1.8f)),.48f,yellow);
            for(int h=1;h<height;h+=4)for(int side=0;side<4;side++)
            {
                Vector3 a=Quaternion.Euler(0,side*90,0)*new Vector3(-1.8f,h,1.8f),c=Quaternion.Euler(0,side*90,0)*new Vector3(1.8f,h+4,1.8f);b.Beam(at(a),at(c),.18f,yellow);
            }
            b.Box(at(new Vector3(0,height+1.3f,0)),new Vector3(5,2.6f,5),yellow,yaw);b.Box(at(new Vector3(-2,height+.8f,3.1f)),new Vector3(2.4f,2.6f,2.5f),teal,yaw);
            b.Box(at(new Vector3(-2,height+1.1f,4.39f)),new Vector3(2,1.5f,.08f),glazing,yaw);
            b.Beam(at(new Vector3(-9,height+2,0)),at(new Vector3(reach,height+2,0)),.6f,yellow);b.Beam(at(new Vector3(-9,height+5,0)),at(new Vector3(reach,height+2,0)),.27f,yellow);
            for(int i=0;i<reach+7;i+=4)b.Beam(at(new Vector3(-7+i,height+2,0)),at(new Vector3(-5+i,height+4.5f-i/reach*2.5f,0)),.18f,yellow);
            b.Box(at(new Vector3(-8,height+1,0)),new Vector3(5,3.5f,4),joint,yaw);b.Beam(at(new Vector3(reach-4,height+2,0)),at(new Vector3(reach-4,6,0)),.065f,tire);
            b.Box(at(new Vector3(reach-4,6,0)),new Vector3(2,.7f,1.5f),yellow,yaw);
        }
        static void Landmarks()
        {
            var b=new EnvironmentGeometry("Island destinations / distinct landmarks",root);Vector3 clinic=pads[4];
            for(int floor=0;floor<10;floor++)foreach(float side in new[]{-1f,1f})
            {
                float y=6+floor*3.5f;b.Box(new Vector3(clinic.x,y,clinic.z+side*17.1f),new Vector3(30,1.8f,.15f),glazing);
                b.Box(new Vector3(clinic.x+side*17.1f,y,clinic.z),new Vector3(.15f,1.8f,30),glazing);
                for(int j=0;j<8;j++)
                {
                    b.Box(new Vector3(clinic.x-14+j*4,y,clinic.z+side*17.22f),new Vector3(.22f,2.1f,.12f),trim);
                    b.Box(new Vector3(clinic.x+side*17.22f,y,clinic.z-14+j*4),new Vector3(.12f,2.1f,.22f),trim);
                }
            }
            foreach(float side in new[]{-1f,1f})
            {
                b.Box(clinic+new Vector3(side*16.8f,.25f,0),new Vector3(.35f,.55f,34),trim);b.Box(clinic+new Vector3(0,.25f,side*16.8f),new Vector3(34,.55f,.35f),trim);
            }
            b.Box(new Vector3(clinic.x,39.2f,clinic.z+17.3f),new Vector3(26,2.8f,.25f),teal);
            Sign("MERIDIAN MEDICAL",new Vector3(clinic.x,39.2f,clinic.z+17.5f),0,.8f,trim);
            b.Box(new Vector3(clinic.x+12,43.3f,clinic.z-12),new Vector3(5,1.5f,5),steel);b.Box(new Vector3(clinic.x-12,43.3f,clinic.z-12),new Vector3(3,1.5f,4),steel);
            Vector3 farm=new Vector3(156,Height(156,256),256);Building(b,farm,25,18,6.5f,trim,Roof,0,true);
            Fence(b,new Vector3(139,Height(139,231),231),new Vector3(139,Height(139,325),325),1.15f);
            for(int x=0;x<7;x++)for(int z=0;z<6;z++)
            {
                Vector3 p=new Vector3(178+x*13,0,310+z*13);p.y=Height(p.x,p.z);if(NearPad(p,35))continue;
                Tree(b,p,5.3f+(x+z)%3*.6f,x+z,false);
                for(int fruit=0;fruit<3;fruit++)b.Rock(p+new Vector3(-1+fruit,3.5f,.9f),new Vector3(.18f,.18f,.18f),Signal,fruit*31);
            }
            Vector3 ridge=new Vector3(521,Height(521,529),529);Building(b,ridge,20,13,6,trim,blue,0,true);
            RadioMast(b,new Vector3(536,Height(536,546),546),25);b.Cylinder(ridge+new Vector3(-15,1.4f,3),2.1f,2.8f,steel);
            Vector3 lodge=new Vector3(-395,Height(-395,446),446);Building(b,lodge,37,27,11,Wood,Roof,0,true);
            b.Box(lodge+new Vector3(0,4.4f,16),new Vector3(41,.35f,6),Wood);Fence(b,lodge+new Vector3(-20,4.7f,18),lodge+new Vector3(20,4.7f,18),1.15f);
            for(int i=0;i<6;i++)b.Cylinder(lodge+new Vector3(-18+i*7.2f,2.2f,18),.15f,4.4f,Wood);
            b.Box(lodge+new Vector3(11,13,0),new Vector3(2,7,3),rock);Sign("SUMMIT LODGE",lodge+new Vector3(0,8.7f,13.7f),0,.75f,trim);
            Vector3 light=new Vector3(-752,Height(-752,450),450);
            IslandWorld.Piece("Lighthouse tower",PrimitiveType.Cylinder,light+Vector3.up*15,new Vector3(8,15,8),trim);
            for(int i=0;i<3;i++)b.Cylinder(light+Vector3.up*(8+i*8),4.06f,2.6f,coral);
            b.Cylinder(light+Vector3.up*30.2f,5.2f,.55f,concrete);b.Cylinder(light+Vector3.up*32.5f,3.3f,4,glazing);
            b.Cone(light+Vector3.up*35.7f,new Vector3(4.1f,2.6f,4.1f),teal);
            for(int i=0;i<12;i++)
            {
                float a=i*30*Mathf.Deg2Rad;Vector3 offset=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),next=new Vector3(Mathf.Sin(a+30*Mathf.Deg2Rad),0,Mathf.Cos(a+30*Mathf.Deg2Rad));
                b.Cylinder(light+offset*3.32f+Vector3.up*32.5f,.085f,4,trim);b.Cylinder(light+offset*4.85f+Vector3.up*31,.06f,1.5f,steel);
                b.Beam(light+offset*4.85f+Vector3.up*31.7f,light+next*4.85f+Vector3.up*31.7f,.065f,steel);
            }
            b.Cylinder(light+Vector3.up*32.5f,.75f,1.5f,yellow);Building(b,light+new Vector3(-22,0,-9),19,12,5,trim,Roof,0,true);
            for(int i=0;i<4;i++)
            {
                Vector3 p=new Vector3(806+i%2*28,0,31+i/2*34);p.y=Height(p.x,p.z);Building(b,p,17,13,5.5f,i%2==0?ochre:coral,blue,0,true);
            }
            Vector3 jetty=new Vector3(991,1.2f,79);b.Box(jetty,new Vector3(130,.6f,6),Wood);
            for(int i=0;i<31;i++)
            {
                b.Box(jetty+new Vector3(-63+i*4.2f,.33f,0),new Vector3(.08f,.03f,6),tire);
                if(i%3==0)foreach(float s in new[]{-1f,1f})b.Cylinder(jetty+new Vector3(-63+i*4.2f,-3,s*2.6f),.23f,12,Wood);
            }
            Boat(b,new Vector3(1041,-2.5f,91),coral,11,0);b.Finish();
            var detail=new EnvironmentGeometry("Landing pads / inset edge lights",root);
            for(int i=0;i<pads.Length;i++)for(int n=0;n<8;n++)
            {
                float radius=i==0?17:i==4?9:i>5?10:14,a=n*45*Mathf.Deg2Rad;Vector3 p=pads[i]+new Vector3(Mathf.Sin(a)*(radius+1.1f),.02f,Mathf.Cos(a)*(radius+1.1f));
                detail.Cylinder(p,.2f,.075f,Metal);detail.Cylinder(p+Vector3.up*.05f,.11f,.065f,yellow);
            }
            detail.Finish();
        }
        static void Boat(EnvironmentGeometry b,Vector3 p,Material hull,float length,float yaw)
        {
            Quaternion q=Quaternion.Euler(0,yaw,0);System.Func<Vector3,Vector3> at=v=>p+q*v;float w=length*.23f,h=length*.12f;
            Vector3 a=at(new Vector3(-w*.5f,.4f,-length*.45f)),c=at(new Vector3(w*.5f,.4f,-length*.45f)),d=at(new Vector3(w*.5f,.4f,length*.25f)),e=at(new Vector3(0,.4f,length*.52f)),f=at(new Vector3(-w*.5f,.4f,length*.25f));
            Vector3 aa=at(new Vector3(-w*.35f,-h,-length*.40f)),cc=at(new Vector3(w*.35f,-h,-length*.40f)),dd=at(new Vector3(w*.30f,-h,length*.2f)),ee=at(new Vector3(0,-h*.5f,length*.45f)),ff=at(new Vector3(-w*.30f,-h,length*.2f));
            b.Quad(a,f,d,c,hull);b.Quad(f,e,d,d,hull);
            b.Quad(a,c,cc,aa,hull);b.Quad(c,d,dd,cc,hull);b.Quad(d,e,ee,dd,hull);b.Quad(e,f,ff,ee,hull);b.Quad(f,a,aa,ff,hull);
            b.Box(at(new Vector3(0,.5f,-length*.13f)),new Vector3(w*.87f,.3f,length*.57f),trim,yaw);
            b.Box(at(new Vector3(0,1.8f,-length*.1f)),new Vector3(w*.75f,2.3f,length*.24f),trim,yaw);b.Box(at(new Vector3(0,2.15f,length*.025f)),new Vector3(w*.65f,1.2f,.06f),glazing,yaw);
            foreach(float side in new[]{-1f,1f})b.Box(at(new Vector3(side*w*.383f,2.15f,-length*.1f)),new Vector3(.06f,1.2f,length*.18f),glazing,yaw);
            b.Box(at(new Vector3(0,3.04f,-length*.1f)),new Vector3(w*.9f,.22f,length*.30f),teal,yaw);
            b.Beam(at(new Vector3(0,3.1f,-length*.15f)),at(new Vector3(0,6.2f,-length*.15f)),.09f,steel);
            b.Beam(at(new Vector3(-1,5.2f,-length*.15f)),at(new Vector3(1,5.2f,-length*.15f)),.065f,trim);
            b.Beam(a+Vector3.up*.45f,c+Vector3.up*.45f,.075f,trim);b.Beam(a+Vector3.up*.45f,f+Vector3.up*.45f,.075f,trim);b.Beam(c+Vector3.up*.45f,d+Vector3.up*.45f,.075f,trim);
        }
        static void Vegetation()
        {
            var random=new System.Random(904);
            // Independent 165m cells keep scenery cullable; canopy clusters share materials.
            for(int cx=-6;cx<=5;cx++)for(int cz=-4;cz<=5;cz++)
            {
                var b=new EnvironmentGeometry("Vegetation cell "+cx+" / "+cz,root);int count=cz>0?42:12;
                for(int i=0;i<count;i++)
                {
                    float x=cx*165+Next(random)*165,z=cz*165+Next(random)*165,y=Height(x,z);Vector3 p=new Vector3(x,y,z);
                    if(y < -2 || NearPad(p,49) || NearRoad(p,14) || Developed(x,z))continue;
                    float slope=new Vector2(Height(x+4,z)-Height(x-4,z),Height(x,z+4)-Height(x,z-4)).magnitude/8;
                    if(y<4.7f || slope>.45f)
                    {
                        float size=1.5f+Next(random)*4;b.Rock(p+Vector3.up*.15f,new Vector3(size,size*.65f,size*.8f),rock,Next(random)*360);
                        if(y>1 && i%2==0)Shrub(b,p+new Vector3(3,0,1),1.5f,random);
                    }
                    else
                    {
                        float stand=Mathf.PerlinNoise(x*.005f+12,z*.005f+23);
                        bool woodland=z>130&&stand>.43f;
                        // Leave open grass clearings between dense hill stands instead of
                        // scattering one isolated tree at every random sample.
                        if(z>130&&!woodland&&i%5!=0)continue;
                        bool evergreen=z>200 || x<-590 || i%4==0;float h=evergreen?12+Next(random)*14:9+Next(random)*9;
                        Tree(b,p,h,i+cx*3+cz*7,evergreen);
                        int companions=woodland?(i%3==0?2:1):(i%3==0?1:0);
                        for(int companion=0;companion<companions;companion++)
                        {
                            float angle=Next(random)*Mathf.PI*2,distance=5+Next(random)*7;
                            Vector3 near=p+new Vector3(Mathf.Sin(angle)*distance,0,Mathf.Cos(angle)*distance);near.y=Height(near.x,near.z);
                            if(NearPad(near,49)||NearRoad(near,14)||Developed(near.x,near.z))continue;
                            Tree(b,near,h*(.70f+Next(random)*.18f),i+12+companion*4,evergreen);
                        }
                        if(i%4==0)Shrub(b,p+new Vector3(-3,0,3),1.3f,random);
                    }
                }
                b.Finish();
            }
            var foreground=new EnvironmentGeometry("Airfield / coastal tree line",root);
            for(int i=0;i<20;i++)
            {
                Vector3 p=new Vector3(-550-i%3*11,0,-558+i*9);p.y=Height(p.x,p.z);Tree(foreground,p,10+i%4*2.4f,i,true);
            }
            for(int i=0;i<15;i++)
            {
                Vector3 p=new Vector3(-497+i*11,0,-597-i%3*9);p.y=Height(p.x,p.z);Tree(foreground,p,8+i%3*3,i,false);
            }
            foreground.Finish();
        }
        static void Tree(EnvironmentGeometry b,Vector3 p,float h,int seed,bool evergreen)
        {
            float yaw=seed*137.51f,r=h*(evergreen?.19f:.25f);b.Cylinder(p+Vector3.up*h*.36f,h*.019f,h*.72f,bark);
            if(evergreen)
            {
                b.Evergreen(p+Vector3.up*h*.59f,new Vector3(r*1.22f,h*.82f,r*1.22f),pine,yaw);
                // Uneven branch clusters interrupt the continuous crown outline.
                for(int limb=0;limb<7;limb++)
                {
                    float a=(yaw+limb*137)*Mathf.Deg2Rad,level=h*(.34f+limb*.045f),width=r*(.75f-limb*.055f);
                    Vector3 end=p+new Vector3(Mathf.Sin(a)*width,level,Mathf.Cos(a)*width);
                    b.Beam(p+Vector3.up*(level+.4f),end,.075f,bark);
                    b.Foliage(end,new Vector3(r*.43f,h*.095f,r*.45f),limb%3==0?leaf:pine,yaw+limb*73);
                }
            }
            else
            {
                for(int limb=0;limb<6;limb++)
                {
                    float a=(limb*60+yaw)*Mathf.Deg2Rad,d=r*(limb%2==0?.65f:.85f),level=h*(.63f+limb%3*.09f);Vector3 end=p+new Vector3(Mathf.Sin(a)*d,level,Mathf.Cos(a)*d);
                    b.Beam(p+Vector3.up*h*.4f,end,h*.013f,bark);b.Foliage(end,new Vector3(r*.79f,h*.28f,r*.79f),limb%3==0?leafLight:leaf,yaw+limb*53);
                }
                b.Foliage(p+Vector3.up*h*.86f,new Vector3(r*.9f,h*.23f,r*.9f),leafLight,yaw);
            }
        }
        static void Shrub(EnvironmentGeometry b,Vector3 p,float size,System.Random random)
        {
            for(int i=0;i<3;i++)b.Foliage(p+new Vector3((Next(random)-.5f)*size,.2f,(Next(random)-.5f)*size),new Vector3(size,size*.65f,size),i==0?leafLight:leaf,Next(random)*360);
        }
        static void Vehicle(EnvironmentGeometry b,Vector3 p,Material body,float yaw,bool utility)
        {
            Quaternion q=Quaternion.Euler(0,yaw,0);System.Action<Vector3,Vector3,Material> part=(v,s,m)=>b.Box(p+q*v,s,m,yaw);float length=utility?5.3f:4.5f;
            part(new Vector3(0,.75f,0),new Vector3(1.9f,.7f,length),body);part(new Vector3(0,1.38f,utility?.5f:0),new Vector3(1.73f,.75f,utility?2.2f:2.8f),body);
            part(new Vector3(0,1.44f,utility?1.62f:1.44f),new Vector3(1.52f,.55f,.055f),glazing);
            foreach(float side in new[]{-1f,1f})
            {
                part(new Vector3(side*.89f,1.42f,utility?.55f:0),new Vector3(.035f,.52f,utility?1.7f:2.2f),glazing);
                foreach(float z in new[]{-length*.31f,length*.31f})part(new Vector3(side*.92f,.40f,z),new Vector3(.25f,.77f,.77f),tire);
                part(new Vector3(side*.62f,.76f,length*.5f+.04f),new Vector3(.42f,.23f,.045f),trim);part(new Vector3(side*.62f,.76f,-length*.5f-.04f),new Vector3(.33f,.21f,.045f),Signal);
            }
            part(new Vector3(0,.47f,length*.5f+.08f),new Vector3(1.75f,.15f,.17f),steel);
            if(utility)part(new Vector3(0,1.1f,-1.5f),new Vector3(1.6f,.3f,1.7f),joint);else if(body==yellow)part(new Vector3(0,1.89f,0),new Vector3(.65f,.25f,.35f),trim);
        }
        static void StreetLight(EnvironmentGeometry b,Vector3 p,float yaw)
        {
            Quaternion q=Quaternion.Euler(0,yaw,0);b.Cylinder(p+Vector3.up*4.4f,.1f,8.8f,steel);b.Beam(p+Vector3.up*8.7f,p+q*new Vector3(0,8.85f,2.2f),.1f,steel);
            b.Box(p+q*new Vector3(0,8.8f,2.3f),new Vector3(.4f,.18f,.9f),Metal,yaw);b.Box(p+q*new Vector3(0,8.69f,2.3f),new Vector3(.30f,.025f,.7f),trim,yaw);
        }
        static void Bench(EnvironmentGeometry b,Vector3 p,float yaw)
        {
            b.Box(p+Vector3.up*.5f,new Vector3(2.5f,.16f,.7f),Wood,yaw);b.Box(p+Quaternion.Euler(0,yaw,0)*new Vector3(0,.95f,-.3f),new Vector3(2.5f,.7f,.12f),Wood,yaw);
            foreach(float s in new[]{-1f,1f})b.Box(p+Quaternion.Euler(0,yaw,0)*new Vector3(s*.9f,.25f,0),new Vector3(.1f,.5f,.65f),Metal,yaw);
        }
        static void Bollard(EnvironmentGeometry b,Vector3 p)
        { b.Cylinder(p+Vector3.up*.48f,.15f,.96f,yellow);b.Cylinder(p+Vector3.up*.68f,.153f,.22f,Metal); }
        static void Crate(EnvironmentGeometry b,Vector3 p)
        {
            b.Box(p+Vector3.up*.9f,new Vector3(3.4f,1.8f,3),Wood);
            for(int j=0;j<3;j++)b.Box(p+new Vector3(-1.3f+j*1.3f,1,1.55f),new Vector3(.11f,1.7f,.09f),ochre);
        }
        static void Fence(EnvironmentGeometry b,Vector3 a,Vector3 c,float height)
        {
            int n=Mathf.CeilToInt(Vector3.Distance(a,c)/4);for(int i=0;i<=n;i++)b.Cylinder(Vector3.Lerp(a,c,i/(float)n)+Vector3.up*height*.5f,.055f,height,steel);
            b.Beam(a+Vector3.up*height,c+Vector3.up*height,.055f,steel);b.Beam(a+Vector3.up*height*.52f,c+Vector3.up*height*.52f,.04f,steel);b.Beam(a+Vector3.up*.18f,c+Vector3.up*.18f,.04f,steel);
        }
        static void RadioMast(EnvironmentGeometry b,Vector3 p,float height)
        {
            b.Cylinder(p+Vector3.up*height*.5f,.17f,height,steel);
            for(int i=0;i<3;i++)
            {
                float a=i*120*Mathf.Deg2Rad;b.Beam(p+new Vector3(Mathf.Sin(a)*7,0,Mathf.Cos(a)*7),p+Vector3.up*height*.7f,.045f,steel);
                b.Box(p+new Vector3(Mathf.Sin(a)*1.4f,height-3,Mathf.Cos(a)*1.4f),new Vector3(.5f,3,.6f),trim,i*120);
            }
            b.Cylinder(p+Vector3.up*height,.2f,.5f,Signal);
        }
        static void Sign(string text,Vector3 p,float yaw,float size,Material color)
        {
            // TextMesh characterSize multiplies the glyph's pixel coordinates / 10.
            // Convert an authored capital height in metres instead of magnifying 64px text.
            signFont.GetCharacterInfo('M',out CharacterInfo capital,64);
            float characterSize=size*10/Mathf.Max(1,capital.maxY-capital.minY),advance=0;
            foreach(char letter in text)if(signFont.GetCharacterInfo(letter,out CharacterInfo glyph,64))advance+=glyph.advance;
            Vector3 outward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
            var board=IslandWorld.Piece(text+" / signboard",PrimitiveType.Cube,p-outward*.085f,new Vector3(advance*characterSize*.1f+.65f,size+.42f,.14f),color==teal||color==Metal?trim:teal,false);
            board.transform.rotation=Quaternion.Euler(0,yaw,0);
            var go=new GameObject(text,typeof(TextMesh));go.transform.SetParent(root,false);go.transform.position=p+outward*.015f;go.transform.rotation=Quaternion.Euler(0,yaw+180,0);
            var label=go.GetComponent<TextMesh>();label.font=signFont;label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.characterSize=characterSize;label.fontSize=64;label.color=color.color;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=signMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material material)=>IslandWorld.Piece(name,PrimitiveType.Cube,p,size,material);
        static float Next(System.Random random)=>(float)random.NextDouble();
        static bool NearPad(Vector3 p,float distance)
        { foreach(var pad in pads)if(Vector2.Distance(new Vector2(pad.x,pad.z),new Vector2(p.x,p.z))<distance)return true;return false; }
        static bool NearRoad(Vector3 p,float distance)
        {
            Vector2 point=new Vector2(p.x,p.z);foreach(var path in roads)for(int i=0;i<path.Length-1;i++)
            {
                Vector2 delta=path[i+1]-path[i];float t=Mathf.Clamp01(Vector2.Dot(point-path[i],delta)/delta.sqrMagnitude);if(Vector2.Distance(point,path[i]+delta*t)<distance)return true;
            }
            return false;
        }
        static bool Developed(float x,float z)
        {
            return(x>-445&&x<59&&z>-335&&z<127)||(x>-533&&x<-323&&z>-582&&z<-447)||(x>325&&x<580&&z>-519&&z<-390)||
                  (x<-755&&z>-285&&z<-135)||(x>138&&x<278&&z>235&&z<399)||(x>797&&x<864&&z>12&&z<87);
        }
    }
}
