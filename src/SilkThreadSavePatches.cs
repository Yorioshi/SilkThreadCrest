using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SilkThreadCrest
{
    internal static class SilkThreadSaveData
    {
        internal const string RootProperty = "SilkThreadCrest";
        private const string SidecarDirectory = "SilkThreadCrest";
        private const string SidecarPrefix = "summons-slot-";
        private static readonly FieldInfo ProfileIdField = typeof(GameManager).GetField(
            "profileID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static string InjectIntoSaveJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;
            try
            {
                JObject root = JObject.Parse(json);
                root[RootProperty] = JObject.FromObject(EnemySummonRegistry.ExportSaveData());
                return root.ToString(Formatting.None);
            }
            catch (Exception exception)
            {
                SilkThreadCrestPlugin.LogWarning("Could not append Silk Thread summon data to the native save JSON: " + exception.Message);
                return json;
            }
        }

        internal static void RestoreFromSaveJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                if (!RestoreFromSidecar())
                    EnemySummonRegistry.RestoreFromSaveData(null);
                return;
            }
            try
            {
                JObject root = JObject.Parse(json);
                JToken token = root[RootProperty];
                if (token == null || token.Type == JTokenType.Null)
                {
                    if (RestoreFromSidecar())
                        return;
                    EnemySummonRegistry.RestoreFromSaveData(null);
                    return;
                }
                SilkThreadSummonSaveData data = token.ToObject<SilkThreadSummonSaveData>();
                if ((data == null || data.Invocations == null || data.Invocations.Count == 0) && RestoreFromSidecar())
                    return;
                EnemySummonRegistry.RestoreFromSaveData(data);
            }
            catch (Exception exception)
            {
                if (RestoreFromSidecar())
                    return;
                EnemySummonRegistry.RestoreFromSaveData(null);
                SilkThreadCrestPlugin.LogWarning("Could not restore Silk Thread summon data from the native save: " + exception.Message);
            }
        }

        internal static void WriteSidecar()
        {
            try
            {
                string path = GetSidecarPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonConvert.SerializeObject(EnemySummonRegistry.ExportSaveData(), Formatting.None);
                File.WriteAllText(path, json);
                SilkThreadCrestPlugin.LogInfo("Silk Thread wrote the summon sidecar for save slot " + GetProfileId() + ".");
            }
            catch (Exception exception)
            {
                SilkThreadCrestPlugin.LogWarning("Could not write the summon sidecar: " + exception.GetBaseException().Message);
            }
        }

        internal static void ClearSidecar()
        {
            try
            {
                string path = GetSidecarPath();
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                SilkThreadCrestPlugin.LogWarning("Could not clear the summon sidecar: " + exception.GetBaseException().Message);
            }
        }

        internal static bool RestoreFromSidecar(int profileId)
        {
            try
            {
                string path = GetSidecarPath(profileId);
                if (!File.Exists(path))
                    return false;
                string json = File.ReadAllText(path);
                SilkThreadSummonSaveData data = JsonConvert.DeserializeObject<SilkThreadSummonSaveData>(json);
                if (data == null || data.Invocations == null || data.Invocations.Count == 0)
                    return false;
                EnemySummonRegistry.RestoreFromSaveData(data);
                SilkThreadCrestPlugin.LogInfo("Silk Thread restored summon data from the slot sidecar fallback.");
                return true;
            }
            catch (Exception exception)
            {
                SilkThreadCrestPlugin.LogWarning("Could not restore the summon sidecar: " + exception.GetBaseException().Message);
                return false;
            }
        }

        private static bool RestoreFromSidecar()
        {
            return RestoreFromSidecar(GetProfileId());
        }

        private static string GetSidecarPath(int profileId)
        {
            string directory = Path.Combine(Application.persistentDataPath, SidecarDirectory);
            return Path.Combine(directory, SidecarPrefix + profileId + ".json");
        }

        private static string GetSidecarPath()
        {
            return GetSidecarPath(GetProfileId());
        }

        private static int GetProfileId()
        {
            try
            {
                GameManager manager = GameManager.instance;
                if (manager && ProfileIdField != null)
                    return (int)ProfileIdField.GetValue(manager);
            }
            catch
            {
            }
            return 0;
        }
    }

    [HarmonyPatch]
    internal static class SilkThreadSerializeSavePatch
    {
        private static MethodBase TargetMethod()
        {
            // In this game build, GameManager.GetBytesForSaveData creates the JSON
            // through GameManager.GetJson<T>, not by calling SerializeSaveData directly
            // from the save writer. Hook the exact method used by the live save path.
            MethodInfo genericDefinition = typeof(GameManager).GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .First(method => method.Name == "GetJson" && method.IsGenericMethodDefinition);
            // Harmony is not reliable when an open generic definition is returned here.
            // Patch the exact closed method invoked by GetBytesForSaveData instead.
            return genericDefinition.MakeGenericMethod(typeof(SaveGameData));
        }

        private static void Postfix(SaveGameData __0, ref string __result)
        {
            if (__0 == null)
                return;
            __result = SilkThreadSaveData.InjectIntoSaveJson(__result);
            SilkThreadCrestPlugin.LogInfo("Silk Thread appended summon data to the active native save JSON.");
        }
    }

    [HarmonyPatch]
    internal static class SilkThreadLoadSavePatch
    {
        private static MethodBase TargetMethod()
        {
            return typeof(GameManager).GetMethod(
                "SetLoadedGameData",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(int) },
                modifiers: null);
        }

        private static void Postfix(string __0)
        {
            SilkThreadSaveData.RestoreFromSaveJson(__0);
        }
    }

    [HarmonyPatch]
    internal static class SilkThreadLoadTypedSavePatch
    {
        private static MethodBase TargetMethod()
        {
            return typeof(GameManager).GetMethod(
                "SetLoadedGameData",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(SaveGameData), typeof(int) },
                modifiers: null);
        }

        private static void Postfix(int __1)
        {
            SilkThreadSaveData.RestoreFromSidecar(__1);
        }
    }

    [HarmonyPatch]
    internal static class SilkThreadNewGamePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GameManager), "StartNewGame", new[] { typeof(bool), typeof(bool) });
        }

        private static void Postfix()
        {
            SilkThreadSaveData.ClearSidecar();
            EnemySummonRegistry.RestoreFromSaveData(null);
        }
    }
}
