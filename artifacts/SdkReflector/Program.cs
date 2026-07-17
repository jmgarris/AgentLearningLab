#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.Reflection;

foreach (var method in typeof(ResponsesClient).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
             .Where(m => m.Name.Contains("Response", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Create", StringComparison.OrdinalIgnoreCase))
             .OrderBy(m => m.Name))
{
    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
    Console.WriteLine($"{method.ReturnType.Name} {method.Name}({parameters})");
}

Console.WriteLine("--- ResponseItem static methods ---");
foreach (var method in typeof(ResponseItem).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.Name))
{
    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
    Console.WriteLine($"{method.ReturnType.Name} {method.Name}({parameters})");
}

Console.WriteLine("--- ResponseTool static methods ---");
foreach (var method in typeof(ResponseTool).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.Name))
{
    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
    Console.WriteLine($"{method.ReturnType.Name} {method.Name}({parameters})");
}

Console.WriteLine("--- CreateResponseOptions properties ---");
foreach (var property in typeof(CreateResponseOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
{
    Console.WriteLine($"{property.PropertyType.Name} {property.Name}");
}
