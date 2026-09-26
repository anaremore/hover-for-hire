using UnityEngine;

namespace HoverForHire
{
    /// <summary>A paint scheme: the enamel colors of the airframe. Paint only; it never changes the aircraft.</summary>
    public struct Livery
    {
        public string Name;
        public int Price;
        /// <summary>Main body enamel.</summary>
        public Color Primary;
        /// <summary>Stripes and secondary panels.</summary>
        public Color Secondary;
        /// <summary>Caution markings and small details.</summary>
        public Color Accent;
    }

    public static class Liveries
    {
        public static readonly Livery[] All =
        {
            new Livery { Name = "Meridian orange", Price = 0, Primary = new Color(.98f, .25f, .035f), Secondary = new Color(.92f, .89f, .76f), Accent = new Color(1f, .72f, .055f) },
            new Livery { Name = "Coastguard", Price = 600, Primary = new Color(.9f, .9f, .87f), Secondary = new Color(.82f, .09f, .06f), Accent = new Color(.08f, .09f, .1f) },
            new Livery { Name = "Forest service", Price = 900, Primary = new Color(.09f, .27f, .15f), Secondary = new Color(.88f, .84f, .68f), Accent = new Color(1f, .72f, .055f) },
            new Livery { Name = "Midnight", Price = 1500, Primary = new Color(.045f, .07f, .16f), Secondary = new Color(.78f, .62f, .24f), Accent = new Color(.78f, .62f, .24f) },
        };

        public static Livery Get(int index) => All[Mathf.Clamp(index, 0, All.Length - 1)];
    }
}
