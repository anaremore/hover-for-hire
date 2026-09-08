using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoverForHire
{
    /// <summary>Bounded, original procedural VFX. All debris and camera motion are cosmetic.</summary>
    [DisallowMultipleComponent]
    public sealed class AircraftEffects : MonoBehaviour
    {
        public HelicopterController Aircraft;
        public FlightAudio Audio;
        public int ActiveDebrisCount { get { int count=0;foreach(var part in debris)if(part!=null)count++;return count; } }
        public int EffectParticleCount => Count(dust)+Count(smoke)+Count(sparks)+Count(flame)+Count(splash)+Count(washStreak);
        public bool HasExploded { get; private set; }

        const float SeaLevel=-3.5f;
        ParticleSystem dust,smoke,sparks,flame,splash,washStreak;
        Material material,fireMaterial;
        Texture2D particleTexture;
        AudioSource impactAudio;
        AudioClip impactClip;
        Light flash;
        ImpactCameraShake cameraShake;
        bool initialized,waterEntered,smoking;
        float nextWashAt,nextSmokeAt,nextImpactAt,flashUntil,fireUntil;
        readonly RaycastHit[] groundHits=new RaycastHit[24];
        readonly List<GameObject> debris=new List<GameObject>();
        readonly List<Renderer> hiddenRenderers=new List<Renderer>();
        readonly List<bool> rendererStates=new List<bool>();
        readonly List<Renderer> scorchedRenderers=new List<Renderer>();
        readonly List<MaterialPropertyBlock> originalProperties=new List<MaterialPropertyBlock>();

        void Start()=>Initialize(Aircraft!=null?Aircraft:GetComponent<HelicopterController>(),Audio);

        public void Initialize(HelicopterController aircraft,FlightAudio audio)
        {
            if(initialized||aircraft==null)return;
            initialized=true;Aircraft=aircraft;Audio=audio;
            particleTexture=MakeParticleTexture();
            Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if(shader==null)shader=Shader.Find("Universal Render Pipeline/Unlit");
            material=new Material(shader){name="Original soft airborne particles",renderQueue=(int)RenderQueue.Transparent,hideFlags=HideFlags.HideAndDontSave};
            material.SetTexture("_BaseMap",particleTexture);material.SetTexture("_MainTex",particleTexture);
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",(float)CullMode.Off);
            material.SetOverrideTag("RenderType","Transparent");material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dust=CreateParticles("Rotor wash / dust",360,.02f);
            smoke=CreateParticles("Airframe / smoke",180,-.045f);
            sparks=CreateParticles("Impact / sparks",160,.85f,true);
            flame=CreateParticles("High energy / fire",100,-.025f);
            splash=CreateParticles("Water / spray",240,1.05f,true);
            var sprayRenderer=splash.GetComponent<ParticleSystemRenderer>();sprayRenderer.lengthScale=3.5f;sprayRenderer.velocityScale=.18f;
            washStreak=CreateParticles("Rotor wash / spreading ground gusts",72,0,true);
            var gustRenderer=washStreak.GetComponent<ParticleSystemRenderer>();gustRenderer.lengthScale=5;gustRenderer.velocityScale=.35f;
            fireMaterial=new Material(material){name="White hot fire core",hideFlags=HideFlags.HideAndDontSave};fireMaterial.SetColor("_BaseColor",new Color(3.5f,3.1f,2.5f,1));
            flame.GetComponent<ParticleSystemRenderer>().sharedMaterial=fireMaterial;
            var flameColor=flame.colorOverLifetime;var fireGradient=new Gradient();
            fireGradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.82f,.36f),.16f),new GradientColorKey(new Color(1,.29f,.025f),.42f),new GradientColorKey(new Color(.20f,.055f,.012f),1)},new[]{new GradientAlphaKey(.95f,0),new GradientAlphaKey(.95f,.28f),new GradientAlphaKey(.75f,.65f),new GradientAlphaKey(0,1)});flameColor.color=fireGradient;
            impactAudio=gameObject.AddComponent<AudioSource>();impactAudio.playOnAwake=false;impactAudio.spatialBlend=0;impactAudio.clip=impactClip=MakeImpactClip();
            var lamp=new GameObject("Impact flash");lamp.transform.SetParent(transform,false);flash=lamp.AddComponent<Light>();flash.type=LightType.Point;flash.range=22;flash.color=new Color(1,.48f,.15f);flash.enabled=false;
            var cam=Camera.main;if(cam!=null)cameraShake=cam.GetComponent<ImpactCameraShake>()??cam.gameObject.AddComponent<ImpactCameraShake>();
            Aircraft.Impact+=PresentImpact;Aircraft.CrashedEvent+=OnCrash;Aircraft.ResetPerformed+=ResetEffects;
        }

        void Update()
        {
            if(!initialized||Aircraft==null||Time.deltaTime<=0)return;
            if(flash!=null){flash.enabled=Time.time<flashUntil;flash.intensity=Mathf.Max(0,flashUntil-Time.time)*22;}
            Vector3 location=Aircraft.transform.position;
            bool aboveWater=IslandWorld.Height(location.x,location.z)<SeaLevel;
            if(!waterEntered&&aboveWater&&location.y-Aircraft.Tuning.SkidClearanceMeters<=SeaLevel)
            {
                waterEntered=true;
                Vector3 velocity=Aircraft.Body!=null?Aircraft.Body.linearVelocity:Vector3.zero;
                PresentImpact(new AircraftImpact(new Vector3(location.x,SeaLevel,location.z),Vector3.up,velocity,Mathf.Max(1,Mathf.Abs(velocity.y)),Aircraft.Body!=null?Aircraft.Body.mass:900,true));
            }
            if(Time.time>=nextWashAt)
            {
                nextWashAt=Time.time+.055f;
                RotorWash(location,aboveWater);
            }
            if(smoking&&!waterEntered&&Time.time>=nextSmokeAt)
            {
                nextSmokeAt=Time.time+.09f;
                Vector3 source=Aircraft.transform.TransformPoint(new Vector3(0,.7f,-.8f));
                Emit(smoke,source,UnityEngine.Random.insideUnitSphere*.7f+Vector3.up*1.5f,new Color(.13f,.14f,.13f,.72f),UnityEngine.Random.Range(.9f,1.5f),UnityEngine.Random.Range(4.5f,7f));
                if(Time.time<fireUntil)
                {
                    Emit(flame,source,UnityEngine.Random.insideUnitSphere*.5f+Vector3.up*2,new Color(1,.8f,.55f,.8f),1.4f,.8f);
                    Emit(flame,source+Vector3.up*.4f,Vector3.up*2.5f,Color.white,.9f,.5f);
                }
            }
        }

        void RotorWash(Vector3 location,bool aboveWater)
        {
            if(Aircraft.RotorSpeed01<.2f||Aircraft.Crashed)return;
            Vector3 ground=new Vector3(location.x,SeaLevel,location.z);bool found=aboveWater,hardSurface=false;
            float closest=18;
            int count=Physics.RaycastNonAlloc(location+Vector3.up*.3f,Vector3.down,groundHits,18,~((1<<8)|(1<<2)),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=groundHits[i];if(hit.rigidbody==Aircraft.Body||hit.transform.IsChildOf(Aircraft.transform)||hit.distance>=closest)continue;
                closest=hit.distance;ground=hit.point;found=true;
                hardSurface=hit.collider.name.IndexOf("terrain",StringComparison.OrdinalIgnoreCase)<0;
            }
            if(!found)return;
            float altitude=location.y-ground.y;
            float strength=Mathf.Clamp01(1-altitude/14)*Aircraft.RotorSpeed01*Aircraft.RotorSpeed01*Mathf.Clamp01(.16f+Aircraft.RawCommand.Collective*1.45f);
            if(strength<.035f||altitude<0)return;
            bool waterSurface=aboveWater&&ground.y<=SeaLevel+.3f;
            if(hardSurface&&!waterSurface)strength*=.8f;
            int emitted=Mathf.CeilToInt(strength*8);
            float radius=2.1f+Mathf.Max(0,altitude)*.35f;
            for(int i=0;i<emitted;i++)
            {
                float angle=UnityEngine.Random.value*Mathf.PI*2;
                Vector3 outward=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                Vector3 p=ground+outward*UnityEngine.Random.Range(radius*.3f,radius)+Vector3.up*.13f;
                Vector3 velocity=outward*UnityEngine.Random.Range(2.5f,6)*strength+Vector3.up*UnityEngine.Random.Range(.2f,.6f);
                Color color=waterSurface?new Color(.79f,.90f,.88f,.28f):hardSurface?new Color(.69f,.70f,.63f,.21f):new Color(.65f,.56f,.39f,.32f);
                Emit(dust,p,velocity,color,UnityEngine.Random.Range(hardSurface?.4f:.7f,hardSurface?.8f:1.4f),UnityEngine.Random.Range(1.1f,2.3f));
                if(i%2==0)Emit(washStreak,ground+outward*radius+Vector3.up*.11f,outward*UnityEngine.Random.Range(4,8),new Color(.73f,.71f,.60f,waterSurface?.17f:.25f),UnityEngine.Random.Range(.07f,.15f),UnityEngine.Random.Range(.5f,.9f));
            }
        }

        public void PresentImpact(AircraftImpact impact)
        {
            if(!initialized)return;
            if(!impact.IsWater&&Time.time<nextImpactAt)return;
            nextImpactAt=Time.time+.12f;
            if(impact.IsWater)
            {
                waterEntered=true;smoking=false;fireUntil=0;
                int count=Mathf.Clamp(30+Mathf.RoundToInt(impact.Speed*5),30,150);
                for(int i=0;i<count;i++)
                {
                    Vector3 outward=UnityEngine.Random.onUnitSphere;outward.y=Mathf.Abs(outward.y);
                    outward.y=.35f+outward.y*.7f;
                    Emit(splash,impact.Point+UnityEngine.Random.insideUnitSphere*.8f,outward*UnityEngine.Random.Range(4,Mathf.Clamp(impact.Speed,6,15)),new Color(.78f,.91f,.90f,.8f),UnityEngine.Random.Range(.055f,.15f),UnityEngine.Random.Range(.8f,1.8f));
                }
                cameraShake?.AddImpact(Mathf.Clamp01(impact.Speed/20)*.5f);
                PlayImpact(impact.Speed,.8f);
                return;
            }
            if(impact.Speed<.7f)return;
            int dustCount=Mathf.Clamp(Mathf.RoundToInt(impact.Speed*3),5,55);
            for(int i=0;i<dustCount;i++)
            {
                Vector3 velocity=UnityEngine.Random.insideUnitSphere*Mathf.Clamp(impact.Speed*.35f,1,7)+impact.Normal*1.8f;
                Emit(dust,impact.Point+UnityEngine.Random.insideUnitSphere*.3f,velocity,new Color(.61f,.55f,.43f,.35f),UnityEngine.Random.Range(.35f,.9f),UnityEngine.Random.Range(.6f,1.8f));
            }
            if(impact.Severity>=ImpactSeverity.Scrape)
            {
                int count=Mathf.Clamp(Mathf.RoundToInt(impact.Speed*4),10,100);
                for(int i=0;i<count;i++)
                {
                    Vector3 velocity=UnityEngine.Random.onUnitSphere*UnityEngine.Random.Range(2,7)+impact.Normal*2;
                    Emit(sparks,impact.Point,velocity,new Color(1,.65f,.17f,1),UnityEngine.Random.Range(.045f,.09f),UnityEngine.Random.Range(.18f,.65f));
                }
                PlayImpact(impact.Speed,impact.Severity>=ImpactSeverity.Structural?.65f:1.2f);
                cameraShake?.AddImpact(Mathf.Clamp01(impact.Speed/24)*.8f);
            }
            if(impact.Severity>=ImpactSeverity.HardHit)
            {
                smoking=true;
                for(int i=0;i<12;i++)Emit(smoke,impact.Point,Vector3.up*1.5f+UnityEngine.Random.insideUnitSphere*1.4f,new Color(.23f,.24f,.23f,.5f),UnityEngine.Random.Range(.5f,1.1f),UnityEngine.Random.Range(2,3.5f));
            }
            if(impact.Severity>=ImpactSeverity.Structural)
            {
                if(impact.Severity==ImpactSeverity.Catastrophic)ScorchAirframe();
                Detach("Cabin door right",impact);
                Detach("Tail assembly",impact);
            }
            if(impact.Severity==ImpactSeverity.Catastrophic&&!HasExploded)
            {
                HasExploded=true;smoking=true;fireUntil=Time.time+9;flashUntil=Time.time+.28f;flash.transform.position=impact.Point+Vector3.up*1.5f;
                Detach("Main rotor (visual only)",impact);Detach("Cabin door left",impact);
                for(int i=0;i<65;i++)
                {
                    Vector3 outward=UnityEngine.Random.onUnitSphere;
                    Emit(flame,impact.Point+Vector3.up+outward*.6f,outward*UnityEngine.Random.Range(4,9),Color.Lerp(new Color(1,.76f,.35f,.9f),Color.white,UnityEngine.Random.value),UnityEngine.Random.Range(1.6f,3.5f),UnityEngine.Random.Range(.65f,1.3f));
                    if(i<35)Emit(smoke,impact.Point+Vector3.up,outward*UnityEngine.Random.Range(2,4)+Vector3.up*2,new Color(.075f,.085f,.08f,.83f),UnityEngine.Random.Range(1.6f,3),UnityEngine.Random.Range(4,7));
                }
            }
        }

        void OnCrash()
        {
            // A slow tip-over or water recovery gets no invented explosion.
            Vector3 p=Aircraft.transform.position;
            bool submerged=IslandWorld.Height(p.x,p.z)<SeaLevel&&p.y-Aircraft.Tuning.SkidClearanceMeters<=SeaLevel;
            if(submerged&&!waterEntered)
            {
                Vector3 velocity=Aircraft.Body!=null?Aircraft.Body.linearVelocity:Vector3.zero;
                PresentImpact(new AircraftImpact(new Vector3(p.x,SeaLevel,p.z),Vector3.up,velocity,Mathf.Max(1,Mathf.Abs(velocity.y)),Aircraft.Body!=null?Aircraft.Body.mass:900,true));
            }
            smoking=!waterEntered;
        }

        void Detach(string partName,AircraftImpact impact)
        {
            if(debris.Count>=4)return;
            Transform source=null;
            foreach(Transform candidate in Aircraft.GetComponentsInChildren<Transform>())if(candidate.name==partName){source=candidate;break;}
            if(source==null)return;
            Renderer[] renderers=source.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0||hiddenRenderers.Contains(renderers[0]))return;
            var fragment=Instantiate(source.gameObject,source.position,source.rotation);fragment.name="Cosmetic wreckage / "+partName;
            fragment.transform.localScale=source.lossyScale;
            if(partName=="Tail assembly")
            {
                foreach(Transform rotor in Aircraft.GetComponentsInChildren<Transform>())
                {
                    if(rotor.name!="Tail rotor (visual only)")continue;
                    var rotorFragment=Instantiate(rotor.gameObject,rotor.position,rotor.rotation);
                    rotorFragment.transform.localScale=rotor.lossyScale;rotorFragment.transform.SetParent(fragment.transform,true);
                    HideRenderers(rotor.GetComponentsInChildren<Renderer>());break;
                }
            }
            foreach(Transform child in fragment.GetComponentsInChildren<Transform>())child.gameObject.layer=2;
            HideRenderers(renderers);
            // Kinematic presentation debris cannot alter flight collisions, missions, or camera collision casts.
            foreach(Collider collider in fragment.GetComponentsInChildren<Collider>())Destroy(collider);
            var motion=fragment.AddComponent<CosmeticAirframeDebris>();
            motion.Velocity=Vector3.ClampMagnitude(impact.IncomingVelocity*.28f+UnityEngine.Random.onUnitSphere*4+impact.Normal*3,16);
            motion.AngularVelocity=UnityEngine.Random.insideUnitSphere*170;
            debris.Add(fragment);
        }

        void HideRenderers(Renderer[] renderers)
        {
            foreach(Renderer renderer in renderers)
            {
                if(hiddenRenderers.Contains(renderer))continue;
                hiddenRenderers.Add(renderer);rendererStates.Add(renderer.enabled);renderer.enabled=false;
            }
        }

        void ScorchAirframe()
        {
            if(scorchedRenderers.Count>0)return;
            foreach(MeshRenderer renderer in Aircraft.GetComponentsInChildren<MeshRenderer>())
            {
                var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
                scorchedRenderers.Add(renderer);originalProperties.Add(original);
                var scorch=new MaterialPropertyBlock();renderer.GetPropertyBlock(scorch);
                scorch.SetColor("_BaseColor",new Color(.11f,.09f,.067f,1));scorch.SetFloat("_Smoothness",.025f);scorch.SetFloat("_Metallic",.08f);
                renderer.SetPropertyBlock(scorch);
            }
        }

        void PlayImpact(float speed,float pitch)
        {
            if(impactAudio==null)return;
            impactAudio.pitch=pitch;impactAudio.PlayOneShot(impactClip,(Audio!=null?Audio.Volume:.65f)*Mathf.Clamp01(speed/18));
        }

        public void ResetEffects()
        {
            foreach(ParticleSystem system in new[]{dust,smoke,sparks,flame,splash,washStreak})if(system!=null)system.Clear(true);
            foreach(GameObject part in debris)if(part!=null)Destroy(part);debris.Clear();
            for(int i=0;i<hiddenRenderers.Count;i++)if(hiddenRenderers[i]!=null)hiddenRenderers[i].enabled=rendererStates[i];
            hiddenRenderers.Clear();rendererStates.Clear();
            for(int i=0;i<scorchedRenderers.Count;i++)if(scorchedRenderers[i]!=null)scorchedRenderers[i].SetPropertyBlock(originalProperties[i]);
            scorchedRenderers.Clear();originalProperties.Clear();
            HasExploded=smoking=waterEntered=false;fireUntil=flashUntil=nextImpactAt=0;
            if(flash!=null)flash.enabled=false;if(impactAudio!=null)impactAudio.Stop();cameraShake?.ResetShake();
        }

        ParticleSystem CreateParticles(string name,int capacity,float gravity,bool streak=false)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.layer=2;
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.maxParticles=capacity;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=gravity;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var fade=system.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(streak?1:0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.65f,.65f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,streak?1:.55f,1,streak?.2f:2.3f));
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortMode=ParticleSystemSortMode.Distance;
            if(streak){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.12f;}
            system.Play();return system;
        }

        static void Emit(ParticleSystem system,Vector3 position,Vector3 velocity,Color color,float size,float life)
        {
            if(system==null)return;
            var particle=new ParticleSystem.EmitParams{position=position,velocity=velocity,startColor=color,startSize=size,startLifetime=life,rotation=UnityEngine.Random.Range(0,360)};
            system.Emit(particle,1);
        }
        static int Count(ParticleSystem system)=>system!=null?system.particleCount:0;

        static Texture2D MakeParticleTexture()
        {
            const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Original radial smoke sprite",wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1;
                float noise=Mathf.PerlinNoise(4+dx*3.4f,7+dy*3.4f),detail=Mathf.PerlinNoise(20+dx*8,15+dy*8);
                float radius=Mathf.Sqrt(dx*dx+dy*dy)/(.9f+.22f*noise);
                float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-radius)*2.7f))*(.62f+.38f*noise)*(.84f+.16f*detail);
                pixels[y*n+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        static AudioClip MakeImpactClip()
        {
            const int rate=22050;var samples=new float[rate];var rng=new System.Random(4407);
            for(int i=0;i<samples.Length;i++){double t=i/(double)rate;samples[i]=(float)(((rng.NextDouble()*2-1)*Math.Exp(-t*11)*.6+Math.Sin(t*2*Math.PI*(67-t*30))*Math.Exp(-t*7)*.38));}
            var clip=AudioClip.Create("Original airframe impact",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void OnDestroy()
        {
            if(Aircraft!=null){Aircraft.Impact-=PresentImpact;Aircraft.CrashedEvent-=OnCrash;Aircraft.ResetPerformed-=ResetEffects;}
            ResetEffects();if(material!=null)Destroy(material);if(fireMaterial!=null)Destroy(fireMaterial);if(particleTexture!=null)Destroy(particleTexture);if(impactClip!=null)Destroy(impactClip);
        }
    }

    /// <summary>Simple visual debris bounce; no Rigidbody or Collider touches the aircraft simulation.</summary>
    public sealed class CosmeticAirframeDebris : MonoBehaviour
    {
        public Vector3 Velocity,AngularVelocity;
        float age;
        void Update()
        {
            float dt=Time.deltaTime;if(dt<=0)return;age+=dt;if(age>12){Destroy(gameObject);return;}
            Velocity+=Physics.gravity*dt;
            Vector3 travel=Velocity*dt;
            if(travel.sqrMagnitude>.00001f&&Physics.Raycast(transform.position,travel.normalized,out RaycastHit hit,travel.magnitude+.15f,~((1<<8)|(1<<2)),QueryTriggerInteraction.Ignore))
            {
                transform.position=hit.point+hit.normal*.15f;Velocity=Vector3.Reflect(Velocity,hit.normal)*.28f;AngularVelocity*=.4f;
            }
            else transform.position+=travel;
            transform.Rotate(AngularVelocity*dt,Space.World);
        }
    }
}
