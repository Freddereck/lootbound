using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MythicAscension.Content
{
    /// Создание перекрашенных копий спрайтов игры (текстуры игры не читаемые — копируем через RenderTexture).
    public static class SpriteUtil
    {
        static readonly Dictionary<string, Sprite> Cache = new();

        static void Keep(UnityEngine.Object o)
        {
            o.hideFlags |= HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
        }

        /// Читаемая копия области спрайта.
        static Texture2D CopyRegion(Sprite src)
        {
            var tex = src.texture;
            var r = src.textureRect;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            UnityEngine.Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var outTex = new Texture2D(Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height), TextureFormat.RGBA32, false);
            outTex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
            outTex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            outTex.filterMode = tex.filterMode;
            outTex.wrapMode = TextureWrapMode.Clamp;
            return outTex;
        }

        /// Сдвиг тона (градусы) + множитель насыщенности. Для перекраски золота Divine в красно-золотое.
        public static Sprite HueShift(Sprite src, float hueShiftDeg, float satMul = 1.15f, float valMul = 1f)
        {
            if (src == null) return null;
            string key = $"{src.GetInstanceID()}_{hueShiftDeg}_{satMul}_{valMul}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            try
            {
                var tex = CopyRegion(src);
                var px = tex.GetPixels32();
                float shift = hueShiftDeg / 360f;
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a == 0) continue;
                    Color.RGBToHSV(new Color(c.r / 255f, c.g / 255f, c.b / 255f), out var h, out var s, out var v);
                    if (s < 0.08f) continue; // серые/белые пиксели не трогаем
                    h = (h + shift + 1f) % 1f;
                    s = Mathf.Clamp01(s * satMul);
                    v = Mathf.Clamp01(v * valMul);
                    var n = Color.HSVToRGB(h, s, v);
                    px[i] = new Color32((byte)(n.r * 255), (byte)(n.g * 255), (byte)(n.b * 255), c.a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                Plugin.V($"[Sprite] {src.name}: rect={src.rect} texRect={src.textureRect} tex={src.texture.width}x{src.texture.height} packed={src.packed}");
                var sprite = Make(tex, src);
                sprite.name = src.name + "_Mythic";
                Cache[key] = sprite;
                return sprite;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning($"[Sprite] Не удалось перекрасить {src.name}: {e.Message}");
                return src;
            }
        }

        static Sprite Make(Texture2D tex, Sprite like)
        {
            var rect = like.rect;
            var pivot = new Vector2(like.pivot.x / rect.width, like.pivot.y / rect.height);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, like.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, like.border);
            Keep(tex);
            Keep(s);
            return s;
        }

        /// Своя иконка из plugins/MythicAscension/icons/<ID>.png (размер подгоняется под оригинальную иконку).
        public static Sprite LoadIconOverride(string itemId, Sprite like)
        {
            var path = Path.Combine(Plugin.PluginDir, "icons", itemId + ".png");
            if (!File.Exists(path) || like == null) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) return null;
                tex.filterMode = like.texture.filterMode;
                float ppu = like.pixelsPerUnit * (tex.width / like.rect.width);
                var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                s.name = itemId;
                Keep(tex);
                Keep(s);
                return s;
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning($"[Sprite] Иконка {path}: {e.Message}");
                return null;
            }
        }
    }
}
