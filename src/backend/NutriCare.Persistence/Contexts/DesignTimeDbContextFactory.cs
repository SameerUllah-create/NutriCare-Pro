namespace NutriCare.Persistence.Contexts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NutriCareDbContext>
{
    public NutriCareDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=NutriCareImplementationDev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        return new NutriCareDbContext(new DbContextOptionsBuilder<NutriCareDbContext>().UseSqlServer(connection).Options);
    }
}
