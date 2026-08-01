using FluentAssertions;
using NetArchTest.Rules;

namespace LabelPrint.ArchitectureTests;

public class PluginCompositionTests
{
    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(LabelPrint.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions).Assembly;

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(LabelPrint.Application.DependencyInjection.ApplicationServiceCollectionExtensions).Assembly;

    [Fact]
    public void PluginLoader_Should_Reside_In_Infrastructure()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveName("PluginLoader")
            .Should()
            .ResideInNamespace("LabelPrint.Infrastructure.Plugins")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_Should_Not_Reference_AssemblyLoadContext()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("System.Runtime.Loader")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ui_ViewModels_Should_Not_Reference_AssemblyLoadContext()
    {
        var result = Types.InAssembly(typeof(LabelPrint.UI.ViewModels.MainViewModel).Assembly)
            .That()
            .ResideInNamespace("LabelPrint.UI.ViewModels")
            .ShouldNot()
            .HaveDependencyOn("System.Runtime.Loader")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
