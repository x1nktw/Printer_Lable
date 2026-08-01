using FluentAssertions;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Services;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.Application.Tests;

public class PrintQueueServiceTests
{
    [Fact]
    public async Task ListAsync_Returns_Active_Queue_Jobs()
    {
        var uow = new InMemoryUnitOfWork();
        var printerId = Guid.NewGuid();
        await uow.Printers.AddAsync(new Printer { Id = printerId, Name = "Virtual", IsActive = true });
        await uow.PrintJobs.AddAsync(new PrintJob { PrinterId = printerId, Title = "Pending job" });
        var completed = new PrintJob { PrinterId = printerId, Title = "Done job" };
        completed.MarkAsRendering();
        completed.MarkAsPrinting();
        completed.MarkAsCompleted();
        await uow.PrintJobs.AddAsync(completed);

        var service = CreateService(uow);
        var result = await service.ListAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].PrinterName.Should().Be("Virtual");
    }

    [Fact]
    public async Task CancelAsync_Cancels_Pending_Job()
    {
        var uow = new InMemoryUnitOfWork();
        var job = new PrintJob { PrinterId = Guid.NewGuid(), Title = "To cancel" };
        await uow.PrintJobs.AddAsync(job);

        var service = CreateService(uow);
        var result = await service.CancelAsync(job.Id);

        result.IsSuccess.Should().BeTrue();
        var updated = await uow.PrintJobs.GetByIdAsync(job.Id);
        updated!.Status.Should().Be(PrintJobStatus.Cancelled);
    }

    [Fact]
    public async Task RetryAsync_Requeues_Transient_Failed_Job()
    {
        var uow = new InMemoryUnitOfWork();
        var job = new PrintJob { PrinterId = Guid.NewGuid(), Title = "Failed" };
        job.MarkAsRendering();
        job.MarkAsFailed("offline", isTransient: true);
        await uow.PrintJobs.AddAsync(job);

        var service = CreateService(uow);
        var result = await service.RetryAsync(job.Id);

        result.IsSuccess.Should().BeTrue();
        var updated = await uow.PrintJobs.GetByIdAsync(job.Id);
        updated!.Status.Should().Be(PrintJobStatus.Pending);
        updated.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task ReprintJobAsync_Creates_New_Pending_Job()
    {
        var uow = new InMemoryUnitOfWork();
        var job = new PrintJob
        {
            PrinterId = Guid.NewGuid(),
            Title = "Original",
            VariablesJson = """{"Sku":"A1"}"""
        };
        job.MarkAsRendering();
        job.MarkAsPrinting();
        job.MarkAsCompleted();
        await uow.PrintJobs.AddAsync(job);

        var service = CreateService(uow);
        var result = await service.ReprintJobAsync(job.Id);

        result.IsSuccess.Should().BeTrue();
        var reprint = await uow.PrintJobs.GetByIdAsync(result.Value);
        reprint!.Status.Should().Be(PrintJobStatus.Pending);
        reprint.SourceJobId.Should().Be(job.Id);
    }

    [Fact]
    public async Task TryClaimNext_Returns_Highest_Priority_Pending_Job()
    {
        var uow = new InMemoryUnitOfWork();
        var printerId = Guid.NewGuid();

        await uow.PrintJobs.AddAsync(new PrintJob
        {
            PrinterId = printerId,
            Title = "Low",
            Priority = 0,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        await uow.PrintJobs.AddAsync(new PrintJob
        {
            PrinterId = printerId,
            Title = "High",
            Priority = 10,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var claimed = await uow.PrintJobs.TryClaimNextAsync(printerId, Guid.Empty);
        claimed!.Title.Should().Be("High");
    }

    private static PrintQueueService CreateService(InMemoryUnitOfWork uow) =>
        new(uow, new FakeUserSession(), NullLogger<PrintQueueService>.Instance);
}

internal sealed class FakeUserSession : IUserSession
{
    public Guid? CurrentUserId { get; } = Guid.NewGuid();
    public string? CurrentUserName { get; } = "Tester";
    public UserRole? CurrentUserRole { get; } = UserRole.Operator;
    public bool IsSignedIn => true;
}

public class PrintHistoryServiceTests
{
    [Fact]
    public async Task ReprintAsync_From_History_Creates_Queue_Job()
    {
        var uow = new InMemoryUnitOfWork();
        var printerId = Guid.NewGuid();
        await uow.Printers.AddAsync(new Printer { Id = printerId, Name = "P1", IsActive = true });

        var sourceJob = new PrintJob
        {
            PrinterId = printerId,
            TemplateId = Guid.NewGuid(),
            Title = "Label",
            VariablesJson = "{}"
        };
        sourceJob.MarkAsRendering();
        sourceJob.MarkAsPrinting();
        sourceJob.MarkAsCompleted();
        await uow.PrintJobs.AddAsync(sourceJob);

        var history = new PrintHistory
        {
            PrintJobId = sourceJob.Id,
            PrinterId = printerId,
            TemplateId = sourceJob.TemplateId,
            Description = sourceJob.Title,
            Status = PrintJobStatus.Completed,
            VariablesJson = sourceJob.VariablesJson
        };
        await uow.PrintHistory.AddAsync(history);

        var service = new PrintHistoryService(uow, new FakeUserSession(), NullLogger<PrintHistoryService>.Instance);
        var result = await service.ReprintAsync(history.Id);

        result.IsSuccess.Should().BeTrue();
        var job = await uow.PrintJobs.GetByIdAsync(result.Value);
        job!.Status.Should().Be(PrintJobStatus.Pending);
        job.SourceJobId.Should().Be(sourceJob.Id);
    }
}
