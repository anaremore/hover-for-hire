using UnityEngine;

namespace HoverForHire
{
    /// <summary>Small IMGUI drawing vocabulary shared by the flight instruments and flight desk.</summary>
    internal static class FlightHudGraphics
    {
        internal static readonly Color Phosphor = new Color(.83f, .96f, .70f, .92f);
        internal static readonly Color Muted = new Color(.69f, .76f, .66f, .83f);
        internal static readonly Color Amber = new Color(1f, .76f, .35f);
        internal static readonly Color Paper = new Color(.94f, .96f, .89f);

        internal static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        internal static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        internal static void Line(Vector2 from, Vector2 to, Color color, float width = 1.2f)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .001f) return;
            Matrix4x4 previous = GUI.matrix;
            // Compose in logical HUD coordinates before the outer screen scale. Unity's
            // RotateAroundPivot unclips the pivot and can apply that scale a second time.
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(from.x, from.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            Fill(new Rect(0, -width * .5f, delta.magnitude, width), color);
            GUI.matrix = previous;
        }

        internal static Matrix4x4 RotationAround(Vector2 pivot, float angle)
        {
            return Matrix4x4.Translate(new Vector3(pivot.x, pivot.y, 0)) *
                Matrix4x4.Rotate(Quaternion.Euler(0, 0, angle)) *
                Matrix4x4.Translate(new Vector3(-pivot.x, -pivot.y, 0));
        }

        internal static void Frame(Rect rect, Color color, float width = 1)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        internal static void Circle(Vector2 center, float radius, Color color, int segments = 40, float width = 1.2f)
        {
            Vector2 previous = center + Vector2.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(previous, point, color, width);
                previous = point;
            }
        }

        internal static void Diamond(Vector2 center, float radius, Color color)
        {
            Vector2 a = center + Vector2.up * radius, b = center + Vector2.right * radius;
            Vector2 c = center + Vector2.down * radius, d = center + Vector2.left * radius;
            Line(a, b, color, 1.7f); Line(b, c, color, 1.7f);
            Line(c, d, color, 1.7f); Line(d, a, color, 1.7f);
        }

        internal static void Aircraft(Vector2 center, float heading, Color color, float scale = 1)
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * RotationAround(center, heading);
            Line(center + new Vector2(0, -10) * scale, center + new Vector2(-6, 6) * scale, color, 1.7f);
            Line(center + new Vector2(-6, 6) * scale, center + new Vector2(0, 3) * scale, color, 1.7f);
            Line(center + new Vector2(0, 3) * scale, center + new Vector2(6, 6) * scale, color, 1.7f);
            Line(center + new Vector2(6, 6) * scale, center + new Vector2(0, -10) * scale, color, 1.7f);
            GUI.matrix = previous;
        }
    }
}
