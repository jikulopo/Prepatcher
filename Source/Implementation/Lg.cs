using DataAssembly;
using UnityEngine;
using Verse;

namespace Prepatcher;

internal static class Lg
{
    private const string CmdArgVerbose = "verbose";
    static Lg()
    {
        _infoFunc = msg =>
        {

            Log.Message($"Prepatcher: {msg}");
            if(!DataStore.startedOnce) DataStore.logsToPass.Add(("info", msg));
        };
        _errorFunc = msg =>
        {
            Log.Error($"Prepatcher Error: {msg}");
            if(!DataStore.startedOnce) DataStore.logsToPass.Add(("error", msg));
        };

        if (GenCommandLine.CommandLineArgPassed(CmdArgVerbose))
            Lg._verboseFunc = msg => Log.Message($"Prepatcher Verbose: {msg}");
    }
    internal static Action<object>? _infoFunc;
    internal static Action<object>? _errorFunc;
    internal static Action<object>? _verboseFunc;

    internal static void Info(object msg) => _infoFunc?.Invoke(msg);

    internal static void Error(object msg) => _errorFunc?.Invoke(msg);

    internal static void Verbose(object msg) => _verboseFunc?.Invoke(msg);
}
