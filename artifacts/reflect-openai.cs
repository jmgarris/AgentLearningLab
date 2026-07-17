using System;
using System.IO;
using System.Linq;
using System.Reflection;

var asmPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "openai", "2.12.0", "lib", "net10.0", "OpenAI.dll");
var asm = Assembly.LoadFrom(asmPath);
foreach (var type in asm.GetTypes().Where(t => t.Namespace != null && t.Namespace.Contains("Responses") && (t.Name.Contains("Response") || t.Name.Contains("Tool") || t.Name.Contains("Input") || t.Name.Contains("Output"))).OrderBy(t => t.FullName).Take(200))
{
    Console.WriteLine(type.FullName);
}
