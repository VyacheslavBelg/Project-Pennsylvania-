using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Domovoy.Core.Data;

/// <summary>
/// Нужна только для генерации миграций: инструмент EF создаёт контекст вне приложения,
/// где нет ни конфигурации, ни переменных окружения.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DomovoyDbContext>
{
    public DomovoyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DomovoyDbContext>()
            .UseNpgsql("Host=localhost;Database=domovoy;Username=postgres;Password=postgres")
            .Options;

        return new DomovoyDbContext(options);
    }
}
