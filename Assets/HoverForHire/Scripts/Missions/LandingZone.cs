using UnityEngine;

namespace HoverForHire
{
    public sealed class LandingZone : MonoBehaviour
    {
        public string Id;
        public string DisplayName;
        [Min(2f)] public float Radius = 9f;

        public ZoneDefinition Definition => new ZoneDefinition
        {
            Id = string.IsNullOrEmpty(Id) ? gameObject.name : Id,
            Name = string.IsNullOrEmpty(DisplayName) ? gameObject.name : DisplayName,
            X = transform.position.x, Y = transform.position.y, Z = transform.position.z, Radius = Radius
        };

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.7f, new Vector3(Radius * 2f, 3f, Radius * 2f));
        }
    }
}
