using UnityEngine;

namespace HoverForHire
{
    /// <summary>Runs after the ordinary camera pose; never writes the aircraft transform.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ImpactCameraShake : MonoBehaviour
    {
        float strength,phase;
        public void AddImpact(float amount)=>strength=Mathf.Clamp01(Mathf.Max(strength,amount));
        public void ResetShake(){strength=phase=0;}
        void LateUpdate()
        {
            if(Time.deltaTime<=0||strength<=.001f)return;
            phase+=Time.deltaTime*37;
            Vector3 movement=new Vector3(Mathf.Sin(phase*1.31f),Mathf.Sin(phase*1.79f),0)*strength*.09f;
            transform.position+=transform.TransformDirection(movement);
            transform.rotation*=Quaternion.Euler(Mathf.Sin(phase)*strength*.9f,0,Mathf.Sin(phase*1.17f)*strength*.65f);
            strength=Mathf.MoveTowards(strength,0,Time.deltaTime*1.65f);
        }
    }
}
