using System.IO;
using System.Reflection;
using DataAssembly;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using Verse;

namespace Prepatcher;

public static class AssemblyLoadingFreePatch
{
    [FreePatch]
    static void ReplaceAssemblyLoading(ModuleDefinition module)
    {
        var type = module.GetType($"{nameof(Verse)}.{nameof(ModAssemblyHandler)}");
        var method = type.FindMethod(nameof(ModAssemblyHandler.ReloadAll));

        foreach (var inst in method.Body.Instructions)
            if (inst.Operand is MethodReference { Name: nameof(Assembly.LoadFrom) })
                inst.Operand = module.ImportReference(typeof(AssemblyLoadingFreePatch).GetMethod(nameof(LoadFile)));
    }

    public static Assembly LoadFile(string filePath)
    {
        //FileLog.Log($"loadFile path: {filePath}");
        if (DataStore.duplicateAssemblies.TryGetValue(filePath, out string? assemblyPath))
        {
            //FileLog.Log($"Loading assembly from redirected: {filePath} -> {assemblyPath}");
            filePath=assemblyPath;
        }

        if (DataStore.assemblies.TryGetValue(filePath, out Assembly? loadedAssembly))
        {
            return loadedAssembly;
        }
        return Assembly.LoadFrom(filePath);
        /*var rawAssembly = File.ReadAllBytes(filePath);

        var fileInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath)) + ".pdb");
        if (fileInfo.Exists)
        {
            var rawSymbolStore = File.ReadAllBytes(fileInfo.FullName);
            return AppDomain.CurrentDomain.Load(rawAssembly, rawSymbolStore);
        }
        else
        {
            return AppDomain.CurrentDomain.Load(rawAssembly);
        }*/
    }
}
