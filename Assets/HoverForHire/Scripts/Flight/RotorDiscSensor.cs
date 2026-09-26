using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Convex trigger volume swept by a spinning rotor. Physics reports any overlap with terrain, structures or
    /// trees, and the controller turns it into a blade strike while the rotor is turning. The aircraft's own
    /// colliders never generate trigger events against it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotorDiscSensor : MonoBehaviour
    {
        public HelicopterController Owner;
        public CrashCause Cause = CrashCause.RotorStrike;

        private void OnTriggerEnter(Collider other) => Report(other);
        private void OnTriggerStay(Collider other) => Report(other);

        private void Report(Collider other)
        {
            if (Owner != null) Owner.ReportRotorContact(Cause, other);
        }

        /// <summary>Flat convex cylinder in local space: the disc lies in XZ, or in YZ for a tail rotor.</summary>
        public static Mesh DiscMesh(string name, Vector3 center, float radius, float thickness, bool tail)
        {
            const int segments = 24;
            var vertices = new Vector3[segments * 2];
            float half = Mathf.Max(0.02f, thickness * 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, s = Mathf.Sin(a) * radius, c = Mathf.Cos(a) * radius;
                vertices[i] = center + (tail ? new Vector3(-half, s, c) : new Vector3(s, -half, c));
                vertices[i + segments] = center + (tail ? new Vector3(half, s, c) : new Vector3(s, half, c));
            }
            var triangles = new int[(segments - 2) * 6 + segments * 6];
            int t = 0;
            for (int i = 1; i < segments - 1; i++)
            {
                triangles[t++] = 0; triangles[t++] = i; triangles[t++] = i + 1;
                triangles[t++] = segments; triangles[t++] = segments + i + 1; triangles[t++] = segments + i;
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles[t++] = i; triangles[t++] = segments + i; triangles[t++] = next;
                triangles[t++] = next; triangles[t++] = segments + i; triangles[t++] = segments + next;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
