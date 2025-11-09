using System.Reflection;
using System.Threading;
using DataAssembly;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal class PrepatcherMod : Mod
{

    internal const string PrepatcherModId = "zetrith.prepatcher";
    internal const string HarmonyModId = "brrainz.harmony";

    public PrepatcherMod(ModContentPack content) : base(content)
    {
        HarmonyPatches.PatchModLoading();
        HarmonyPatches.AddVerboseProfiling();
        HarmonyPatches.PatchGUI();

        HarmonyPatches.SetLoadingStage("Initializing Prepatcher");

        if (DataStore.startedOnce)
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (sender, args) =>
            {
                Lg.Verbose($"ReflectionOnlyAssemblyResolve: {args.RequestingAssembly} requested {args.Name}");
                return null;
            };
            FixWorldCameraPatch.CreateWorldCamera();
            Lg.Info($"Restarted with the patched assembly, going silent.");
            return;
        }

        // EditWindow_Log.wantsToOpen = false;

        DataStore.startedOnce = true;
        Lg.Info($"Starting... (vanilla load took {Time.realtimeSinceStartup}s)");

        HarmonyPatches.SilenceLogging();
        Loader.Reload();

        // Thread abortion counts as a crash
        Prefs.data.resetModsConfigOnCrash = false;

        Thread.CurrentThread.Abort();
    }

    public override string SettingsCategory()
    {
        return "Prepatcher";
    }
}
