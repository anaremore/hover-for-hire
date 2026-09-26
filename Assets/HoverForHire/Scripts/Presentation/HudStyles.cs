using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire
{
    /// <summary>Fonts, control skins and the small drawing helpers every HUD view shares. Built lazily inside OnGUI.</summary>
    public sealed class HudStyles : IDisposable
    {
        public GUIStyle Title, Label, Small, Value, Button, Panel, TabButton, Toggle, SliderTrack, SliderThumb;
        public GUIStyle HudLabel, HudSmall, HudValue, HudCenter, HudRight, HudValueRight, HudValueCenter;
        private Texture2D buttonIdle, buttonHover, buttonActive, toggleOff, toggleOn;

        public bool Ready => Title != null;

        /// <summary>Create the styles on first use; GUI.skin is only valid inside OnGUI.</summary>
        public void Build()
        {
            if (Ready) return;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = FlightHudGraphics.Paper }, padding = new RectOffset(0, 0, 2, 2) };
            Small = new GUIStyle(Label) { fontSize = 13 };
            Title = new GUIStyle(Label) { fontSize = 29, fontStyle = FontStyle.Bold };
            Value = new GUIStyle(Label) { fontSize = 26, fontStyle = FontStyle.Bold };
            HudLabel = new GUIStyle(Label) { fontSize = 15, wordWrap = false, normal = { textColor = FlightHudGraphics.Phosphor } };
            HudSmall = new GUIStyle(HudLabel) { fontSize = 13 };
            HudValue = new GUIStyle(HudLabel) { fontSize = 27, fontStyle = FontStyle.Bold };
            HudValueRight = new GUIStyle(HudValue) { alignment = TextAnchor.UpperRight };
            HudValueCenter = new GUIStyle(HudValue) { alignment = TextAnchor.MiddleCenter };
            HudCenter = new GUIStyle(HudLabel) { alignment = TextAnchor.MiddleCenter };
            HudRight = new GUIStyle(HudSmall) { alignment = TextAnchor.UpperRight };

            buttonIdle = FlightHudGraphics.Solid(new Color(.18f, .21f, .18f));
            buttonHover = FlightHudGraphics.Solid(new Color(.28f, .34f, .25f));
            buttonActive = FlightHudGraphics.Solid(new Color(.43f, .52f, .34f));
            toggleOff = FlightHudGraphics.Solid(new Color(.15f, .18f, .15f));
            toggleOn = FlightHudGraphics.Solid(new Color(.29f, .37f, .23f));
            Button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 16, 10, 10),
                margin = new RectOffset(3, 3, 4, 4), border = new RectOffset(), fixedHeight = 44
            };
            SetControlColors(Button, buttonIdle, buttonHover, buttonActive);
            TabButton = new GUIStyle(Button) { alignment = TextAnchor.MiddleCenter, fixedHeight = 43, fontSize = 14, padding = new RectOffset(6, 6, 10, 10) };
            Toggle = new GUIStyle(Button) { fontSize = 14, fixedHeight = 36 };
            SetControlColors(Toggle, toggleOff, buttonHover, toggleOn);
            SliderTrack = new GUIStyle(GUI.skin.horizontalSlider)
                { fixedHeight = 7, margin = new RectOffset(5, 5, 9, 9), border = new RectOffset(), normal = { background = buttonIdle } };
            SliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                fixedWidth = 16, fixedHeight = 19, border = new RectOffset(),
                normal = { background = buttonActive }, hover = { background = buttonHover }, active = { background = buttonActive }
            };
            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 15, 15), normal = { background = buttonIdle }, border = new RectOffset() };
        }

        private static void SetControlColors(GUIStyle style, Texture2D idle, Texture2D hover, Texture2D active)
        {
            style.normal.background = idle; style.hover.background = hover; style.active.background = active; style.focused.background = hover;
            style.onNormal.background = active; style.onHover.background = active; style.onActive.background = active; style.onFocused.background = active;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = FlightHudGraphics.Paper;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = FlightHudGraphics.Paper;
        }

        /// <summary>Translucent dark panel.</summary>
        public void Box(Rect rect, float alpha) => FlightHudGraphics.Fill(rect, new Color(.045f, .065f, .053f, alpha));

        public void Bar(Rect rect, float progress, Color color)
        {
            Box(rect, .8f);
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>HUD text on a dark panel, where a shadow would not show.</summary>
        public void Plain(Rect rect, string text, GUIStyle style, Color? tint = null)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = tint ?? FlightHudGraphics.Phosphor;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }

        /// <summary>HUD text with a one-pixel shadow so it reads over bright scenery.</summary>
        public void Text(Rect rect, string text, GUIStyle style, Color? tint = null)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = new Color(.015f, .025f, .018f, .85f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            style.normal.textColor = tint ?? FlightHudGraphics.Phosphor;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in new[] { buttonIdle, buttonHover, buttonActive, toggleOff, toggleOn })
                if (texture != null) Object.Destroy(texture);
        }
    }
}
