using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

class DecompileSummary
{
    static void Main()
    {
        var asm = AssemblyDefinition.ReadAssembly(@"C:\GOG Games\Hollow Knight Silksong\BepInEx\plugins\CarrieForle-CustomTranslation\CustomTranslation.dll");
        foreach (var type in asm.MainModule.Types)
        {
            Console.WriteLine("TYPE: " + type.FullName);
            foreach (var m in type.Methods)
            {
                Console.WriteLine("  METHOD: " + m.Name);
            }
        }
    }
}
