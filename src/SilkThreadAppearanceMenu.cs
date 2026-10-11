using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilkThreadCrest
{
    [DefaultExecutionOrder(10000)]
    internal sealed class SilkThreadAppearanceMenu : MonoBehaviour
    {
        private const string OriginalPreset = "Original";
        private const string CustomPalettePreset = "Paleta personalizada";
        private const string SilkEclipsePreset = "Silk Eclipse";
        private const string SkinsFolderName = "Skins";
        private const string SettingsFileName = "appearance.json";

        private sealed class CollectionBackup
        {
            internal Material[] Materials;
            internal Material[] AssetMaterials;
            internal Material Material;
            internal Material[] MaterialInsts;
            internal readonly List<Material> Clones = new List<Material>();
            internal readonly List<Texture2D> TextureClones = new List<Texture2D>();
            internal List<SilkThreadMaterialBinding.FrameSnapshot> Frames;
        }

        private readonly List<string> presets = new List<string>();
        private readonly Dictionary<string, Texture2D> packedTextures =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<tk2dSpriteCollectionData, CollectionBackup> backups =
            new Dictionary<tk2dSpriteCollectionData, CollectionBackup>();
        private readonly Dictionary<tk2dBaseSprite, Color> spriteColorBackups =
            new Dictionary<tk2dBaseSprite, Color>();
        private readonly Dictionary<Renderer, Material> rendererMaterialBackups =
            new Dictionary<Renderer, Material>();
        private static MethodInfo TextureLoadImageMethod = ResolveTextureLoadImageMethod();
        private static readonly MethodInfo UpdateMaterialMethod = typeof(tk2dBaseSprite).GetMethod(
            "UpdateMaterial", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateColorsMethod = typeof(tk2dBaseSprite).GetMethod(
            "UpdateColors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private string pluginPath;
        private string skinsPath;
        private string settingsPath;
        private KeyboardShortcut menuShortcut;
        private string selectedPreset = OriginalPreset;
        private Color customPalette = Color.white;
        private Color capePalette = Color.white;
        private Color headPalette = Color.white;
        private Color underCloakPalette = Color.white;
        private bool initialized;
        private bool menuOpen;
        private bool cursorOverridden;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockState;
        private Vector2 presetScrollPosition;
        private bool needsApply = true;
        private float nextApplyTime;
        private int lastTargetCount;
        private float nextBindingCheck;
        private int lastBoundFrames;
        private int lastVisibleBindings;
        private long lastChangedPixels;
        private string status = "A aparência original está ativa.";
        private Texture2D blackTexture;
        private Texture2D redTexture;
        private Texture2D whiteTexture;
        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle headingStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle panelStyle;

        internal void Initialize(string rootPath, KeyboardShortcut shortcut)
        {
            pluginPath = rootPath;
            skinsPath = Path.Combine(pluginPath, SkinsFolderName);
            settingsPath = Path.Combine(pluginPath, SettingsFileName);
            menuShortcut = shortcut;
            Directory.CreateDirectory(skinsPath);
            LoadSettings();
            ScanPresets();
            initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SilkThreadCrestPlugin.LogInfo("Silk Thread appearance menu ready; press " + menuShortcut.MainKey + " to open it.");
            SilkThreadCrestPlugin.LogInfo("Appearance runtime " + SilkThreadCrestPlugin.PluginVersion + ": " + typeof(SilkThreadAppearanceMenu).Assembly.Location);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CloseMenu();
            SilkThreadEclipseVisual.Clear();
            RestoreAllCollections();
            DestroyPackedTextures();
            DestroyGuiTextures();
        }

        private void EnsureGuiStyles()
        {
            if (windowStyle != null)
                return;
            blackTexture = MakeGuiTexture(new Color(0.015f, 0.015f, 0.02f, 0.98f));
            redTexture = MakeGuiTexture(new Color(0.86f, 0.035f, 0.08f, 1f));
            whiteTexture = MakeGuiTexture(new Color(0.94f, 0.94f, 0.92f, 1f));

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = blackTexture;
            windowStyle.border = new RectOffset(3, 3, 3, 3);
            windowStyle.padding = new RectOffset(18, 18, 28, 16);
            windowStyle.normal.textColor = Color.white;
            windowStyle.fontStyle = FontStyle.Bold;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.wordWrap = true;

            headingStyle = new GUIStyle(labelStyle);
            headingStyle.normal.textColor = new Color(1f, 0.12f, 0.16f, 1f);
            headingStyle.fontStyle = FontStyle.Bold;
            headingStyle.fontSize = 14;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = blackTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.background = redTexture;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.background = redTexture;
            buttonStyle.border = new RectOffset(2, 2, 2, 2);
            buttonStyle.padding = new RectOffset(10, 10, 6, 6);

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = whiteTexture;
            selectedButtonStyle.normal.textColor = Color.black;
            selectedButtonStyle.hover.background = redTexture;
            selectedButtonStyle.hover.textColor = Color.white;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = blackTexture;
            panelStyle.normal.textColor = Color.white;
            panelStyle.border = new RectOffset(2, 2, 2, 2);
            panelStyle.padding = new RectOffset(12, 12, 10, 10);
        }

        private static Texture2D MakeGuiTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.DontSave;
            return texture;
        }

        private void DestroyGuiTextures()
        {
            if (blackTexture) Destroy(blackTexture);
            if (redTexture) Destroy(redTexture);
            if (whiteTexture) Destroy(whiteTexture);
            blackTexture = null;
            redTexture = null;
            whiteTexture = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            needsApply = true;
            nextApplyTime = Time.unscaledTime + 0.35f;
        }

        private void Update()
        {
            if (!initialized)
                return;

            if (menuOpen && TurnBattleSystem.IsBattleActive)
                CloseMenu();
            if (menuShortcut.IsDown())
            {
                if (menuOpen)
                {
                    CloseMenu();
                }
                else if (!TurnBattleSystem.IsBattleActive)
                {
                    menuOpen = true;
                    OpenMenuCursor();
                    ScanPresets();
                    status = "Escolha um preset ou coloque atlases PNG na pasta Skins.";
                }
            }

            if (needsApply && Time.unscaledTime >= nextApplyTime)
            {
                needsApply = false;
                ApplySelectedPreset();
                if (!string.Equals(selectedPreset, OriginalPreset, StringComparison.OrdinalIgnoreCase) && lastTargetCount == 0)
                {
                    needsApply = true;
                    nextApplyTime = Time.unscaledTime + 0.75f;
                }
            }
        }

        private void LateUpdate()
        {
            if (menuOpen)
                ForceMenuCursor();
            if (string.Equals(selectedPreset, SilkEclipsePreset, StringComparison.OrdinalIgnoreCase))
                SilkThreadEclipseVisual.Tick();
            else if (!string.Equals(selectedPreset, OriginalPreset, StringComparison.OrdinalIgnoreCase) &&
                Time.unscaledTime >= nextBindingCheck && backups.Count > 0)
            {
                nextBindingCheck = Time.unscaledTime + 0.5f;
                foreach (var collection in FindHornetCollections())
                {
                    if (!backups.ContainsKey(collection))
                    {
                        needsApply = true;
                        nextApplyTime = Time.unscaledTime;
                        break;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!initialized || !menuOpen || TurnBattleSystem.IsBattleActive)
                return;

            ForceMenuCursor();
            EnsureGuiStyles();

            float width = Mathf.Min(720f, Mathf.Max(420f, Screen.width - 32f));
            float height = Mathf.Min(760f, Mathf.Max(440f, Screen.height - 32f));
            Rect window = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUI.Window(GetInstanceID(), window, DrawWindow, "SILK THREAD // VISUAL CUSTOMIZER " + SilkThreadCrestPlugin.PluginVersion, windowStyle);
        }

        private void OpenMenuCursor()
        {
            if (!cursorOverridden)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockState = Cursor.lockState;
                cursorOverridden = true;
            }
            ForceMenuCursor();
        }

        private static void ForceMenuCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseMenu()
        {
            menuOpen = false;
            if (!cursorOverridden)
                return;
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            cursorOverridden = false;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical(panelStyle);
            Rect accent = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(accent, redTexture);
            GUILayout.Label("PERSONALIZAÇÃO DE HORNET", headingStyle);
            GUILayout.Label("Personaliza a aparência sem alterar as mecânicas do combate.", labelStyle);
            GUILayout.Label("Presets de paleta funcionam imediatamente. Packs PNG do CustomKnight Creator devem ficar em:", labelStyle);
            GUILayout.Label(skinsPath, labelStyle);
            GUILayout.Space(8f);

            GUILayout.Label("PRESETS", headingStyle);
            presetScrollPosition = GUILayout.BeginScrollView(
                presetScrollPosition,
                false,
                true,
                GUILayout.Height(116f));
            const int columns = 3;
            for (int i = 0; i < presets.Count; i++)
            {
                if (i % columns == 0)
                    GUILayout.BeginHorizontal();
                string preset = presets[i];
                string label = string.Equals(selectedPreset, preset, StringComparison.Ordinal)
                    ? "> " + preset
                    : preset;
                GUIStyle style = string.Equals(selectedPreset, preset, StringComparison.Ordinal)
                    ? selectedButtonStyle
                    : buttonStyle;
                if (GUILayout.Button(label, style, GUILayout.MinWidth(190f), GUILayout.Height(38f)))
                {
                    selectedPreset = preset;
                    ApplySelectedPreset();
                }
                if (i % columns == columns - 1 || i == presets.Count - 1)
                    GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10f);
            if (string.Equals(selectedPreset, CustomPalettePreset, StringComparison.Ordinal))
                DrawCustomPaletteControls();
            else if (IsPackedPreset(selectedPreset))
                GUILayout.Label("Pack selecionado: os PNGs serão aplicados pelos nomes das coleções tk2d.", labelStyle);
            else
                GUILayout.Label("Preset selecionado: " + selectedPreset, labelStyle);

            GUILayout.Space(10f);
            GUILayout.Label(status, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("RECARREGAR PACKS", buttonStyle, GUILayout.Height(34f)))
            {
                ScanPresets();
                needsApply = true;
                status = "Packs reindexados.";
            }
            if (GUILayout.Button("RESTAURAR ORIGINAL", buttonStyle, GUILayout.Height(34f)))
            {
                selectedPreset = OriginalPreset;
                ApplySelectedPreset();
            }
            if (GUILayout.Button("FECHAR", buttonStyle, GUILayout.Height(34f)))
                CloseMenu();
            GUILayout.EndHorizontal();
            GUILayout.Label("Atalho: " + menuShortcut.MainKey + " abre/fecha o menu e devolve o mouse | Durante uma batalha fica bloqueado.", labelStyle);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
        }

        private void DrawCustomPaletteControls()
        {
            GUILayout.Label("PALETA PERSONALIZADA — altere cada parte separadamente.", labelStyle);
            GUILayout.Label("Máscaras por cor da arte original: tecido vermelho = capa, máscara clara = cabeça, membros escuros = sob a capa.", labelStyle);
            capePalette = DrawPartPalette("CAPA", capePalette);
            headPalette = DrawPartPalette("CABEÇA", headPalette);
            underCloakPalette = DrawPartPalette("SOB A CAPA", underCloakPalette);
            if (GUILayout.Button("APLICAR PALETA", selectedButtonStyle, GUILayout.Height(32f)))
                ApplySelectedPreset();
        }

        private Color DrawPartPalette(string partName, Color value)
        {
            GUILayout.BeginVertical(panelStyle);
            GUILayout.Label(partName, headingStyle);
            value.r = DrawChannel("Vermelho", value.r);
            value.g = DrawChannel("Verde", value.g);
            value.b = DrawChannel("Azul", value.b);
            value.a = 1f;
            GUILayout.EndVertical();
            return value;
        }

        private float DrawChannel(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(90f));
            float next = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.MinWidth(180f));
            GUILayout.Label(Mathf.RoundToInt(next * 255f).ToString(), labelStyle, GUILayout.Width(42f));
            GUILayout.EndHorizontal();
            return next;
        }

        private void ScanPresets()
        {
            presets.Clear();
            presets.Add(OriginalPreset);
            presets.Add(SilkEclipsePreset);
            presets.Add("Carmim");
            presets.Add("Vazio");
            presets.Add("Seda");
            presets.Add("Ocre");
            presets.Add(CustomPalettePreset);

            try
            {
                if (Directory.Exists(skinsPath))
                {
                    foreach (string directory in Directory.GetDirectories(skinsPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileName(directory);
                        if (!string.IsNullOrEmpty(name) && !presets.Contains(name, StringComparer.OrdinalIgnoreCase))
                            presets.Add(name);
                    }
                }
            }
            catch (Exception exception)
            {
                status = "Não foi possível ler a pasta Skins: " + exception.GetBaseException().Message;
                SilkThreadCrestPlugin.LogWarning(status);
            }

            if (!presets.Contains(selectedPreset, StringComparer.OrdinalIgnoreCase))
                selectedPreset = OriginalPreset;
        }

        private bool IsPackedPreset(string preset)
        {
            return !string.IsNullOrEmpty(preset) &&
                !string.Equals(preset, OriginalPreset, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(preset, CustomPalettePreset, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(preset, SilkEclipsePreset, StringComparison.OrdinalIgnoreCase) &&
                !IsPalettePreset(preset);
        }

        private static bool IsPalettePreset(string preset)
        {
            return string.Equals(preset, "Carmim", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preset, "Vazio", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preset, "Seda", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preset, "Ocre", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySelectedPreset()
        {
            if (!initialized)
                return;

            RestoreAllCollections();
            DestroyPackedTextures();
            SilkThreadEclipseVisual.Clear();
            lastBoundFrames = 0;
            lastVisibleBindings = 0;
            lastChangedPixels = 0;

            try
            {
                if (string.Equals(selectedPreset, OriginalPreset, StringComparison.OrdinalIgnoreCase))
                {
                    status = "A aparência original está ativa.";
                }
                else if (string.Equals(selectedPreset, SilkEclipsePreset, StringComparison.OrdinalIgnoreCase))
                {
                    bool visualReady = SilkThreadEclipseVisual.Apply(pluginPath);
                    lastTargetCount = visualReady ? 1 : 0;
                    status = visualReady
                        ? "Silk Eclipse: poses sincronizadas, escala Idle; ações sem pose usam a arte original."
                        : "Silk Eclipse aguardando a Hornet e a folha de sprites.";
                }
                else if (string.Equals(selectedPreset, CustomPalettePreset, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyPalette(capePalette, headPalette, underCloakPalette);
                    status = lastVisibleBindings > 0 && lastChangedPixels > 0
                        ? "Paleta: " + lastVisibleBindings + " renderer(s) ativo(s); " + lastChangedPixels + " pixels alterados."
                        : lastChangedPixels == 0
                            ? "Nenhum pixel mudou. Teste uma cor forte ou consulte Appearance binding no log."
                            : "Nenhum renderer ativo confirmado. Consulte Appearance binding no log.";
                }
                else if (IsPalettePreset(selectedPreset))
                {
                    Color presetColor = GetPalette(selectedPreset);
                    ApplyPalette(presetColor, presetColor, presetColor);
                    status = "Preset " + selectedPreset + ": " + lastVisibleBindings + " renderer(s), " + lastBoundFrames + " frames vinculados.";
                }
                else
                {
                    int loaded = LoadPackedTextures(Path.Combine(skinsPath, selectedPreset));
                    int changed = ApplyPackedTextures();
                    SilkThreadCrestPlugin.LogInfo("Silk Thread loaded " + loaded + " atlas(es) from appearance pack '" + selectedPreset + "' and applied " + changed + " collection(s).");
                    status = loaded == 0
                        ? "O pack não contém PNGs legíveis."
                        : "Pack aplicado: " + changed + " coleção(ões), " + loaded + " atlas(es).";
                }
                SaveSettings();
            }
            catch (Exception exception)
            {
                status = "Falha ao aplicar aparência: " + exception.GetBaseException().Message;
                SilkThreadCrestPlugin.LogWarning(status);
            }
        }

        private static Color GetPalette(string preset)
        {
            if (string.Equals(preset, "Carmim", StringComparison.OrdinalIgnoreCase))
                return new Color(0.78f, 0.16f, 0.18f, 1f);
            if (string.Equals(preset, "Vazio", StringComparison.OrdinalIgnoreCase))
                return new Color(0.28f, 0.20f, 0.46f, 1f);
            if (string.Equals(preset, "Seda", StringComparison.OrdinalIgnoreCase))
                return new Color(0.92f, 0.34f, 0.66f, 1f);
            return new Color(0.92f, 0.62f, 0.16f, 1f);
        }

        private void ApplyPalette(Color tint)
        {
            ApplyPalette(tint, tint, tint);
        }

        private void ApplyPalette(Color capeTint, Color headTint, Color underCloakTint)
        {
            List<tk2dSpriteCollectionData> collections = FindHornetCollections();
            lastTargetCount = collections.Count;
            HashSet<tk2dSpriteCollectionData> pixelCollections = new HashSet<tk2dSpriteCollectionData>();
            foreach (tk2dSpriteCollectionData collection in collections)
            {
                CollectionBackup backup = SaveBackup(collection);
                Material[] replacements = CloneMaterials(backup.Materials, backup.Clones);
                for (int i = 0; i < replacements.Length; i++)
                {
                    if (replacements[i] != null)
                        replacements[i].color = Color.white;
                }
                if (ApplyPartTexturePalette(collection, backup, replacements, capeTint, headTint, underCloakTint))
                    pixelCollections.Add(collection);
                lastBoundFrames += SilkThreadMaterialBinding.Bind(collection, replacements, backup.Frames, backup.Materials);
                RefreshActiveSprites(collection);
            }

            List<tk2dBaseSprite> activeSprites = FindActiveHornetSprites();
            int capeCount = 0;
            int headCount = 0;
            int underCloakCount = 0;
            foreach (tk2dBaseSprite sprite in activeSprites)
            {
                if (sprite && pixelCollections.Contains(SilkThreadMaterialBinding.RuntimeCollection(sprite)))
                    continue;
                HornetPart part = ClassifySprite(sprite);
                if (part == HornetPart.None)
                    continue;
                if (!spriteColorBackups.ContainsKey(sprite))
                    spriteColorBackups[sprite] = sprite.color;
                if (part == HornetPart.Cape)
                {
                    sprite.color = capeTint;
                    capeCount++;
                }
                else if (part == HornetPart.Head)
                {
                    sprite.color = headTint;
                    headCount++;
                }
                else if (part == HornetPart.UnderCloak)
                {
                    sprite.color = underCloakTint;
                    underCloakCount++;
                }
                RefreshSpriteColors(sprite);
            }
            SilkThreadCrestPlugin.LogInfo("Silk Thread appearance palette found " + collections.Count + " Hornet collection(s), atlas masks=" + pixelCollections.Count + "; fallback parts cape=" + capeCount + ", head=" + headCount + ", under-cloak=" + underCloakCount + ".");
            SilkThreadCrestPlugin.LogInfo("Appearance binding " + SilkThreadCrestPlugin.PluginVersion + ": frames=" + lastBoundFrames + ", active renderers=" + lastVisibleBindings + ", changed pixels=" + lastChangedPixels + ", preset=" + selectedPreset);
            if (lastVisibleBindings == 0 && HeroController.instance)
            {
                foreach (Renderer renderer in HeroController.instance.GetComponentsInChildren<Renderer>(true).Take(20))
                    SilkThreadCrestPlugin.LogInfo("Appearance unbound renderer: " + renderer.name + "; type=" + renderer.GetType().Name +
                        "; enabled=" + renderer.enabled + "; active=" + renderer.gameObject.activeInHierarchy +
                        "; material=" + (renderer.sharedMaterial ? renderer.sharedMaterial.name : "none"));
            }
        }

        private bool ApplyPartTexturePalette(tk2dSpriteCollectionData collection, CollectionBackup backup, Material[] replacements, Color capeTint, Color headTint, Color underCloakTint)
        {
            if (!collection || collection.spriteDefinitions == null || collection.spriteDefinitions.Length == 0)
                return false;
            Dictionary<Texture2D, Color32[]> buffers = new Dictionary<Texture2D, Color32[]>();
            Dictionary<Texture2D, Texture2D> clones = new Dictionary<Texture2D, Texture2D>();
            Dictionary<Texture2D, HashSet<string>> processedRegions = new Dictionary<Texture2D, HashSet<string>>();
            bool changed = false;

            foreach (tk2dSpriteDefinition definition in collection.spriteDefinitions)
            {
                if (definition == null)
                    continue;
                Material sourceMaterial = definition.materialInst ? definition.materialInst : definition.material;
                if (sourceMaterial == null && definition.materialId >= 0 && definition.materialId < backup.Materials.Length)
                    sourceMaterial = backup.Materials[definition.materialId];
                Texture2D sourceTexture = sourceMaterial != null ? sourceMaterial.mainTexture as Texture2D : null;
                if (!sourceTexture)
                    continue;
                Color32[] pixels;
                if (!buffers.TryGetValue(sourceTexture, out pixels))
                {
                    pixels = ReadTexturePixels(sourceTexture);
                    if (pixels == null)
                        continue;
                    buffers[sourceTexture] = pixels;
                    Texture2D clone = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
                    clone.name = sourceTexture.name + " [SilkThread Palette]";
                    clone.filterMode = sourceTexture.filterMode;
                    clone.wrapMode = sourceTexture.wrapMode;
                    clone.hideFlags = HideFlags.DontSave;
                    clones[sourceTexture] = clone;
                    backup.TextureClones.Add(clone);
                }

                Rect region = GetDefinitionRegion(definition, sourceTexture.width, sourceTexture.height);
                if (region.width <= 0f || region.height <= 0f)
                    continue;
                HashSet<string> regions;
                if (!processedRegions.TryGetValue(sourceTexture, out regions))
                    processedRegions[sourceTexture] = regions = new HashSet<string>();
                string regionKey = region.x + ":" + region.y + ":" + region.width + ":" + region.height;
                if (!regions.Add(regionKey))
                    continue;
                int modified = RecolorRegion(pixels, sourceTexture.width, sourceTexture.height, region, capeTint, headTint, underCloakTint);
                lastChangedPixels += modified;
                changed |= modified > 0;
            }

            foreach (KeyValuePair<Texture2D, Texture2D> pair in clones)
            {
                Color32[] pixels = buffers[pair.Key];
                pair.Value.SetPixels32(pixels);
                pair.Value.Apply(false, false);
            }
            for (int i = 0; i < replacements.Length; i++)
            {
                Material material = replacements[i];
                Texture2D original = material != null ? material.mainTexture as Texture2D : null;
                Texture2D clone;
                if (original != null && clones.TryGetValue(original, out clone))
                    material.mainTexture = clone;
            }
            return changed;
        }

        private static Color32[] ReadTexturePixels(Texture2D source)
        {
            try
            {
                return source.GetPixels32();
            }
            catch
            {
                RenderTexture target = null;
                RenderTexture previous = RenderTexture.active;
                Texture2D readable = null;
                try
                {
                    target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, target);
                    RenderTexture.active = target;
                    readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                    readable.Apply(false, false);
                    return readable.GetPixels32();
                }
                catch (Exception exception)
                {
                    SilkThreadCrestPlugin.LogWarning("Could not read a Hornet atlas for palette masks: " + exception.GetBaseException().Message);
                    return null;
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (target != null)
                        RenderTexture.ReleaseTemporary(target);
                    if (readable != null)
                        UnityEngine.Object.Destroy(readable);
                }
            }
        }

        private static Rect GetDefinitionRegion(tk2dSpriteDefinition definition, int textureWidth, int textureHeight)
        {
            if (definition.uvs != null && definition.uvs.Length > 0)
            {
                float minU = definition.uvs.Min(value => value.x);
                float maxU = definition.uvs.Max(value => value.x);
                float minV = definition.uvs.Min(value => value.y);
                float maxV = definition.uvs.Max(value => value.y);
                int x = Mathf.Clamp(Mathf.FloorToInt(minU * textureWidth), 0, textureWidth - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(minV * textureHeight), 0, textureHeight - 1);
                int width = Mathf.Clamp(Mathf.CeilToInt((maxU - minU) * textureWidth), 1, textureWidth - x);
                int height = Mathf.Clamp(Mathf.CeilToInt((maxV - minV) * textureHeight), 1, textureHeight - y);
                return new Rect(x, y, width, height);
            }
            if (definition.extractRegion && definition.regionW > 0 && definition.regionH > 0)
            {
                int x = Mathf.Clamp(definition.regionX, 0, textureWidth - 1);
                int y = Mathf.Clamp(definition.regionY, 0, textureHeight - 1);
                return new Rect(x, y, Math.Min(definition.regionW, textureWidth - x), Math.Min(definition.regionH, textureHeight - y));
            }
            return new Rect(0f, 0f, 0f, 0f);
        }

        private static int RecolorRegion(Color32[] pixels, int textureWidth, int textureHeight, Rect region, Color capeTint, Color headTint, Color underCloakTint)
        {
            int changed = 0;
            int left = Mathf.Clamp(Mathf.FloorToInt(region.x), 0, textureWidth - 1);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(region.y), 0, textureHeight - 1);
            int right = Mathf.Clamp(Mathf.CeilToInt(region.xMax), left + 1, textureWidth);
            int top = Mathf.Clamp(Mathf.CeilToInt(region.yMax), bottom + 1, textureHeight);
            for (int y = bottom; y < top; y++)
            {
                for (int x = left; x < right; x++)
                {
                    int index = y * textureWidth + x;
                    Color32 source = pixels[index];
                    if (source.a <= 8)
                        continue;
                    // Approximate material masks for the original Hornet palette:
                    // neutral bright = mask/head, red cloth = cloak, dark neutral = limbs.
                    // These are color masks, not a perfect manually authored segmentation.
                    bool cloth = source.r > source.g * 1.25f && source.r > source.b * 1.12f && source.r > 24;
                    int high = Math.Max(source.r, Math.Max(source.g, source.b));
                    int low = Math.Min(source.r, Math.Min(source.g, source.b));
                    bool head = high > 110 && high - low < 85;
                    if (!cloth && !head && high <= 12)
                        continue; // preserve black outlines and eyes
                    Color tint = cloth ? capeTint : head ? headTint : underCloakTint;
                    Color32 replacement = TintPixel(source, tint);
                    if (source.r != replacement.r || source.g != replacement.g || source.b != replacement.b)
                        changed++;
                    pixels[index] = replacement;
                }
            }
            return changed;
        }

        private static Color32 TintPixel(Color32 source, Color tint)
        {
            if (tint.r > 0.995f && tint.g > 0.995f && tint.b > 0.995f)
                return source;
            Color original = source;
            float luminance = Mathf.Clamp01(original.r * 0.299f + original.g * 0.587f + original.b * 0.114f);
            float tintPeak = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            float brightness = Mathf.Clamp01(Mathf.Max(0.2f, luminance) / Mathf.Max(0.001f, tintPeak) * 1.15f);
            Color replacement = tintPeak <= 0.001f ? Color.black : tint * brightness;
            Color result = Color.Lerp(original, replacement, 0.82f);
            result.a = original.a;
            return result;
        }

        private int LoadPackedTextures(string directory)
        {
            if (!Directory.Exists(directory))
                return 0;
            int loaded = 0;
            foreach (string file in Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories))
            {
                try
                {
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!TryLoadPng(texture, File.ReadAllBytes(file)))
                    {
                        Destroy(texture);
                        continue;
                    }
                    texture.name = "SilkThreadSkin_" + Path.GetFileNameWithoutExtension(file);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Point;
                    string key = Path.GetFileNameWithoutExtension(file);
                    if (packedTextures.ContainsKey(key))
                        Destroy(packedTextures[key]);
                    packedTextures[key] = texture;
                    loaded++;
                }
                catch (Exception exception)
                {
                    SilkThreadCrestPlugin.LogWarning("Could not load skin atlas " + file + ": " + exception.GetBaseException().Message);
                }
            }
            return loaded;
        }

        private static bool TryLoadPng(Texture2D texture, byte[] bytes)
        {
            if (TextureLoadImageMethod == null)
                TextureLoadImageMethod = ResolveTextureLoadImageMethod();
            if (TextureLoadImageMethod == null)
                return false;
            try
            {
                return (bool)TextureLoadImageMethod.Invoke(null, new object[] { texture, bytes });
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo ResolveTextureLoadImageMethod()
        {
            Type imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (imageConversion == null)
                return null;
            return imageConversion.GetMethod(
                "LoadImage",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Texture2D), typeof(byte[]) },
                modifiers: null);
        }

        private int ApplyPackedTextures()
        {
            int changedCollections = 0;
            List<tk2dSpriteCollectionData> collections = FindHornetCollections();
            lastTargetCount = collections.Count;
            foreach (tk2dSpriteCollectionData collection in collections)
            {
                CollectionBackup backup = SaveBackup(collection);
                Material[] replacements = CloneMaterials(backup.Materials, backup.Clones);
                bool changed = false;
                for (int i = 0; i < replacements.Length; i++)
                {
                    Material material = replacements[i];
                    Texture2D replacement = FindReplacementTexture(collection, material);
                    if (material == null || replacement == null)
                        continue;
                    material.mainTexture = replacement;
                    changed = true;
                }
                if (changed)
                {
                    SilkThreadMaterialBinding.Bind(collection, replacements, backup.Frames, backup.Materials);
                    RefreshActiveSprites(collection);
                    changedCollections++;
                }
            }
            SilkThreadCrestPlugin.LogInfo("Silk Thread appearance pack inspected " + collections.Count + " Hornet collection(s) and changed " + changedCollections + ".");
            return changedCollections;
        }

        private Texture2D FindReplacementTexture(tk2dSpriteCollectionData collection, Material material)
        {
            string collectionName = collection != null ? collection.name : "";
            string spriteCollectionName = collection != null ? collection.spriteCollectionName : "";
            string materialTextureName = material != null && material.mainTexture != null ? material.mainTexture.name : "";
            foreach (KeyValuePair<string, Texture2D> entry in packedTextures)
            {
                if (NamesMatch(entry.Key, collectionName) ||
                    NamesMatch(entry.Key, spriteCollectionName) ||
                    NamesMatch(entry.Key, materialTextureName))
                    return entry.Value;
            }
            return null;
        }

        private static bool NamesMatch(string atlasName, string gameName)
        {
            if (string.IsNullOrEmpty(atlasName) || string.IsNullOrEmpty(gameName))
                return false;
            string left = NormalizeName(atlasName);
            string right = NormalizeName(gameName);
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
                left.Length > 3 && right.IndexOf(left, StringComparison.OrdinalIgnoreCase) >= 0 ||
                right.Length > 3 && left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeName(string value)
        {
            char[] chars = value.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        private List<tk2dSpriteCollectionData> FindHornetCollections()
        {
            HashSet<tk2dSpriteCollectionData> found = new HashSet<tk2dSpriteCollectionData>();
            HeroController hero = HeroController.instance;
            if (hero)
            {
                foreach (tk2dBaseSprite sprite in FindActiveHornetSprites())
                {
                    if (sprite && sprite.Collection)
                        found.Add(SilkThreadMaterialBinding.RuntimeCollection(sprite));
                }
            }
            found.Remove(null);
            return found.ToList();
        }

        private static List<tk2dBaseSprite> FindActiveHornetSprites()
        {
            HeroController hero = HeroController.instance;
            return hero
                ? hero.GetComponentsInChildren<tk2dBaseSprite>(true).Where(sprite => sprite && IsCharacterSprite(sprite)).ToList()
                : new List<tk2dBaseSprite>();
        }

        private enum HornetPart
        {
            None,
            Cape,
            Head,
            UnderCloak
        }

        private static bool IsCharacterSprite(tk2dBaseSprite sprite)
        {
            string identity = GetSpriteIdentity(sprite);
            return identity.IndexOf("hitbox", StringComparison.OrdinalIgnoreCase) < 0 &&
                identity.IndexOf("collider", StringComparison.OrdinalIgnoreCase) < 0 &&
                identity.IndexOf("trail", StringComparison.OrdinalIgnoreCase) < 0 &&
                identity.IndexOf("effect", StringComparison.OrdinalIgnoreCase) < 0 &&
                identity.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static HornetPart ClassifySprite(tk2dBaseSprite sprite)
        {
            string identity = GetSpriteIdentity(sprite);
            if (ContainsAny(identity, "head", "mask", "face", "hair", "hornethead"))
                return HornetPart.Head;
            if (ContainsAny(identity, "cape", "cloak", "mantle", "shawl", "skirt", "backcloth"))
                return HornetPart.Cape;
            if (ContainsAny(identity, "undercloak", "under_cloak", "body", "torso", "chest", "arm", "leg", "hand", "foot", "needle", "weapon", "undersuit", "shirt", "tunic"))
                return HornetPart.UnderCloak;
            return HornetPart.None;
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string GetSpriteIdentity(tk2dBaseSprite sprite)
        {
            if (!sprite)
                return string.Empty;
            string identity = sprite.name + " " + sprite.gameObject.name;
            try
            {
                if (sprite.CurrentSprite != null)
                    identity += " " + sprite.CurrentSprite.name;
            }
            catch
            {
                // Some pooled tk2d sprites do not expose a valid current definition during setup.
            }
            Transform parent = sprite.transform.parent;
            int depth = 0;
            while (parent != null && depth++ < 6)
            {
                identity += " " + parent.name;
                parent = parent.parent;
            }
            return identity;
        }

        private void RefreshActiveSprites(tk2dSpriteCollectionData collection)
        {
            foreach (tk2dBaseSprite sprite in FindActiveHornetSprites())
            {
                if (!sprite || SilkThreadMaterialBinding.RuntimeCollection(sprite) != collection)
                    continue;
                try
                {
                    Renderer renderer = sprite.GetComponent<Renderer>();
                    if (renderer)
                    {
                        if (!rendererMaterialBackups.ContainsKey(renderer))
                            rendererMaterialBackups[renderer] = renderer.sharedMaterial;
                        Material[] activeMaterials = collection.materialInsts != null && collection.materialInsts.Length > 0
                            ? collection.materialInsts
                            : collection.materials;
                        int materialId = sprite.CurrentSprite != null ? sprite.CurrentSprite.materialId : 0;
                        if (activeMaterials != null && materialId >= 0 && materialId < activeMaterials.Length && activeMaterials[materialId] != null)
                            renderer.sharedMaterial = activeMaterials[materialId];
                    }
                    // Use the concrete implementation: the base method is abstract.
                    MethodInfo refresh = sprite.GetType().GetMethod("UpdateMaterial", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (refresh != null)
                        refresh.Invoke(sprite, null);
                    RefreshSpriteColors(sprite);
                    if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy && sprite.CurrentSprite != null && renderer.sharedMaterial &&
                        sprite.CurrentSprite.materialInst && renderer.sharedMaterial.mainTexture == sprite.CurrentSprite.materialInst.mainTexture)
                    {
                        lastVisibleBindings++;
                        SilkThreadCrestPlugin.LogInfo("Appearance renderer bound: " + GetSpriteIdentity(sprite) +
                            "; enabled=" + renderer.enabled + "; active=" + renderer.gameObject.activeInHierarchy +
                            "; texture=" + renderer.sharedMaterial.mainTexture.name);
                    }
                }
                catch (Exception exception)
                {
                    SilkThreadCrestPlugin.LogWarning("Could not refresh an active Hornet sprite: " + exception.GetBaseException().Message);
                }
            }
        }

        private static bool IsHornetCollection(tk2dSpriteCollectionData collection)
        {
            if (!collection)
                return false;
            string name = (collection.name ?? "") + " " + (collection.spriteCollectionName ?? "");
            if (name.IndexOf("hornet", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (collection.materials == null)
                return false;
            foreach (Material material in collection.materials)
            {
                if (material != null && material.mainTexture != null &&
                    material.mainTexture.name.IndexOf("hornet", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private CollectionBackup SaveBackup(tk2dSpriteCollectionData collection)
        {
            CollectionBackup existing;
            if (backups.TryGetValue(collection, out existing))
                return existing;
            Material[] current = collection.materialInsts != null && collection.materialInsts.Length > 0
                ? collection.materialInsts : collection.materials;
            if ((current == null || current.Length == 0) && collection.material != null)
                current = new[] { collection.material };
            if (current == null)
                current = new Material[0];
            Material[] originals = new Material[current.Length];
            Array.Copy(current, originals, current.Length);
            CollectionBackup backup = new CollectionBackup
            {
                Materials = originals,
                AssetMaterials = collection.materials,
                Material = collection.material,
                MaterialInsts = collection.materialInsts,
                Frames = SilkThreadMaterialBinding.Capture(collection)
            };
            backups[collection] = backup;
            return backup;
        }

        private static Material[] CloneMaterials(Material[] originals, List<Material> clones)
        {
            Material[] replacements = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null)
                    continue;
                Material clone = new Material(originals[i]);
                clone.name = originals[i].name + " [SilkThread Appearance]";
                clone.hideFlags = HideFlags.DontSave;
                clones.Add(clone);
                replacements[i] = clone;
            }
            return replacements;
        }

        private void RestoreAllCollections()
        {
            foreach (KeyValuePair<tk2dSpriteCollectionData, CollectionBackup> pair in backups)
            {
                tk2dSpriteCollectionData collection = pair.Key;
                CollectionBackup backup = pair.Value;
                SilkThreadMaterialBinding.Restore(backup.Frames);
                if (collection)
                {
                    collection.materials = backup.AssetMaterials;
                    collection.materialInsts = backup.MaterialInsts;
                    collection.material = backup.Material;
                }
                foreach (Material clone in backup.Clones)
                {
                    if (clone)
                        Destroy(clone);
                }
                foreach (Texture2D clone in backup.TextureClones)
                {
                    if (clone)
                        Destroy(clone);
                }
            }
            backups.Clear();
            foreach (KeyValuePair<Renderer, Material> pair in rendererMaterialBackups)
            {
                if (pair.Key)
                    pair.Key.sharedMaterial = pair.Value;
            }
            rendererMaterialBackups.Clear();
            foreach (KeyValuePair<tk2dBaseSprite, Color> pair in spriteColorBackups)
            {
                if (!pair.Key)
                    continue;
                pair.Key.color = pair.Value;
                RefreshSpriteColors(pair.Key);
            }
            spriteColorBackups.Clear();
        }

        private static void RefreshSpriteColors(tk2dBaseSprite sprite)
        {
            if (!sprite)
                return;
            MethodInfo refresh = sprite.GetType().GetMethod("UpdateColors", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (refresh != null)
                refresh.Invoke(sprite, null);
        }

        private void DestroyPackedTextures()
        {
            foreach (Texture2D texture in packedTextures.Values)
            {
                if (texture)
                    Destroy(texture);
            }
            packedTextures.Clear();
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return;
                JObject data = JObject.Parse(File.ReadAllText(settingsPath));
                selectedPreset = (string)data["preset"] ?? OriginalPreset;
                customPalette = new Color(
                    (float?)data["r"] ?? 1f,
                    (float?)data["g"] ?? 1f,
                    (float?)data["b"] ?? 1f,
                    1f);
                capePalette = ReadStoredColor(data, "cape", customPalette);
                headPalette = ReadStoredColor(data, "head", customPalette);
                underCloakPalette = ReadStoredColor(data, "underCloak", ReadStoredColor(data, "body", customPalette));
            }
            catch (Exception exception)
            {
                selectedPreset = OriginalPreset;
                SilkThreadCrestPlugin.LogWarning("Could not load appearance settings: " + exception.GetBaseException().Message);
            }
        }

        private void SaveSettings()
        {
            try
            {
                JObject data = new JObject
                {
                    ["preset"] = selectedPreset,
                    ["r"] = customPalette.r,
                    ["g"] = customPalette.g,
                    ["b"] = customPalette.b,
                    ["capeR"] = capePalette.r,
                    ["capeG"] = capePalette.g,
                    ["capeB"] = capePalette.b,
                    ["headR"] = headPalette.r,
                    ["headG"] = headPalette.g,
                    ["headB"] = headPalette.b,
                    ["underCloakR"] = underCloakPalette.r,
                    ["underCloakG"] = underCloakPalette.g,
                    ["underCloakB"] = underCloakPalette.b
                };
                File.WriteAllText(settingsPath, data.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception exception)
            {
                SilkThreadCrestPlugin.LogWarning("Could not save appearance settings: " + exception.GetBaseException().Message);
            }
        }

        private static Color ReadStoredColor(JObject data, string prefix, Color fallback)
        {
            return new Color(
                (float?)data[prefix + "R"] ?? fallback.r,
                (float?)data[prefix + "G"] ?? fallback.g,
                (float?)data[prefix + "B"] ?? fallback.b,
                1f);
        }
    }
}
