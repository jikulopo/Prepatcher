using System.Linq;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using RimWorld;
using RimWorld.Planet;

namespace Prepatcher;

public static class FixWorldCameraPatch
{

    public static void PrefixCheckActivateWorldCamera()
    {
        HarmonyPatches.harmony.Patch(typeof(WorldRenderer).GetMethod("CheckActivateWorldCamera"),
            prefix: new HarmonyMethod(typeof(FixWorldCameraPatch), nameof(CreateWorldCamera)));
        HarmonyPatches.harmony.Patch(typeof(WorldInterface).GetMethod("Reset"),
            prefix: new HarmonyMethod(typeof(FixWorldCameraPatch), nameof(CreateWorldCamera)));
    }

    private static void CreateWorldCamera()
    {
        WorldCameraManager.worldCameraInt = WorldCameraManager.CreateWorldCamera();
        WorldCameraManager.worldSkyboxCameraInt = WorldCameraManager.CreateWorldSkyboxCamera(WorldCameraManager.worldCameraInt);
        WorldCameraManager.worldCameraDriverInt = WorldCameraManager.worldCameraInt.GetComponent<WorldCameraDriver>();
        HarmonyPatches.harmony.Unpatch(typeof(WorldInterface).GetMethod("Reset"),
            HarmonyPatchType.Prefix
            );
        HarmonyPatches.harmony.Unpatch(typeof(WorldRenderer).GetMethod("CheckActivateWorldCamera"),
            HarmonyPatchType.Prefix
        );
        HarmonyPatches.UnSilenceLogging();
        HarmonyPatches.UnPatchGUI();
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
        string newNameOnly = newFullName.Split('.').Last();
        string newNamespace = newFullName.Replace("." + newNameOnly, "");

        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;

            foreach (var instr in method.Body.Instructions)
            {
                if (instr.Operand is MethodReference mr && mr.DeclaringType.FullName == oldFullName)
                {
                    mr.DeclaringType.Name = newNameOnly;
                    modified = true;
                }
                else if (instr.Operand is FieldReference fr && fr.DeclaringType.FullName == oldFullName)
                {
                    fr.DeclaringType.Name = newNameOnly;
                    modified = true;
                }
                else if (instr.Operand is TypeReference tr && tr.FullName == oldFullName)
                {
                    tr.Name = newNameOnly;
                    modified = true;
                }
            }


        }

        foreach (var member in type.Methods.Cast<ICustomAttributeProvider>()
                     .Concat(type.Fields)
                     .Concat(type.Properties)
                     .Concat(type.Events)
                     .Concat(new[] { type }))
        {
            if (!member.HasCustomAttributes) continue;

            foreach (var attr in member.CustomAttributes)
            {
                if (attr.AttributeType.FullName == oldFullName)
                {
                    attr.AttributeType.Name = newNameOnly;
                    modified = true;
                }

                // Handle constructor arguments and named arguments if they reference the type
                for (int i = 0; i < attr.ConstructorArguments.size; i++)
                {
                    var arg = attr.ConstructorArguments.ElementAt(i);

                    if (arg.Type.FullName == oldFullName)
                    {
                        arg = new CustomAttributeArgument(
                            new TypeReference(newNamespace, newNameOnly, type.Resolve().Module, arg.Type.Scope),
                            arg.Value
                        );
                        attr.ConstructorArguments.RemoveAt(i);
                        attr.ConstructorArguments.Insert(i, arg);
                        modified = true;
                    }
                    else if (arg.Value is TypeReference tr && tr.FullName == oldFullName)
                    {
                        var newTypeRef = new TypeReference(newNamespace, newNameOnly, tr.Resolve().module, tr.Scope);
                        arg = new CustomAttributeArgument(arg.Type, newTypeRef);
                        attr.ConstructorArguments.RemoveAt(i);
                        attr.ConstructorArguments.Insert(i, arg);
                        modified = true;
                    }
                }

                foreach (var namedArg in attr.Properties)
                {
                    if (namedArg.Argument.Type.FullName == oldFullName)
                    {
                        namedArg.Argument.Type.Name = newNameOnly;
                        modified = true;
                    }
                }

                foreach (var namedArg in attr.Fields)
                {
                    if (namedArg.Argument.Type.FullName == oldFullName)
                    {
                        namedArg.Argument.Type.Name = newNameOnly;
                        modified = true;
                    }
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
