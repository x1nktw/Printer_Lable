using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LabelPrint.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef migrations</c>.
/// </summary>
public sealed class LabelPrintDbContextFactory : IDesignTimeDbContextFactory<LabelPrintDbContext>
{
    public LabelPrintDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LabelPrintDbContext>()
            .UseSqlite("Data Source=labelprint-design.db")
            .Options;

        return new LabelPrintDbContext(options);
    }
}
