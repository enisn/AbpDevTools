using System.Reflection;

var cliFxAsm = Assembly.LoadFrom("../src/AbpDevTools/bin/Debug/net9.0/CliFx.dll");

Console.WriteLine("=== ConsoleReader Type ===");
var consoleReaderType = cliFxAsm.GetType("CliFx.Infrastructure.ConsoleReader")!;
Console.WriteLine($"IsValueType: {consoleReaderType.IsValueType}");
Console.WriteLine($"IsByRefLike: {consoleReaderType.IsByRefLike}");
Console.WriteLine("Constructors:");
foreach (var ctor in consoleReaderType.GetConstructors())
{
    Console.WriteLine($"  {string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.FullName))}");
}
Console.WriteLine("Methods:");
foreach (var method in consoleReaderType.GetMethods().Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
{
    Console.WriteLine($"  {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}): {method.ReturnType.Name}");
}

Console.WriteLine("\n=== ConsoleWriter Type ===");
var consoleWriterType = cliFxAsm.GetType("CliFx.Infrastructure.ConsoleWriter")!;
Console.WriteLine($"IsValueType: {consoleWriterType.IsValueType}");
Console.WriteLine($"IsByRefLike: {consoleWriterType.IsByRefLike}");
Console.WriteLine("Constructors:");
foreach (var ctor in consoleWriterType.GetConstructors())
{
    Console.WriteLine($"  {string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.FullName))}");
}
Console.WriteLine("Methods:");
foreach (var method in consoleWriterType.GetMethods().Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
{
    Console.WriteLine($"  {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}): {method.ReturnType.Name}");
}
