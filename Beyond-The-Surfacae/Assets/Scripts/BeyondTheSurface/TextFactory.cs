using TMPro;
using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>Creates world-space TextMeshPro text (auto-sized to a rectangle in metres) and simple panels.</summary>
    public static class TextFactory
    {
        public static Material NewSpriteMaterial(Color c)
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color = c;
            return m;
        }

        /// <summary>
        /// Readable from the side the camera looks from when rotation is identity and the parent faces the camera.
        /// To face +Z (player standing on the +Z side) use rotation Euler(0,180,0).
        /// </summary>
        public static TextMeshPro Create(string name, string text, Transform parent, Vector3 pos, Quaternion rot,
            Vector2 sizeMeters, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center,
            bool worldSpace = false, float maxFont = 10f)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            if (worldSpace) { go.transform.position = pos; go.transform.rotation = rot; }
            else { go.transform.localPosition = pos; go.transform.localRotation = rot; }

            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = sizeMeters;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.01f;
            t.fontSizeMax = maxFont;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        public static GameObject Panel(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 sizeMeters,
            Color color, bool worldSpace = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            if (parent != null) go.transform.SetParent(parent, false);
            if (worldSpace) { go.transform.position = pos; go.transform.rotation = rot; }
            else { go.transform.localPosition = pos; go.transform.localRotation = rot; }
            go.transform.localScale = new Vector3(sizeMeters.x, sizeMeters.y, 1f);
            go.GetComponent<Renderer>().sharedMaterial = NewSpriteMaterial(color);
            return go;
        }
    }
}
