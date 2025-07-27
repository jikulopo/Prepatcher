using System.Linq;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using RimWorld.Planet;

namespace Prepatcher;

public static class FixWorldCameraPatch
{

    public static void PrefixCheckActivateWorldCamera()
    {
        HarmonyPatches.harmony.Patch(typeof(WorldRenderer).GetMethod("CheckActivateWorldCamera"),
            prefix: new HarmonyMethod(typeof(FixWorldCameraPatch), nameof(CreateWorldCamera)));
    }

    private static void CreateWorldCamera()
    {
        WorldCameraManager.worldCameraInt = WorldCameraManager.CreateWorldCamera();
        WorldCameraManager.worldSkyboxCameraInt = WorldCameraManager.CreateWorldSkyboxCamera(WorldCameraManager.worldCameraInt);
        WorldCameraManager.worldCameraDriverInt = WorldCameraManager.worldCameraInt.GetComponent<WorldCameraDriver>();
        HarmonyPatches.harmony.Unpatch(typeof(WorldRenderer).GetMethod("CheckActivateWorldCamera"),
            HarmonyPatchType.Prefix
            );
        //Lg.Info("UnPatched");
    }

    [FreePatch]
    public static void DontInitWorldCamera(ModuleDefinition module)
    {
        var type = module.GetType($"RimWorld.Planet.WorldCameraManager");
        var method = type.GetStaticConstructor();
        Collection<Instruction> instructions = new();
        bool done = false;
        foreach (var inst in method.Body.Instructions)
        {
            if (inst.ToString().Contains("CreateWorldCamera"))
            {
                inst.OpCode = OpCodes.Ret;
                inst.Operand = null;
                done = true;
                instructions.Add(inst);
            }
            if(!done)instructions.Add(inst);

        }
        method.Body.instructions =  instructions;
        /*foreach (var inst in method.Body.Instructions)
        {
            Lg.Info(inst);
        }*/
    }

    [FreePatchAll]
    public static bool RenameWorldCamera(ModuleDefinition module)
    {
        bool res= UpdateModuleReferences(module,"RimWorld.Planet.WorldCameraDriver","RimWorld.Planet.WorldCameraDriverReplaced");
        if(res)
            Lg.Info($"Renamed type RimWorld.Planet.WorldCameraDriver to RimWorld.Planet.WorldCameraDriverReplaced in assembly {module.Name}");
        return res;

    }

    private static bool UpdateTypeReferences(TypeDefinition type, string oldFullName, string newFullName)
    {
        //ai generated because lazy
        bool modified = false;

        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;

            foreach (var instr in method.Body.Instructions)
            {
                if (instr.Operand is MethodReference mr && mr.DeclaringType.FullName == oldFullName)
                {
                    mr.DeclaringType.Name = newFullName.Split('.').Last();
                    modified = true;
                }
                else if (instr.Operand is FieldReference fr && fr.DeclaringType.FullName == oldFullName)
                {
                    fr.DeclaringType.Name = newFullName.Split('.').Last();
                    modified = true;
                }
                else if (instr.Operand is TypeReference tr && tr.FullName == oldFullName)
                {
                    tr.Name = newFullName.Split('.').Last();
                    modified = true;
                }
            }
        }

        // Recursively handle nested types
        foreach (var nested in type.NestedTypes)
        {
            if (UpdateTypeReferences(nested, oldFullName, newFullName))
            {
                modified = true;
            }
        }

        return modified;
    }

    private static bool UpdateModuleReferences(ModuleDefinition module, string oldFullName, string newFullName)
    {
        //ai generated because lazy
        bool modified = false;

        foreach (var type in module.Types)
        {
            if (UpdateTypeReferences(type, oldFullName, newFullName))
            {
                modified = true;
            }
        }

        return modified;
    }
}
