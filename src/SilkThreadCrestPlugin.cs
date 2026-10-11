using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;

namespace SilkThreadCrest
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SilkThreadCrestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "manus.silkthreadcrest";
        public const string PluginName = "Silk Thread Crest";
        public const string PluginVersion = "0.7.25";
        public const string CrestId = "SilkThread";
        public const string SacrificeId = "SilkThreadSacrifice";

        private Harmony harmony;
        private ToolItemManager registeredManager;
        private bool sacrificeRegistered;
        private float nextCheckTime;
        private bool waitingForCrestMenuLogged;
        private ConfigEntry<KeyboardShortcut> appearanceMenuShortcut;
        private SilkThreadAppearanceMenu appearanceMenu;
        internal static SilkThreadCrestPlugin Instance { get; private set; }
        internal static bool AllowNativeDownspikeBounce { get; private set; }

        private void Awake()
        {
            Instance = this;
            appearanceMenuShortcut = Config.Bind(
                "Appearance",
                "MenuShortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Abre o menu de presets e skins visuais do Silk Thread Crest.");
            try
            {
                appearanceMenu = gameObject.AddComponent<SilkThreadAppearanceMenu>();
                string appearanceRoot = Path.GetDirectoryName(typeof(SilkThreadCrestPlugin).Assembly.Location);
                appearanceMenu.Initialize(appearanceRoot, appearanceMenuShortcut.Value);
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Silk Thread appearance menu disabled without affecting the battle system: " + exception.GetBaseException().Message);
            }
            harmony = new Harmony(PluginGuid);
            Assembly assembly = typeof(SilkThreadCrestPlugin).Assembly;
            foreach (Type patchType in assembly.GetTypes()
                .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0))
            {
                bool optionalSavePatch = patchType == typeof(SilkThreadSerializeSavePatch) ||
                    patchType == typeof(SilkThreadLoadSavePatch) ||
                    patchType == typeof(SilkThreadLoadTypedSavePatch) ||
                    patchType == typeof(SilkThreadNewGamePatch);
                if (optionalSavePatch)
                    continue;
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                }
                catch (Exception exception)
                {
                    Logger.LogError("Silk Thread skipped patch class " + patchType.Name + ": " + exception.GetBaseException().Message);
                }
            }

            TryInstallOptionalSavePatches();
            Logger.LogInfo("Silk Thread Crest loaded; waiting for ToolItemManager and PlayerData.");
        }

        private void TryInstallOptionalSavePatches()
        {
            Type[] savePatches =
            {
                typeof(SilkThreadSerializeSavePatch),
                typeof(SilkThreadLoadSavePatch),
                typeof(SilkThreadLoadTypedSavePatch),
                typeof(SilkThreadNewGamePatch)
            };
            foreach (Type patchType in savePatches)
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    Logger.LogInfo("Silk Thread installed optional save patch " + patchType.Name + ".");
                }
                catch (Exception exception)
                {
                    Logger.LogWarning("Silk Thread could not install optional save patch " + patchType.Name +
                        "; battles remain enabled and summon progress will stay session-local: " +
                        exception.GetBaseException().Message);
                }
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheckTime)
                return;
            nextCheckTime = Time.unscaledTime + 0.5f;

            EnsureTurnBattleSystem();

            ToolItemManager manager = ManagerSingleton<ToolItemManager>.Instance;
            if (!manager || !PlayerData.HasInstance)
                return;
            if (manager == registeredManager && ToolItemManager.GetCrestByName(CrestId) && sacrificeRegistered)
                return;
            if (!HasReadyCrestMenu())
            {
                if (!waitingForCrestMenuLogged)
                {
                    Logger.LogInfo("Waiting for InventoryToolCrestList to finish its initial Setup before registering the custom crest.");
                    waitingForCrestMenuLogged = true;
                }
                return;
            }
            waitingForCrestMenuLogged = false;

            TryRegisterCrest(manager);
        }

        private void EnsureTurnBattleSystem()
        {
            HeroController hero = HeroController.instance;
            if (!hero || hero.GetComponent<TurnBattleSystem>())
                return;
            hero.gameObject.AddComponent<TurnBattleSystem>();
            Logger.LogInfo("Turn-based battle system attached to Hornet.");
        }

        private static bool HasReadyCrestMenu()
        {
            InventoryToolCrestList[] lists = Resources.FindObjectsOfTypeAll<InventoryToolCrestList>();
            foreach (InventoryToolCrestList list in lists)
            {
                if (list && list.gameObject.scene.IsValid() && list.IsSetupComplete)
                    return true;
            }
            return false;
        }

        internal static bool IsThreadCrestEquipped(HeroController hero)
        {
            return hero && PlayerData.HasInstance && PlayerData.instance.CurrentCrestID == CrestId;
        }

        internal static bool IsSacrificeEquipped(HeroController hero)
        {
            if (!IsThreadCrestEquipped(hero))
                return false;
            ToolItem item = ToolItemManager.GetToolByName(SacrificeId);
            return item && item.IsEquipped;
        }

        internal static void StartNormalSacrificePogo(HeroController hero)
        {
            if (!hero)
                return;
            AllowNativeDownspikeBounce = true;
            try
            {
                hero.DownspikeBounce(false);
            }
            finally
            {
                AllowNativeDownspikeBounce = false;
            }
        }

        internal static void LogInfo(string message)
        {
            if (Instance)
                Instance.Logger.LogInfo(message);
        }

        internal static void LogWarning(string message)
        {
            if (Instance)
                Instance.Logger.LogWarning(message);
        }

        private void TryRegisterCrest(ToolItemManager manager)
        {
            try
            {
                ToolCrest existing = ToolItemManager.GetCrestByName(CrestId);
                if (existing)
                {
                    CrestLocalization.Configure(existing);
                    if (!existing.IsUnlocked)
                    {
                        Logger.LogInfo("Found an existing Brasão do Fio entry; retrying unlock now that player data is available.");
                        existing.Unlock();
                    }
                    TryRegisterSacrifice(manager, existing);
                    registeredManager = manager;
                    Logger.LogInfo("Brasão do Fio state: total=" + ToolItemManager.GetAllCrests().Count + ", unlocked=" + existing.IsUnlocked + ", visible=" + existing.IsVisible + ".");
                    ScheduleCrestMenuRefresh();
                    return;
                }

                ToolCrest template = ToolItemManager.GetCrestByName("Hunter");
                if (!template)
                    template = ToolItemManager.GetAllCrests().FirstOrDefault(c => c);
                if (!template)
                {
                    Logger.LogWarning("No base crest found yet; will retry.");
                    return;
                }

                ToolCrest clone = UnityEngine.Object.Instantiate(template);
                clone.name = CrestId;
                SetField(clone, "isHidden", false);
                SetField(clone, "previousVersion", null);
                SetField(clone, "oldPreviousVersion", null);
                SetField(clone, "upgradedVersion", null);

                CrestLocalization.Configure(clone);

                FieldInfo listField = AccessTools.Field(typeof(ToolItemManager), "crestList");
                ToolCrestList crestList = listField == null ? null : listField.GetValue(manager) as ToolCrestList;
                if (!crestList)
                {
                    Logger.LogError("Could not access ToolItemManager.crestList; crest was not registered.");
                    UnityEngine.Object.Destroy(clone);
                    return;
                }

                crestList.Add(clone);
                ToolItemManager.IncrementVersion();
                CrestLocalization.ApplyTranslations();
                Logger.LogInfo("Added Brasão do Fio to ToolCrestList; unlocking it in player data.");
                if (!clone.IsUnlocked)
                    clone.Unlock();
                TryRegisterSacrifice(manager, clone);
                registeredManager = manager;
                Logger.LogInfo("Registered and unlocked the new crest: " + CrestId + " (other attacks/config are inherited from " + template.name + ").");
                Logger.LogInfo("Brasão do Fio state: total=" + ToolItemManager.GetAllCrests().Count + ", unlocked=" + clone.IsUnlocked + ", visible=" + clone.IsVisible + ".");
                ScheduleCrestMenuRefresh();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to register crest; retrying on next check: " + ex);
            }
        }

        private void TryRegisterSacrifice(ToolItemManager manager, ToolCrest crest)
        {
            ToolItem existing = ToolItemManager.GetToolByName(SacrificeId);
            if (existing)
            {
                if (existing is ToolItemBasic existingBasic)
                    CrestLocalization.ConfigureSacrifice(existingBasic);
                if (!existing.IsUnlocked)
                    existing.Unlock(popupFlags: ToolItem.PopupFlags.None);
                sacrificeRegistered = existing.IsUnlocked;
                if (sacrificeRegistered)
                    CrestLocalization.ApplyTranslations();
                return;
            }

            ToolCrest.SlotInfo[] slots = crest ? crest.Slots : null;
            ToolCrestsData.Data crestData = crest ? crest.SaveData : default(ToolCrestsData.Data);
            ToolItemType? compatibleType = null;
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    bool unlocked = !slots[i].IsLocked && (crestData.Slots == null || i >= crestData.Slots.Count || crestData.Slots[i].IsUnlocked);
                    if (unlocked && !slots[i].Type.IsAttackType())
                    {
                        compatibleType = slots[i].Type;
                        break;
                    }
                }
                if (!compatibleType.HasValue)
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        bool unlocked = !slots[i].IsLocked && (crestData.Slots == null || i >= crestData.Slots.Count || crestData.Slots[i].IsUnlocked);
                        if (unlocked)
                        {
                            compatibleType = slots[i].Type;
                            break;
                        }
                    }
                }
            }
            if (!compatibleType.HasValue)
            {
                Logger.LogWarning("Could not find an unlocked Brasão do Fio slot for Sacrifício; will retry.");
                return;
            }

            ToolItemBasic template = ToolItemManager.GetAllTools().OfType<ToolItemBasic>()
                .FirstOrDefault(item => item && item.Type == compatibleType.Value);
            if (!template)
                template = ToolItemManager.GetAllTools().OfType<ToolItemBasic>().FirstOrDefault(item => item);
            if (!template)
            {
                Logger.LogWarning("Could not find a basic ToolItem template for Sacrifício; will retry.");
                return;
            }

            ToolItemBasic clone = UnityEngine.Object.Instantiate(template);
            clone.name = SacrificeId;
            SetField(clone, "type", compatibleType.Value);
            SetField(clone, "isCounted", false);
            SetField(clone, "preventTutorialMsg", true);
            SetField(clone, "baseStorageAmount", 0);
            SetField(clone, "unlockStartAmount", 0);
            SetField(clone, "usageOptions", default(ToolItem.UsageOptions));
            SetField(clone, "hasCustomAction", false);
            SetField(clone, "damageFlags", default(ToolDamageFlags));
            CrestLocalization.ConfigureSacrifice(clone);

            FieldInfo listField = AccessTools.Field(typeof(ToolItemManager), "toolItems");
            ToolItemList itemList = listField == null ? null : listField.GetValue(manager) as ToolItemList;
            if (!itemList)
            {
                Logger.LogError("Could not access ToolItemManager.toolItems; Sacrifício was not registered.");
                UnityEngine.Object.Destroy(clone);
                return;
            }

            itemList.Add(clone);
            ToolItemManager.IncrementVersion();
            clone.Unlock(popupFlags: ToolItem.PopupFlags.None);
            ToolItemManager.SendEquippedChangedEvent(force: true);
            CrestLocalization.ApplyTranslations();
            sacrificeRegistered = true;
            Logger.LogInfo("Registered and unlocked item Sacrifício in the " + compatibleType.Value + " slot category of Brasão do Fio; its icon is a placeholder copied from " + template.name + ".");
        }

        private void ScheduleCrestMenuRefresh()
        {
            CancelInvoke(nameof(RefreshCrestMenus));
            Invoke(nameof(RefreshCrestMenus), 0.35f);
        }

        private void RefreshCrestMenus()
        {
            try
            {
                MethodInfo setup = AccessTools.Method(typeof(InventoryToolCrestList), "Setup");
                if (setup == null)
                {
                    Logger.LogWarning("Could not find InventoryToolCrestList.Setup; it will refresh the next time the crest pane opens.");
                    return;
                }

                int refreshed = 0;
                InventoryToolCrestList[] lists = Resources.FindObjectsOfTypeAll<InventoryToolCrestList>();
                foreach (InventoryToolCrestList list in lists)
                {
                    if (!list || !list.gameObject.scene.IsValid())
                        continue;
                    setup.Invoke(list, null);
                    refreshed++;
                }
                Logger.LogInfo("Refreshed " + refreshed + " crest menu instance(s); available crests=" + ToolItemManager.GetAllCrests().Count + ".");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not refresh crest menu; it should refresh when reopened: " + ex.GetBaseException().Message);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null)
                    continue;
                field.SetValue(target, value);
                return;
            }
        }

        private void OnDestroy()
        {
            if (harmony != null)
                harmony.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }
    }

    internal static class CrestLocalization
    {
        private sealed class Translation
        {
            public string Sheet;
            public string Key;
            public string Value;
        }

        private static readonly List<Translation> Entries = new List<Translation>();
        internal static void Configure(ToolCrest crest)
        {
            Entries.Clear();
            AddFieldTranslation(crest, "displayName", "SILK_THREAD_CREST_NAME", "Brasão do Fio");
            AddFieldTranslation(crest, "description", "SILK_THREAD_CREST_DESCRIPTION", "Lança o ferrão para baixo preso por seda. Ao acertar um inimigo, puxa a Hornet até ele para um segundo golpe.");
            AddFieldTranslation(crest, "itemNamePrefix", "SILK_THREAD_CREST_PREFIX", "Fio");
            AddFieldTranslation(crest, "getPromptDesc", "SILK_THREAD_CREST_GET_PROMPT", "Um brasão experimental de pogo preso por seda.");
            AddFieldTranslation(crest, "equipText", "SILK_THREAD_CREST_EQUIP", "Equipar o Brasão do Fio");
            RegisterBattlePrompt();
        }

        internal static void RegisterBattlePrompt()
        {
            Entries.RemoveAll(entry => entry.Sheet == "Prompts" && entry.Key.StartsWith("SILK_THREAD_BATTLE_", StringComparison.Ordinal));
            Entries.Add(new Translation { Sheet = "Prompts", Key = "SILK_THREAD_BATTLE_PROMPT", Value = "Iniciar batalha por turnos" });
            Entries.Add(new Translation { Sheet = "Prompts", Key = "SILK_THREAD_BATTLE_PREVIOUS", Value = "Ação anterior" });
            Entries.Add(new Translation { Sheet = "Prompts", Key = "SILK_THREAD_BATTLE_NEXT", Value = "Ação seguinte" });
            Entries.Add(new Translation { Sheet = "Prompts", Key = "SILK_THREAD_BATTLE_EXECUTE", Value = "Executar" });
            Entries.Add(new Translation { Sheet = "Prompts", Key = "SILK_THREAD_BATTLE_FLEE", Value = "Recuar" });
            Entries.Add(new Translation { Sheet = "Prompts", Key = "SILK_THREAD_BATTLE_SWAP", Value = "Ver brasões 0/3" });
            ApplyTranslations();
        }

        internal static void SetBattleActionLabel(string actionLabel)
        {
            Translation execute = Entries.FirstOrDefault(entry => entry.Sheet == "Prompts" && entry.Key == "SILK_THREAD_BATTLE_EXECUTE");
            if (execute == null)
                return;
            execute.Value = string.IsNullOrWhiteSpace(actionLabel) ? "Executar" : "Executar: " + actionLabel;
            RefreshBattleReminderText();
        }

        internal static void SetBattleCrestSwapLabel(int swapsUsed, bool alreadySwappedThisTurn, bool canSwap)
        {
            Translation swap = Entries.FirstOrDefault(entry => entry.Sheet == "Prompts" && entry.Key == "SILK_THREAD_BATTLE_SWAP");
            if (swap == null)
                return;
            swap.Value = canSwap
                ? "Ver brasões " + swapsUsed + "/3"
                : "Ver brasões";
            RefreshBattleReminderText();
        }

        private static void RefreshBattleReminderText()
        {
            ApplyTranslations();
            foreach (ControlReminder reminder in Resources.FindObjectsOfTypeAll<ControlReminder>())
            {
                if (reminder && reminder.gameObject.scene.IsValid())
                    reminder.ForceUpdateReminderText();
            }
        }

        internal static void ConfigureSacrifice(ToolItemBasic item)
        {
            AddBasicToolFieldTranslation(item, "displayName", "SILK_THREAD_SACRIFICE_NAME", "Sacrifício");
            AddBasicToolFieldTranslation(item, "description", "SILK_THREAD_SACRIFICE_DESCRIPTION", "Quando equipado no Brasão do Fio, converte dano de espinhos e outros perigos ambientais em um pogo normal e 1 ponto de dano. Se estiver com apenas 1 ponto de vida, evita a morte e mantém 1.");
        }

        private static void AddBasicToolFieldTranslation(ToolItemBasic item, string fieldName, string key, string value)
        {
            FieldInfo field = AccessTools.Field(typeof(ToolItemBasic), fieldName);
            if (field == null)
                return;
            LocalisedString text = (LocalisedString)field.GetValue(item);
            string sheet = string.IsNullOrEmpty(text.Sheet) ? "Prompts" : text.Sheet;
            text.Sheet = sheet;
            text.Key = key;
            field.SetValue(item, text);
            Entries.RemoveAll(entry => entry.Key == key);
            Entries.Add(new Translation { Sheet = sheet, Key = key, Value = value });
        }

        private static void AddFieldTranslation(ToolCrest crest, string fieldName, string key, string value)
        {
            FieldInfo field = AccessTools.Field(typeof(ToolCrest), fieldName);
            if (field == null)
                return;
            LocalisedString text = (LocalisedString)field.GetValue(crest);
            if (string.IsNullOrEmpty(text.Sheet))
                return;
            text.Key = key;
            field.SetValue(crest, text);
            Entries.Add(new Translation { Sheet = text.Sheet, Key = key, Value = value });
        }

        internal static void ApplyTranslations()
        {
            try
            {
                _ = Language.CurrentLanguage();
                FieldInfo field = AccessTools.Field(typeof(Language), "_currentEntrySheets");
                var sheets = field == null ? null : field.GetValue(null) as Dictionary<string, Dictionary<string, string>>;
                if (sheets == null)
                    return;
                foreach (Translation entry in Entries)
                {
                    if (sheets.TryGetValue(entry.Sheet, out Dictionary<string, string> values))
                        values[entry.Key] = entry.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Silk Thread Crest localization injection failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch]
    internal static class LanguageSwitchPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(Language).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "SwitchLanguage");
        }

        private static void Postfix()
        {
            CrestLocalization.ApplyTranslations();
        }
    }

    [HarmonyPatch(typeof(HeroController), "DownAttack")]
    internal static class DownAttackPatch
    {
        private static void Postfix(HeroController __instance)
        {
            if (!SilkThreadCrestPlugin.IsThreadCrestEquipped(__instance) || __instance.cState.onGround)
                return;
            SilkThreadMove move = __instance.GetComponent<SilkThreadMove>();
            if (!move)
                move = __instance.gameObject.AddComponent<SilkThreadMove>();
            FieldInfo attackField = AccessTools.Field(typeof(HeroController), "currentDownspike");
            Downspike attack = attackField == null ? null : attackField.GetValue(__instance) as Downspike;
            move.BeginMove(__instance, attack);
        }
    }

    [HarmonyPatch(typeof(HeroController), "NailHitEnemy")]
    internal static class NailHitEnemyPatch
    {
        private static void Postfix(HeroController __instance, HealthManager enemyHealth, HitInstance hitInstance)
        {
            if (!SilkThreadCrestPlugin.IsThreadCrestEquipped(__instance))
                return;
            SilkThreadMove move = __instance.GetComponent<SilkThreadMove>();
            if (move && move.ActiveMove)
                move.SetTarget(enemyHealth, hitInstance);
        }
    }

    [HarmonyPatch(typeof(HeroDownAttack), "OnHitResponded")]
    internal static class DownAttackHitResponsePatch
    {
        private static void Prefix(DamageEnemies.HitResponse hitResponse)
        {
            HeroController hero = HeroController.instance;
            if (!SilkThreadCrestPlugin.IsThreadCrestEquipped(hero) || !hitResponse.HealthManager)
                return;
            SilkThreadMove move = hero.GetComponent<SilkThreadMove>();
            if (move && move.ActiveMove)
                move.SetTarget(hitResponse.HealthManager, hitResponse.Hit);
        }
    }

    [HarmonyPatch(typeof(HeroDownAttack), "OnTriggerEnter2D")]
    internal static class DownAttackNativeHitboxFilterPatch
    {
        private static bool Prefix(Collider2D otherCollider)
        {
            HeroController hero = HeroController.instance;
            if (!SilkThreadCrestPlugin.IsThreadCrestEquipped(hero) || !otherCollider)
                return true;

            SilkThreadMove move = hero.GetComponent<SilkThreadMove>();
            if (!move || !move.ActiveMove || !move.HasCustomHitbox)
                return true;

            bool enemyLayer = (GlobalEnums.PhysLayers)otherCollider.gameObject.layer == GlobalEnums.PhysLayers.ENEMIES;
            bool hasHealthManager = otherCollider.GetComponent<HealthManager>() || otherCollider.GetComponentInParent<HealthManager>();
            return !(enemyLayer || hasHealthManager);
        }
    }

    [HarmonyPatch(typeof(HeroController), "DownspikeBounce")]
    internal static class DownspikeBouncePatch
    {
        private static bool Prefix(HeroController __instance, bool harpoonRecoil, HeroSlashBounceConfig bounceConfig = null)
        {
            if (SilkThreadCrestPlugin.AllowNativeDownspikeBounce)
                return true;
            if (!SilkThreadCrestPlugin.IsThreadCrestEquipped(__instance))
                return true;
            SilkThreadMove move = __instance.GetComponent<SilkThreadMove>();
            if (move && move.ActiveMove && move.HasTarget)
            {
                move.BeginPull();
                return false;
            }

            __instance.DownspikeBounceSlightlyShort(harpoonRecoil);
            SilkThreadCrestPlugin.LogInfo("Silk Thread short pogo started without extra jump height.");
            if (move && move.ActiveMove)
                move.EndMove();
            return false;
        }
    }

    internal sealed class SacrificeHazardCooldown : MonoBehaviour
    {
        internal const float ConsecutiveWindow = 2.5f;
        internal const int ConsecutiveSilkCost = 1;
        private float cooldownUntil;
        private float lastConversionAt = -100f;

        internal bool IsActive => Time.unscaledTime < cooldownUntil;
        internal bool IsConsecutive => Time.unscaledTime - lastConversionAt <= ConsecutiveWindow;

        internal void StartCooldown()
        {
            cooldownUntil = Time.unscaledTime + 0.6f;
        }

        internal void RegisterConversion()
        {
            lastConversionAt = Time.unscaledTime;
        }
    }

    internal sealed class SacrificeHitState
    {
        internal int HealthBefore;
        internal bool WasAtOneHealth;
        internal bool Converted;
        internal int SilkCost;
    }

    [HarmonyPatch(typeof(HeroController), "TakeDamage")]
    internal static class SacrificeHazardPatch
    {
        private static bool Prefix(HeroController __instance, ref int damageAmount, ref GlobalEnums.HazardType hazardType,
            ref GlobalEnums.DamagePropertyFlags damagePropertyFlags, out SacrificeHitState __state)
        {
            __state = null;
            if (!SilkThreadCrestPlugin.IsSacrificeEquipped(__instance) ||
                hazardType == GlobalEnums.HazardType.ENEMY ||
                hazardType == GlobalEnums.HazardType.NON_HAZARD ||
                hazardType == GlobalEnums.HazardType.EXPLOSION)
                return true;

            SacrificeHazardCooldown cooldown = __instance.GetComponent<SacrificeHazardCooldown>();
            if (cooldown && cooldown.IsActive)
                return false;

            PlayerData data = PlayerData.instance;
            if (data == null)
                return true;

            int consecutiveCost = cooldown != null && cooldown.IsConsecutive
                ? SacrificeHazardCooldown.ConsecutiveSilkCost
                : 0;
            if (consecutiveCost > 0 && data.silk < consecutiveCost)
            {
                SilkThreadCrestPlugin.LogInfo("Sacrifício consecutive use was rejected because Hornet lacks " +
                    consecutiveCost + " silk; the environmental damage remains native.");
                return true;
            }

            __state = new SacrificeHitState
            {
                HealthBefore = data.health + data.healthBlue,
                WasAtOneHealth = data.health <= 1,
                Converted = true,
                SilkCost = consecutiveCost
            };
            damageAmount = data.health > 1 ? 1 : 0;
            hazardType = GlobalEnums.HazardType.NON_HAZARD;
            damagePropertyFlags = GlobalEnums.DamagePropertyFlags.None;
            return true;
        }

        private static void Postfix(HeroController __instance, SacrificeHitState __state)
        {
            if (__state == null || !__state.Converted || !__instance || !PlayerData.HasInstance)
                return;

            int healthAfter = PlayerData.instance.health + PlayerData.instance.healthBlue;
            if (!__state.WasAtOneHealth && healthAfter >= __state.HealthBefore)
                return;

            if (__state.SilkCost > 0)
                __instance.TakeSilk(__state.SilkCost);

            SacrificeHazardCooldown cooldown = __instance.GetComponent<SacrificeHazardCooldown>();
            if (!cooldown)
                cooldown = __instance.gameObject.AddComponent<SacrificeHazardCooldown>();
            cooldown.StartCooldown();
            cooldown.RegisterConversion();
            SilkThreadCrestPlugin.StartNormalSacrificePogo(__instance);
            SilkThreadCrestPlugin.LogInfo("Sacrifício converted an environmental hit into one health point of damage (minimum 1 remaining) and a normal pogo; consecutive silk cost=" + __state.SilkCost + ".");
        }
    }

    [HarmonyPatch(typeof(HeroController), "StartRecoil")]
    internal static class TurnBattleNoKnockbackPatch
    {
        private static void Prefix(ref GlobalEnums.CollisionSide __0)
        {
            if (TurnBattleSystem.IsBattleActive)
                __0 = GlobalEnums.CollisionSide.top;
        }
    }
}
