using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

internal static class CoreMethodDiagnostic
{
    private static int Main(string[] args)
    {
        // Supplemental pure-.NET diagnostic. This is not the Unity/NUnit runner.
        string assemblyDirectory = Path.GetFullPath(args[0]);
        string nunitPath = Path.GetFullPath(args[1]);
        string outputPath = Path.GetFullPath(args[2]);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            if (name.Name == "Luckbound.Core")
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(assemblyDirectory, "Luckbound.Core.dll"));
            return null;
        };
        AssemblyLoadContext.Default.LoadFromAssemblyPath(nunitPath);
        string testsPath = Path.Combine(assemblyDirectory, "Luckbound.Core.Tests.dll");
        Assembly tests = AssemblyLoadContext.Default.LoadFromAssemblyPath(testsPath);
        Type fixtureType = tests.GetType("Luckbound.Tests.Core.Expeditions.EncounterPlanPinV1Tests", true)!;
        MethodInfo[] methods = fixtureType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "NUnit.Framework.TestAttribute"))
            .OrderBy(method => method.Name, StringComparer.Ordinal).ToArray();
        if (methods.Length != 6 || methods.Any(method => method.GetParameters().Length != 0 || method.ReturnType != typeof(void)))
            throw new InvalidOperationException("The exact six parameterless synchronous cases are required.");
        if (fixtureType.GetMethods().Any(method => method.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName is "NUnit.Framework.SetUpAttribute" or "NUnit.Framework.TearDownAttribute" or
                "NUnit.Framework.OneTimeSetUpAttribute" or "NUnit.Framework.OneTimeTearDownAttribute")))
            throw new InvalidOperationException("Lifecycle methods require an actual NUnit runner.");
        object fixture = Activator.CreateInstance(fixtureType)!;
        var results = new List<object>();
        int failed = 0;
        foreach (MethodInfo method in methods)
        {
            try
            {
                method.Invoke(fixture, null);
                results.Add(new { method = method.Name, passed = true, error = (string?)null });
            }
            catch (TargetInvocationException error)
            {
                failed++;
                results.Add(new { method = method.Name, passed = false, error = error.InnerException?.ToString() ?? error.ToString() });
            }
        }
        var report = new
        {
            kind = "Supplemental pure .NET method invocation with NUnit assertions",
            unityExecuted = false, nunitRunnerExecuted = false,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            testsAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(testsPath))).ToLowerInvariant(),
            coreAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(assemblyDirectory, "Luckbound.Core.dll")))).ToLowerInvariant(),
            total = methods.Length, passed = methods.Length - failed, failed, results
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine(JsonSerializer.Serialize(new { total = methods.Length, passed = methods.Length - failed, failed }));
        return failed == 0 ? 0 : 1;
    }
}
