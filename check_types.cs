using System;
using System.Reflection;

var asm = Assembly.LoadFrom("src/AbpDevTools/bin/Debug/net9.0/AbpDevTools.dll");
var types = asm.GetTypes();
Console.WriteLine("=== Types containing 'Clear' ===");
foreach (var t in types)
{
    if (t.Name.Contains("Clear", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {t.FullName} - {t.Namespace}");
    }
}

Console.WriteLine("\n=== All Migrations command types ===");
foreach (var t in types)
{
    if (t.Namespace == "AbpDevTools.Commands.Migrations")
    {
        Console.WriteLine($"  {t.Name}");
    }
}
