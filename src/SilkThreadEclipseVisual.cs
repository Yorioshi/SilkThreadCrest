using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilkThreadCrest
{
    internal static class SilkThreadEclipseVisual
    {
        private const int Columns = 4, Rows = 2;
        private const string ResourceName = "SilkThreadCrest.Skins.SilkEclipse.Texture2D.silk_eclipse_sheet.png";
        private static readonly Dictionary<Renderer, bool> RenderingBackups = new Dictionary<Renderer, bool>();
        private static HeroController hero;
        private static tk2dSpriteAnimator nativeAnimator;
        private static tk2dBaseSprite nativeSprite;
        private static Renderer bodyRenderer;
        private static GameObject visualObject;
        private static SpriteRenderer visualRenderer;
        private static Texture2D spriteSheet;
        private static Sprite[] frames;
        private static float[] bodyHeights;
        private static float referenceWorldHeight;
        private static float referenceWorldWidth;
        private static Vector3 referenceLocalCenter;
        private static string previousClip;
        private static int previousFrame = -2;
        private static float nextDiagnostic;

        internal static bool Apply(string pluginPath)
        {
            HeroController current = HeroController.instance;
            if (!current)
                return false;
            Clear();
            hero = current;
            // This is the actual body animator, not a random effect animator under HeroController.
            nativeAnimator = hero.AnimCtrl ? hero.AnimCtrl.animator : null;
            if (!nativeAnimator)
            {
                SilkThreadCrestPlugin.LogWarning("Silk Eclipse: native body animator unavailable; original art preserved.");
                return false;
            }
            nativeSprite = nativeAnimator.Sprite;
            bodyRenderer = nativeSprite ? nativeSprite.GetComponent<Renderer>() : null;
            if (!nativeSprite || !bodyRenderer)
                return false;
            CaptureNativeReference();
            RenderingBackups[bodyRenderer] = bodyRenderer.forceRenderingOff;
            string path = Path.Combine(pluginPath, "Skins", "SilkEclipse", "Texture2D", "silk_eclipse_sheet.png");
            if (!LoadSheet(path))
            {
                Clear();
                return false;
            }
            visualObject = new GameObject("SilkThread // Silk Eclipse Skin");
            visualObject.hideFlags = HideFlags.DontSave;
            visualObject.layer = bodyRenderer.gameObject.layer;
            visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = frames[0];
            visualRenderer.color = Color.white;
            Tick();
            SilkThreadCrestPlugin.LogInfo("Silk Eclipse " + SilkThreadCrestPlugin.PluginVersion +
                ": native animator=" + nativeAnimator.name + ", body=" + bodyRenderer.name + "; synced poses, native fallback enabled.");
            return true;
        }

        internal static void Tick()
        {
            if (!hero || !nativeAnimator || !bodyRenderer || !visualRenderer || frames == null)
                return;
            tk2dBaseSprite currentSprite = nativeAnimator.Sprite;
            Renderer currentRenderer = currentSprite ? currentSprite.GetComponent<Renderer>() : null;
            if (currentRenderer && currentRenderer != bodyRenderer)
            {
                RestoreRendering();
                RenderingBackups.Clear();
                nativeSprite = currentSprite;
                bodyRenderer = currentRenderer;
                RenderingBackups[bodyRenderer] = bodyRenderer.forceRenderingOff;
            }
            if (currentSprite)
                nativeSprite = currentSprite;
            var clip = nativeAnimator.CurrentClip;
            string name = clip != null ? clip.name : string.Empty;
            int nativeFrame = clip != null && clip.frames != null && clip.frames.Length > 0
                ? nativeAnimator.CurrentFrame : 0;
            int count = clip != null && clip.frames != null ? clip.frames.Length : 0;
            int pose = SilkThreadEclipsePose.Select(name, nativeFrame, count);
            var state = hero.cState;
            if (state != null && (name.Equals("Airborne", StringComparison.OrdinalIgnoreCase) || name.Equals("Float", StringComparison.OrdinalIgnoreCase)))
                pose = state.falling ? 5 : state.jumping ? 4 : -1;
            // Eight sample poses cannot represent every special action. Show real native animation instead.
            if (state != null && (state.dead || state.hazardDeath || state.hazardRespawning ||
                state.isInCutsceneMovement || state.wallSliding || state.wallClinging || state.mantling ||
                state.dashing || state.backDashing || state.isSprinting || state.isToolThrowing ||
                state.downSpiking || state.downSpikeAntic || state.upAttacking || state.downAttacking ||
                state.casting && !state.focusing && !state.isBinding))
                pose = -1;
            bool nativeVisible = bodyRenderer.enabled && bodyRenderer.gameObject.activeInHierarchy;
            bool useSkin = pose >= 0 && pose < frames.Length && nativeVisible && frames[pose];
            if (!useSkin)
            {
                visualRenderer.enabled = false;
                RestoreRendering();
            }
            else
            {
                visualRenderer.enabled = true;
                visualRenderer.sprite = frames[pose];
                bodyRenderer.forceRenderingOff = true;
                // Fixed PPU calibration from native Idle, never from attack/weapon/effect bounds.
                float scale = referenceWorldHeight > 0.0001f && bodyHeights[0] > 0.0001f
                    ? referenceWorldHeight / bodyHeights[0] : 1f;
                float referenceArtWidth = frames[0].rect.width / 170f * 0.68f;
                float scaleX = referenceWorldWidth > 0.0001f && referenceArtWidth > 0.0001f
                    ? referenceWorldWidth / referenceArtWidth : scale;
                visualObject.transform.localScale = new Vector3(scaleX, scale, 1f);
                visualObject.transform.rotation = Quaternion.identity;
                // The per-pose pivot is the center of the body, not the center of a weapon/effect rectangle.
                Vector3 center = nativeSprite.transform.TransformPoint(referenceLocalCenter);
                visualObject.transform.position = new Vector3(center.x, center.y, bodyRenderer.transform.position.z - 0.01f);
                visualObject.layer = bodyRenderer.gameObject.layer;
                visualRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                visualRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
                bool facingRight = state != null ? state.facingRight : nativeSprite.FlipX;
                // Normalize both walking frames to the same left-facing orientation.
                visualRenderer.flipX = pose == 3 ? !facingRight : facingRight;
                visualRenderer.flipY = nativeSprite.FlipY;
                visualRenderer.color = nativeSprite.color;
            }
            if ((previousClip != name || previousFrame != pose) && Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 0.5f;
                SilkThreadCrestPlugin.LogInfo("Silk Eclipse sync: clip=" + name + "; native frame=" + nativeFrame +
                    "; pose=" + pose + "; mode=" + (useSkin ? "skin" : "native fallback") +
                    "; idle reference height=" + referenceWorldHeight.ToString("0.###"));
            }
            previousClip = name;
            previousFrame = pose;
        }

        private static void RestoreRendering()
        {
            foreach (var pair in RenderingBackups)
                if (pair.Key)
                    pair.Key.forceRenderingOff = pair.Value;
        }

        internal static void Clear()
        {
            RestoreRendering();
            RenderingBackups.Clear();
            if (visualObject) UnityEngine.Object.Destroy(visualObject);
            if (frames != null)
                foreach (Sprite frame in frames)
                    if (frame) UnityEngine.Object.Destroy(frame);
            if (spriteSheet) UnityEngine.Object.Destroy(spriteSheet);
            hero = null;
            nativeAnimator = null;
            nativeSprite = null;
            bodyRenderer = null;
            visualObject = null;
            visualRenderer = null;
            spriteSheet = null;
            frames = null;
            bodyHeights = null;
            referenceWorldHeight = 0f;
            referenceWorldWidth = 0f;
            referenceLocalCenter = Vector3.zero;
            previousClip = null;
            previousFrame = -2;
            nextDiagnostic = 0f;
        }

        private static void CaptureNativeReference()
        {
            tk2dSpriteDefinition definition = nativeSprite.CurrentSprite;
            var idle = nativeAnimator.GetClipByName("Idle");
            if (idle != null && idle.frames != null && idle.frames.Length > 0)
            {
                var frame = idle.frames[0];
                var collection = frame.spriteCollection ? frame.spriteCollection.inst : null;
                if (collection && collection.spriteDefinitions != null && frame.spriteId >= 0 && frame.spriteId < collection.spriteDefinitions.Length)
                    definition = collection.spriteDefinitions[frame.spriteId];
            }
            if (definition != null && definition.positions != null && definition.positions.Length > 0)
            {
                Vector3 min = definition.positions[0], max = min;
                foreach (Vector3 position in definition.positions)
                {
                    min = Vector3.Min(min, position);
                    max = Vector3.Max(max, position);
                }
                referenceLocalCenter = (min + max) * 0.5f;
                referenceWorldHeight = Math.Abs(nativeSprite.transform.TransformVector(new Vector3(0f, max.y - min.y, 0f)).y);
                referenceWorldWidth = Math.Abs(nativeSprite.transform.TransformVector(new Vector3(max.x - min.x, 0f, 0f)).x);
            }
            if (referenceWorldHeight <= 0.0001f)
            {
                referenceWorldHeight = bodyRenderer.bounds.size.y;
                referenceWorldWidth = bodyRenderer.bounds.size.x;
                referenceLocalCenter = nativeSprite.transform.InverseTransformPoint(bodyRenderer.bounds.center);
            }
        }

        private static bool LoadSheet(string path)
        {
            try
            {
                bool embedded;
                byte[] bytes = SilkThreadSkinAssetReader.Read(path, typeof(SilkThreadEclipseVisual).Assembly, ResourceName, out embedded);
                SilkThreadCrestPlugin.LogInfo(embedded ? "Silk Eclipse loading embedded sheet from DLL." : "Silk Eclipse loading external sheet: " + path);
                Type imageType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                MethodInfo load = imageType?.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(Texture2D), typeof(byte[]) }, null);
                if (load == null)
                    throw new InvalidOperationException("ImageConversion.LoadImage unavailable");
                spriteSheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!(bool)load.Invoke(null, new object[] { spriteSheet, bytes }))
                    throw new InvalidDataException("PNG decode failed");
                if (spriteSheet.width < Columns || spriteSheet.height < Rows)
                    throw new InvalidDataException("Sprite sheet too small");
                spriteSheet.filterMode = FilterMode.Bilinear;
                spriteSheet.wrapMode = TextureWrapMode.Clamp;
                spriteSheet.hideFlags = HideFlags.DontSave;
                Color32[] pixels = spriteSheet.GetPixels32();
                int cellWidth = spriteSheet.width / Columns, cellHeight = spriteSheet.height / Rows;
                frames = new Sprite[8];
                bodyHeights = new float[8];
                // Pivot and body coverage measured from the existing eight-pose sheet.
                // Ignore needles, long cloth and cyan effects when computing body center/height.
                Vector2[] pivots = {
                    new Vector2(.37f,.5f), new Vector2(.32f,.5f), new Vector2(.31f,.5f), new Vector2(.70f,.5f),
                    new Vector2(.30f,.5f), new Vector2(.50f,.45f), new Vector2(.30f,.5f), new Vector2(.51f,.40f)
                };
                float[] coverage = {1f,1f,1f,1f,1f,.84f,1f,.80f};
                for (int i = 0; i < frames.Length; i++)
                {
                    int left = (i % Columns) * cellWidth;
                    int bottom = (Rows - 1 - i / Columns) * cellHeight;
                    // Attack effect spills a little into the left edge of the adjacent healing cell.
                    int trim = i == 7 ? Math.Max(1, cellWidth / 12) : 0;
                    Rect rect = FindVisibleRect(pixels, spriteSheet.width, left + trim, bottom, cellWidth - trim, cellHeight);
                    frames[i] = Sprite.Create(spriteSheet, rect, pivots[i], 170f, 0, SpriteMeshType.FullRect);
                    frames[i].name = "SilkThread_SilkEclipse_Frame_" + i;
                    bodyHeights[i] = rect.height / 170f * coverage[i];
                }
                return true;
            }
            catch (Exception exception)
            {
                SilkThreadCrestPlugin.LogWarning("Silk Eclipse sheet load failed; original art preserved: " + exception.GetBaseException().Message);
                return false;
            }
        }

        private static Rect FindVisibleRect(Color32[] pixels, int textureWidth, int left, int bottom, int width, int height)
        {
            int minX = left + width, maxX = left - 1, minY = bottom + height, maxY = bottom - 1;
            for (int y = bottom; y < bottom + height; y++)
                for (int x = left; x < left + width; x++)
                    if (pixels[y * textureWidth + x].a > 8)
                    {
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
            if (maxX < minX || maxY < minY)
                throw new InvalidDataException("Empty sprite sheet cell");
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
