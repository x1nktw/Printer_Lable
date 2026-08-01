using FluentAssertions;
using NetArchTest.Rules;

namespace LabelPrint.ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(LabelPrint.Domain.Common.EntityBase).Assembly;

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(LabelPrint.Application.DependencyInjection.ApplicationServiceCollectionExtensions).Assembly;

    private static readonly System.Reflection.Assembly PluginsAssembly =
        typeof(LabelPrint.Plugins.Abstractions.Printing.IPrinterGateway).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Layers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LabelPrint.Application",
                "LabelPrint.Infrastructure",
                "LabelPrint.Infrastructure.Printing",
                "LabelPrint.Infrastructure.FrontPad",
                "LabelPrint.UI",
                "LabelPrint.Plugins.Abstractions",
                "Avalonia",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Ui()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LabelPrint.Infrastructure",
                "LabelPrint.Infrastructure.Printing",
                "LabelPrint.Infrastructure.FrontPad",
                "LabelPrint.UI",
                "Avalonia",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void PluginsAbstractions_Should_Only_Depend_On_Domain()
    {
        var result = Types.InAssembly(PluginsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LabelPrint.Application",
                "LabelPrint.Infrastructure",
                "LabelPrint.Infrastructure.Printing",
                "LabelPrint.Infrastructure.FrontPad",
                "LabelPrint.UI",
                "Avalonia",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ui_ViewModels_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(LabelPrint.UI.ViewModels.MainViewModel).Assembly)
            .That()
            .ResideInNamespace("LabelPrint.UI.ViewModels")
            .ShouldNot()
            .HaveDependencyOnAny(
                "LabelPrint.Infrastructure",
                "LabelPrint.Infrastructure.Printing",
                "LabelPrint.Infrastructure.FrontPad",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
