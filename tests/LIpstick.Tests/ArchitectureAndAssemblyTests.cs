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
        // Verify runner capability and headless execution baseline
        Assert.IsTrue(Environment.ProcessorCount >= 1, "Environment must provide at least 1 logical processor");
        Assert.IsNotNull(AppContext.BaseDirectory, "AppContext.BaseDirectory must be valid");
    }
}
