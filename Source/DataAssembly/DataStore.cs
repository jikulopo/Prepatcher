// This assembly doesn't have any dependencies (apart from system ones) so won't get reloaded
// and can be used to pass data across the reloading barrier

using System;
using System.Collections.Generic;
using System.Reflection;

namespace DataAssembly;

public static class DataStore
{
    public static bool startedOnce;

    public static volatile bool openModManager;
    public static volatile string? loadingStage = null;
    public static Dictionary<string,Assembly> assemblies = new();
    public static Dictionary<string,string> duplicateAssemblies = new();

}
