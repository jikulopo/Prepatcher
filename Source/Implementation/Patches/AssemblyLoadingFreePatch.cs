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
        if (DataStore.duplicateAssemblies.TryGetValue(filePath, out string? assemblyPath))
        {
            Lg.Verbose($"Loading assembly from redirected: {filePath} -> {assemblyPath}");
            filePath=assemblyPath;
        }

        if (DataStore.assemblies.TryGetValue(filePath, out Assembly? loadedAssembly))
        {
            Lg.Verbose($"Returning prepatcher loaded assembly: {filePath}");
            return loadedAssembly;
        }
        Lg.Verbose($"using loadFrom on non modified asm: {filePath}");
        var asm = Assembly.LoadFrom(filePath);
        filePath=asm.Location;
        if (DataStore.assemblies.TryGetValue(filePath, out Assembly? loadedAssembly2))
        {
            Lg.Verbose($"Returning prepatcher loaded assembly after loadFrom: {filePath}");
            return loadedAssembly2;
        }
        return asm;


    }
}
