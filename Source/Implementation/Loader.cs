using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using LudeonTK;
using Prepatcher.Process;
using Prestarter;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace Prepatcher;

internal static class Loader
{
    internal static Assembly origAsm;
    internal static Assembly newAsm;
    internal static volatile bool restartGame;

    internal static void Reload()
    {
        HarmonyPatches.holdLoading = true;

        try
        {
            Lg.Verbose("Reloading the game");
            DoReload();

            if (GenCommandLine.CommandLineArgPassed("patchandexit"))
                Application.Quit();

            Lg.Info("Done loading");
            restartGame = true;
        }
        catch (Exception e)
        {
            Lg.Error($"Fatal error while reloading: {e}");
        }

        if (restartGame) return;

        UnsafeAssembly.UnsetRefonlys();
        Find.Root.StartCoroutine(MinimalInit.DoInit());
        Find.Root.StartCoroutine(ShowLogConsole());
    }

    private static void DoReload()
    {
        origAsm = typeof(Game).Assembly;

        var set = new AssemblySet();
        set.AddAssembly("RimWorld", AssemblyCollector.AssemblyCSharp, null, typeof(Game).Assembly);

        foreach (var (friendlyName, path) in AssemblyCollector.SystemAssemblyPaths())
        {
            if (AssemblyName.GetAssemblyName(path).Name == AssemblyCollector.AssemblyCSharp)
                continue;

            var addedAsm = set.AddAssembly("System", friendlyName, path, null);
            addedAsm.AllowPatches = false;
        }

        foreach (var (mod, friendlyName, asm) in AssemblyCollector.ModAssemblies())
        {
            var name = asm.GetName().Name;

            // Don't add system assemblies packaged by mods
            // Find original locations of loadFrom deduplicated assemblies
            var location = asm.Location;
            if (set.HasAssembly(name))
            {
                location = GetAssemblyPath(mod, asm);
                if (location != "")
                {
                    if (asm.Location != "")
                    {
                        DataStore.duplicateAssemblies[location] = asm.Location;
                        Lg.Verbose($"Registering duplicate assembly redirect: {location} -> {asm.Location}");
                    }
                    else
                    {
                        var orgAsm = set.nameToAsm[name];
                        DataStore.duplicateAssemblies[location] = orgAsm.SourceLocation;
                        Lg.Verbose($"Registering duplicate assembly redirect: {location} -> {orgAsm.SourceLocation}");
                    }

                }
                else
                {
                    Lg.Error($"Duplicate Assembly not found: {friendlyName}");
                }

                continue;
            }


            // Try to find if assembly is duplicate of already loaded one(eg. by doorstop)
            if (location == "")
            {
                var ass = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == name && a.Location != "");
                location=GetAssemblyPath(mod, asm);
                if (ass != null)
                {
                    var firstAsm = set.AddAssembly(mod.Name, friendlyName, null, ass);
                    firstAsm.ProcessAttributes = true;
                    Reloader.setRefonly.Add(asm);
                    DataStore.duplicateAssemblies[location] = ass.Location;
                    Lg.Verbose($"Registering duplicate assembly redirect: {location} -> {ass.Location}");
                    continue;
                }
                else
                {
                    Lg.Verbose($"No copy of assembly has Location set: {friendlyName}");
                }


            }

            var addedAsm = set.AddAssembly(mod.Name, friendlyName, null, asm);

            if (name.EndsWith("DataAssembly"))
                addedAsm.AllowPatches = false;
            else
                addedAsm.ProcessAttributes = true;
            addedAsm.SourceLocation = location;
        }

        using (StopwatchScope.Measure("Game processing"))
            GameProcessing.Process(set);

