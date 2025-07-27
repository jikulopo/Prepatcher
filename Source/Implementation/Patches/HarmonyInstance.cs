using HarmonyLib;

namespace Prepatcher;

internal static partial class HarmonyPatches
{
    public static Harmony harmony = new("prepatcher");
}
