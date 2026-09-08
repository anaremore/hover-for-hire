using UnityEngine;

namespace HoverForHire
{
    public sealed class HelicopterVisual : MonoBehaviour
    {
        public Transform CockpitMount { get; private set; }
        Transform rotor, tailRotor;
        HelicopterController aircraft;
        public void Build(HelicopterController controller)
        {
            aircraft=controller;
            Part("Utility cabin",PrimitiveType.Sphere,new Vector3(0,.1f,.25f),new Vector3(2.35f,2.15f,3.65f),IslandWorld.Signal);
            Part("Forward canopy",PrimitiveType.Sphere,new Vector3(0,.35f,1.2f),new Vector3(2.15f,1.65f,2.1f),IslandWorld.Glass);
            Part("Canopy center frame",PrimitiveType.Cube,new Vector3(0,.7f,1.9f),new Vector3(.08f,1.1f,.1f),IslandWorld.White);
            Part("Engine housing",PrimitiveType.Capsule,new Vector3(0,1.12f,-.6f),new Vector3(1.25f,1.35f,1.9f),IslandWorld.White).transform.localRotation=Quaternion.Euler(90,0,0);
            Part("Tail boom",PrimitiveType.Capsule,new Vector3(0,.35f,-3.25f),new Vector3(.5f,2.45f,.5f),IslandWorld.Signal).transform.localRotation=Quaternion.Euler(90,0,0);
            Part("Tail fin",PrimitiveType.Cube,new Vector3(0,1,-5.15f),new Vector3(.12f,1.8f,1),IslandWorld.White);
            Part("Tailplane",PrimitiveType.Cube,new Vector3(0,.3f,-4.2f),new Vector3(2.4f,.12f,.7f),IslandWorld.Signal);
            foreach(float side in new[]{-1f,1f})
            {
                Part("Skid",PrimitiveType.Capsule,new Vector3(side,-1.36f,.15f),new Vector3(.18f,2,.18f),IslandWorld.Metal).transform.localRotation=Quaternion.Euler(90,0,0);
                foreach(float z in new[]{-.8f,.9f}) Part("Skid strut",PrimitiveType.Cube,new Vector3(side*.85f,-.88f,z),new Vector3(.13f,.95f,.13f),IslandWorld.White).transform.localRotation=Quaternion.Euler(0,0,side*18);
                Part("Door handle",PrimitiveType.Cube,new Vector3(side*1.14f,.1f,-.3f),new Vector3(.1f,.09f,.32f),IslandWorld.White);
            }
            Part("Rotor mast",PrimitiveType.Cylinder,new Vector3(0,1.7f,0),new Vector3(.16f,.5f,.16f),IslandWorld.Metal);
            rotor=new GameObject("Main rotor (visual only)").transform;rotor.SetParent(transform,false);rotor.localPosition=new Vector3(0,2.15f,0);
            IslandWorld.Piece("Rotor blade",PrimitiveType.Cube,Vector3.zero,new Vector3(10,.06f,.23f),IslandWorld.Metal,false,rotor);
            IslandWorld.Piece("Rotor tip",PrimitiveType.Cube,new Vector3(4.7f,0,0),new Vector3(.6f,.07f,.24f),IslandWorld.White,false,rotor);
            IslandWorld.Piece("Rotor tip",PrimitiveType.Cube,new Vector3(-4.7f,0,0),new Vector3(.6f,.07f,.24f),IslandWorld.White,false,rotor);
            tailRotor=new GameObject("Tail rotor (visual only)").transform;tailRotor.SetParent(transform,false);tailRotor.localPosition=new Vector3(.25f,1,-5.2f);
            IslandWorld.Piece("Tail rotor blade",PrimitiveType.Cube,Vector3.zero,new Vector3(.08f,1.7f,.12f),IslandWorld.Metal,false,tailRotor);
            CockpitMount=new GameObject("Pilot eye").transform;CockpitMount.SetParent(transform,false);CockpitMount.localPosition=new Vector3(.4f,.58f,1.85f);
            foreach(var child in GetComponentsInChildren<Transform>())child.gameObject.layer=8;
            // Only these compound hull/skid colliders contact the world, never decorative rotor pieces.
            var hull=gameObject.AddComponent<BoxCollider>();hull.center=new Vector3(0,.05f,0);hull.size=new Vector3(2,1.7f,3.1f);
            foreach(float side in new[]{-1f,1f}) { var c=gameObject.AddComponent<BoxCollider>();c.center=new Vector3(side,-1.38f,.15f);c.size=new Vector3(.2f,.24f,3.9f); }
        }
        GameObject Part(string name,PrimitiveType type,Vector3 p,Vector3 s,Material m) => IslandWorld.Piece(name,type,p,s,m,false,transform);
        void Update()
        {
            if(rotor==null)return;float rpm=aircraft.RotorRpm;
            rotor.Rotate(Vector3.up,rpm*6*Time.deltaTime);tailRotor.Rotate(Vector3.right,rpm*18*Time.deltaTime);
        }
    }
}