        // Reload the assemblies
        Reloader.Reload(
            set,
            LoadAssembly,
            () =>
            {
                HarmonyPatches.SetLoadingStage("Serializing assemblies"); // Point where the mod manager can get opened
            },
            () =>
            {
                HarmonyPatches.SetLoadingStage("Reloading game"); // Point where the mod manager can get opened

                HarmonyPatches.PatchRootMethods();
                Application.logMessageReceivedThreaded -= Log.Notify_MessageReceivedThreadedInternal;
                UnregisterWorkshopCallbacks();
                ClearAssemblyResolve();
            }
        );
    }

    private static void LoadAssembly(ModifiableAssembly asm)
    {
        Lg.Verbose($"Loading assembly: {asm}");
        Assembly loadedAssembly;
        if (asm.symbolsLoaded)
        {
            Lg.Verbose($"Loading assembly with symbols: {asm}: ");
            loadedAssembly = Assembly.Load(asm.Bytes, asm.SymbolBytes);
        }
        else
        {
            loadedAssembly = Assembly.Load(asm.Bytes);
        }


        if (loadedAssembly.GetName().Name == AssemblyCollector.AssemblyCSharp)
        {
            newAsm = loadedAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, _) => loadedAssembly;
        }

        DataStore.assemblies.Add(asm.SourceLocation,loadedAssembly);


        if (GenCommandLine.TryGetCommandLineArg("dumpasms", out var path) && !path.Trim().NullOrEmpty())
        {
            Directory.CreateDirectory(path);
            if (asm.Modified)
                File.WriteAllBytes(Path.Combine(path, asm.AsmDefinition.Name.Name + ".dll"), asm.Bytes!);
        }
    }

    private static IEnumerator ShowLogConsole()
    {
        yield return null;

        LongEventHandler.currentEvent = null;
        Find.WindowStack.Add(new EditWindow_Log { doCloseX = false });
        UIRoot_Prestarter.showManager = false;
    }

    private static void UnregisterWorkshopCallbacks()
    {
        Lg.Verbose("Unregistering workshop callbacks");

        // These hold references to old code and would get called externally by Steam
        Workshop.subscribedCallback?.Unregister();
        Workshop.unsubscribedCallback?.Unregister();
        Workshop.installedCallback?.Unregister();
    }

    private static void ClearAssemblyResolve()
    {
        Lg.Verbose("Clearing AppDomain.AssemblyResolve");

        var asmResolve = AccessTools.Field(typeof(AppDomain), "AssemblyResolve");
        var del = (Delegate)asmResolve.GetValue(AppDomain.CurrentDomain);

        // Handle MonoMod's internal dynamic assemblies
        foreach (var d in del.GetInvocationList().ToList())
        {
            if (d!.Method.DeclaringType!.Namespace!.StartsWith("MonoMod.Utils"))
            {
                foreach (var f in AccessTools.GetDeclaredFields(d.Method.DeclaringType))
                {
                    if (f.FieldType == typeof(Assembly))
                    {
                        var da = (Assembly)f.GetValue(d.Target);
                        Reloader.setRefonly.Add(da);
                    }
                }
            }
        }

        asmResolve.SetValue(AppDomain.CurrentDomain, null);
    }

    //finds original assembly location
    //loadFrom deduplicates assemblies, asm.Location might be location of the first assembly loaded of given identity instead of actual location
    //in case of non loadFrom assemblies which dont have a location tries to find the original location based on order of loaded assemblies/by manually checking the assembly names
    private static string GetAssemblyPath(ModContentPack mod, Assembly asm)
    {
        var files = ModContentPack.GetAllFilesForModPreserveOrder(mod, "Assemblies/", (string e) => e.ToLower() == ".dll",
            null).Select(f => f.Item2.ToString()).ToList();
        if (files.Contains(asm.Location))
            return asm.Location;
        if (files.Count() != mod.assemblies.loadedAssemblies.Count)
        {
            Lg.Error($"Mod: {mod.Name} has assemblies that arent loaded");
            foreach (string file in files)
            {
                try
                {
                    var a = AssemblyName.GetAssemblyName(file);
                    if (a?.FullName == asm.FullName) return file;
                }
                catch (Exception)
                {
                    Lg.Error($"Mod: {mod.Name} failed to compare assembly name of location: {file}");
                }

            }

            return "";
        }
        var i = mod.assemblies.loadedAssemblies.FindIndex(x => x.FullName == asm.FullName);
        if (i != -1)
        {
            return files.ElementAt(i);
        }
        return "";
    }
}
