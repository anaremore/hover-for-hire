using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Original synthesized loops. No external audio assets or runtime network access.</summary>
    public sealed class FlightAudio : MonoBehaviour
    {
        public HelicopterController Aircraft;
        AudioSource rotor,wind,feedback; AudioClip touchdown,chime;
        public float Volume=0.65f;
        void Start()
        {
            rotor=Source(true);wind=Source(true);feedback=Source(false);
            rotor.clip=Tone("Governed four-beat rotor",2,(t,n)=> (float)((Math.Sin(t*2*Math.PI*32)*.34+Math.Sin(t*2*Math.PI*64)*.1+n*.16)*(.6+.4*Math.Cos(t*2*Math.PI*16))));
            wind.clip=Tone("Wind",3,(t,n)=>n*.16f);touchdown=Tone("Skid touchdown",.28,(t,n)=>n*(float)Math.Exp(-t*18)*.7f);
            chime=Tone("Service completed",.42,(t,n)=>(float)(Math.Sin(t*2*Math.PI*(t<.2?660:880))*Math.Sin(Math.PI*t/.42)*.25));
            rotor.Play();wind.Play();Aircraft.Touchdown+=OnTouchdown;
        }
        AudioSource Source(bool loop){var a=gameObject.AddComponent<AudioSource>();a.loop=loop;a.spatialBlend=0;a.playOnAwake=false;return a;}
        static AudioClip Tone(string name,double length,Func<double,float,float> sample)
        {
            const int rate=22050;var data=new float[(int)(length*rate)];var rng=new System.Random(17);
            for(int i=0;i<data.Length;i++)data[i]=sample(i/(double)rate,(float)rng.NextDouble()*2-1);
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void Update(){if(rotor==null)return;rotor.volume=Volume*(Aircraft.Crashed?.07f:.3f+.22f*Aircraft.RawCommand.Collective);rotor.pitch=Aircraft.Crashed?.45f:.95f+.08f*Aircraft.RawCommand.Collective;wind.volume=Volume*Mathf.Clamp01(Aircraft.GroundSpeed/45)*.8f;}
        void OnTouchdown(float speed){feedback.PlayOneShot(touchdown,Volume*Mathf.Clamp01(speed/3+.15f));}
        public void ServiceChime(){if(feedback!=null)feedback.PlayOneShot(chime,Volume);}
        void OnDestroy(){if(Aircraft!=null)Aircraft.Touchdown-=OnTouchdown;}
    }
}
