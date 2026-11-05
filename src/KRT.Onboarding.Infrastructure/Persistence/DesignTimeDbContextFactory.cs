using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KRT.Onboarding.Infrastructure.Persistence;

/// <summary>Usado apenas pelo "dotnet ef" para gerar migrations.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KrtDbContext>
{
    public KrtDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KrtDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=KrtOnboarding;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new KrtDbContext(options);
    }
}
