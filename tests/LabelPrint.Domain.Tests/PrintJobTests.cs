using FluentAssertions;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Exceptions;
using LabelPrint.Domain.ValueObjects;

namespace LabelPrint.Domain.Tests;

public class PrintJobTests
{
    [Fact]
    public void HappyPath_Transitions_To_Completed()
    {
        var job = CreateJob();

        job.MarkAsRendering();
        job.Status.Should().Be(PrintJobStatus.Rendering);

        job.MarkAsPrinting();
        job.Status.Should().Be(PrintJobStatus.Printing);

        job.MarkAsCompleted();
        job.Status.Should().Be(PrintJobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_From_Printing_Sets_Reason()
    {
        var job = CreateJob();
        job.MarkAsRendering();
        job.MarkAsPrinting();

        job.MarkAsFailed("Printer offline", isTransient: true);

        job.Status.Should().Be(PrintJobStatus.Failed);
        job.FailureReason.Should().Be("Printer offline");
        job.IsTransientFailure.Should().BeTrue();
    }

    [Fact]
    public void RequeueForRetry_Only_Allows_Transient_Failures()
    {
        var job = CreateJob();
        job.MarkAsRendering();
        job.MarkAsFailed("Bad protocol", isTransient: false);

        var act = () => job.RequeueForRetry();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateReprint_Creates_New_Pending_Job_With_Source()
    {
        var job = CreateJob();
        job.MarkAsRendering();
        job.MarkAsPrinting();
        job.MarkAsCompleted();

        var reprint = job.CreateReprint(Guid.NewGuid(), "Operator");

        reprint.Id.Should().NotBe(job.Id);
        reprint.SourceJobId.Should().Be(job.Id);
        reprint.Status.Should().Be(PrintJobStatus.Pending);
        reprint.VariablesJson.Should().Be(job.VariablesJson);
    }

    [Fact]
    public void Invalid_Transition_Throws()
    {
        var job = CreateJob();
        var act = () => job.MarkAsPrinting();
        act.Should().Throw<DomainException>();
    }

    private static PrintJob CreateJob() => new()
    {
        PrinterId = Guid.NewGuid(),
        Title = "Test",
        VariablesJson = """{"ProductName":"Burger"}"""
    };
}

public class MoneyTests
{
    [Fact]
    public void Normalizes_Currency_To_Upper()
    {
        var money = new Money(10.5m, "rub");
        money.Currency.Should().Be("RUB");
        money.ToString().Should().Be("10.5 RUB");
    }
}

public class LabelSizeTests
{
    [Fact]
    public void Parse_Accepts_Invariant_Format()
    {
        var size = LabelSize.Parse("58x40");
        size.WidthMm.Should().Be(58);
        size.HeightMm.Should().Be(40);
    }
}

public class ProductTemplateResolutionTests
{
    [Fact]
    public void ResolveOrderItemTemplateId_Falls_Back_To_Default()
    {
        var defaultId = Guid.NewGuid();
        var product = new Product
        {
            DefaultTemplateId = defaultId,
            OrderItemTemplateId = null
        };

        product.ResolveOrderItemTemplateId().Should().Be(defaultId);
    }
}
