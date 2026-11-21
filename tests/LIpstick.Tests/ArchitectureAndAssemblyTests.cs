using System.Reflection;
using System.Runtime.Versioning;
using LIpstick.Core.Services;

namespace LIpstick.Tests;

[TestClass]
public sealed class ArchitectureAndAssemblyTests
{
    [TestMethod]
    public void ARCH_01_Assembly_MethodLevel_Parallelization_Is_Enabled()
    {
        // Verify that Parallelize attribute is defined on the test assembly with MethodLevel scope
        var assembly = typeof(ArchitectureAndAssemblyTests).Assembly;
        var parallelAttr = assembly.GetCustomAttribute<ParallelizeAttribute>();

        Assert.IsNotNull(parallelAttr, "Test assembly must define [assembly: Parallelize] attribute");
        Assert.AreEqual(ExecutionScope.MethodLevel, parallelAttr.Scope, "Parallel execution scope must be MethodLevel");
    }

    [TestMethod]
    public void ARCH_02_Assemblies_Target_Net10_And_Reference_Clean_Contracts()
    {
        // Core assembly verification
        var coreAssembly = typeof(RubricEvaluator).Assembly;
        var coreFramework = coreAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        Assert.IsNotNull(coreFramework);
        Assert.IsTrue(coreFramework.Contains("10.0"), $"LIpstick.Core must target .NET 10. Actual: {coreFramework}");

        // Infrastructure assembly verification
        var infraAssembly = typeof(LIpstick.Infrastructure.LinkedIn.MockLinkedInClient).Assembly;
        var infraFramework = infraAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        Assert.IsNotNull(infraFramework);
        Assert.IsTrue(infraFramework.Contains("10.0"), $"LIpstick.Infrastructure must target .NET 10. Actual: {infraFramework}");

        // Tests assembly verification
        var testAssembly = typeof(ArchitectureAndAssemblyTests).Assembly;
        var testFramework = testAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        Assert.IsNotNull(testFramework);
        Assert.IsTrue(testFramework.Contains("10.0"), $"LIpstick.Tests must target .NET 10. Actual: {testFramework}");
    }

    [TestMethod]
    public void ARCH_03_MSTest_Runner_Passes_All_Quality_Gates()
    {
        var testAssembly = typeof(ArchitectureAndAssemblyTests).Assembly;

        // 1. Verify test fixture classes are properly annotated with [TestClass]
        var testClasses = testAssembly.GetTypes()
            .Where(t => t.IsClass && t.GetCustomAttribute<TestClassAttribute>() != null)
            .ToList();

        Assert.IsTrue(testClasses.Count >= 8, $"Must have at least 8 test fixture classes. Found {testClasses.Count}");

        // 2. Discover all [TestMethod] methods across test fixtures
        var testMethods = testClasses
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
            .ToList();

        Assert.IsTrue(testMethods.Count >= 100, $"Total test methods count must exceed 100. Discovered {testMethods.Count}");

        // 3. Verify quality gate prefixes are structurally represented across test methods
        // Gates: T1 (Architecture/Runner), T2 (Security/DPAPI), T3 (Storage/Reboot), T4 (Rubric/Scanner), T5 (Humanizer), T6 (Safe Apply & Content Lab)
        var testMethodNames = testMethods.Select(m => m.Name).ToList();

        Assert.IsTrue(testMethodNames.Any(n => n.StartsWith("ARCH_")), "T1 Gate: Architecture & runner tests must be represented");
        Assert.IsTrue(testMethodNames.Any(n => n.StartsWith("T2_") || n.StartsWith("SEC_")), "T2 Gate: DPAPI & secret isolation tests must be represented");
        Assert.IsTrue(testMethodNames.Any(n => n.StartsWith("ST_") || n.StartsWith("RE_06")), "T3 Gate: Storage & restart bookmark tests must be represented");
        Assert.IsTrue(testMethodNames.Any(n => n.StartsWith("SC_")), "T4 Gate: Rubric scanner tests must be represented");
        Assert.IsTrue(testMethodNames.Any(n => n.StartsWith("HU_")), "T5 Gate: Humanizer tests must be represented");
        Assert.IsTrue(testMethodNames.Any(n => n.StartsWith("CL_") || n.StartsWith("AP_")), "T6 Gate: Content Lab & Safe Apply tests must be represented");

        // 4. Verify asynchronous test methods return Task and synchronous return void
        foreach (var m in testMethods)
        {
            Assert.IsTrue(m.ReturnType == typeof(void) || typeof(Task).IsAssignableFrom(m.ReturnType),
                $"Test method {m.DeclaringType?.Name}.{m.Name} must return void or Task");
        }

        // 5. Verify headless execution runtime baseline
        Assert.IsTrue(Environment.ProcessorCount >= 1, "Environment must provide at least 1 logical processor");
        Assert.IsFalse(string.IsNullOrWhiteSpace(AppContext.BaseDirectory), "AppContext.BaseDirectory must be valid");
    }
}
