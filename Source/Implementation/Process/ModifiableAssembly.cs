using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Verse;

namespace Prepatcher.Process;

public class ModifiableAssembly
{
    public string OwnerName { get; }
    public string FriendlyName { get; }

    public Assembly? SourceAssembly { get; set; }
    public AssemblyDefinition AsmDefinition { get; }
    public ModuleDefinition ModuleDefinition => AsmDefinition.MainModule;

    public bool ProcessAttributes { get; set; }
    public bool NeedsReload => needsReload || Modified;
    public bool Modified { get; set; }
    public bool AllowPatches { get; set; } = true;

    public byte[]? Bytes { get; private set; }
    private byte[]? RawBytes { get; }

    public string SourceLocation { get; set; }

    private bool needsReload;

    public bool symbolsLoaded = false;

    public byte[]? SymbolBytes { get; private set; }

    public ModifiableAssembly(string ownerName, string friendlyName, Assembly sourceAssembly,
        IAssemblyResolver resolver)
    {
        OwnerName = ownerName;
        FriendlyName = friendlyName;
        SourceAssembly = sourceAssembly;
        SourceLocation = sourceAssembly.Location;
        RawBytes = UnsafeAssembly.GetRawData(sourceAssembly);
        if (sourceAssembly.Location != "")
        {
            try
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(sourceAssembly.Location, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = true,
                    InMemory = true
                });
                symbolsLoaded = true;
                CheckSymbols();


            }
            catch (Exception e)
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(sourceAssembly.Location, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = false,
                    InMemory = true
                });
            }
        }
        else
        {
            try
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(
                    new MemoryStream(RawBytes),
                    new ReaderParameters
                    {
                        AssemblyResolver = resolver,
                        ReadSymbols = true,
                    });
                symbolsLoaded = true;
               CheckSymbols();
            }
            catch (Exception e)
            {
                AsmDefinition = AssemblyDefinition.ReadAssembly(
                    new MemoryStream(RawBytes),
                    new ReaderParameters
                    {
                        AssemblyResolver = resolver,
                    });
            }

        }
    }

    public ModifiableAssembly(string ownerName, string friendlyName, string path, IAssemblyResolver resolver)
    {
        OwnerName = ownerName;
        FriendlyName = friendlyName;
        try
        {
            AsmDefinition = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = true,
                InMemory = true
            });
            symbolsLoaded = true;
            CheckSymbols();


        }
        catch (Exception e)
        {
            AsmDefinition = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = false,
                InMemory = true
            });
        }
        SourceLocation = path;
    }

    public void SerializeToByteArray()
    {
        if (RawBytes != null && !Modified)
        {
            Lg.Verbose($"Assembly not modified, skipping serialization: {FriendlyName}");
            Bytes = RawBytes;
            return;
        }

        Lg.Verbose($"Serializing: {FriendlyName}");
        var stream = new MemoryStream();
        if (symbolsLoaded)
        {
            Lg.Verbose($"Serializing assembly with symbols loaded: {FriendlyName} {AsmDefinition.MainModule.symbol_reader.ToString()}");
            var symbolsStream = new MemoryStream();
            AsmDefinition.Write(stream,
                new WriterParameters
                {
                    SymbolStream = symbolsStream,
                    SymbolWriterProvider = new PdbWriterProvider()
                });
            SymbolBytes = symbolsStream.ToArray();

        }
        else
        {
            AsmDefinition.Write(stream);
        }

        Bytes = stream.ToArray();

    }

    public void SetSourceRefOnly()
    {
        Lg.Verbose($"Setting refonly: {FriendlyName}");
        UnsafeAssembly.SetReflectionOnly(SourceAssembly!, true);
    }

    public void SetNeedsReload()
    {
        needsReload = true;
    }

    public override string ToString()
    {
        return FriendlyName;
    }

    private void CheckSymbols()
    {
        if (AsmDefinition.MainModule.symbol_reader is EmbeddedPortablePdbReader mainEmbedded)
        {
            AsmDefinition.MainModule.symbol_reader = mainEmbedded.reader;

        }

        if (!AsmDefinition.Modules.NullOrEmpty())
        {
            foreach (var module in AsmDefinition.Modules)
            {
                if (module.symbol_reader is EmbeddedPortablePdbReader embedded)
                {
                    module.symbol_reader = embedded.reader;
                }
            }
        }


        if (AsmDefinition.MainModule.symbol_reader is NativePdbReader)
        {
            symbolsLoaded = false;
        }
    }
}
