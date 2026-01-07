using System.Reflection;
using System.Threading;
using DataAssembly;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{

    internal const string PrepatcherModId = "jikulopo.prepatcher";
    internal const string HarmonyModId = "brrainz.harmony";

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        if(!DataStore.startedOnce)
            Lg.Info($"Starting... (vanilla load took {Time.realtimeSinceStartup}s)");
        HarmonyPatches.PatchModLoading();
        HarmonyPatches.AddVerboseProfiling();
        HarmonyPatches.PatchGUI();

        HarmonyPatches.SetLoadingStage("Initializing Prepatcher");

        if (DataStore.startedOnce)
        {
            foreach (var log in DataStore.logsToPass)
            {
                if (log.Item1 == "info")
                {
                    Lg.Info($"Before reload: {log.Item2}");
                }
                else
                {
                    Lg.Error($"Before reload: {log.Item2}");
                }
            }
            DataStore.logsToPass.Clear();
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, args) =>
            {
                Lg.Verbose($"ReflectionOnlyAssemblyResolve: {args.RequestingAssembly} requested {args.Name}");
                return null;
            };
            FixWorldCameraPatch.CreateWorldCamera();
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        HarmonyPatches.SilenceLogging();
        Loader.Reload();
        DataStore.startedOnce = true;
        // Thread abortion counts as a crash
        Prefs.data.resetModsConfigOnCrash = false;

        Thread.CurrentThread.Abort();
    }

    public override string SettingsCategory()
    {
        return "Prepatcher";
    }
}
