using Domovoy.Core.Domain;
using Domovoy.Core.Reference;
using Microsoft.EntityFrameworkCore;

namespace Domovoy.Core.Data;

public class DomovoyDbContext(DbContextOptions<DomovoyDbContext> options) : DbContext(options)
{
    // Ядро
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<ManagingOrganization> ManagingOrganizations => Set<ManagingOrganization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserBuildingLink> UserBuildingLinks => Set<UserBuildingLink>();
    public DbSet<DialogState> DialogStates => Set<DialogState>();
    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
    public DbSet<Request> Requests => Set<Request>();

    // Переменная часть
    public DbSet<ProblemCategory> ProblemCategories => Set<ProblemCategory>();
    public DbSet<ResponsibilityZone> ResponsibilityZones => Set<ResponsibilityZone>();
    public DbSet<CategoryResponsibility> CategoryResponsibilities => Set<CategoryResponsibility>();
    public DbSet<NormativeDeadline> NormativeDeadlines => Set<NormativeDeadline>();
    public DbSet<ClarifyingOption> ClarifyingOptions => Set<ClarifyingOption>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Address>(e =>
        {
            e.Property(x => x.Region).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(200);
            e.Property(x => x.Street).HasMaxLength(200);
            e.Property(x => x.House).HasMaxLength(50);
            e.Property(x => x.FiasId).HasMaxLength(64);
            e.Property(x => x.SearchText).HasMaxLength(600);
            e.HasIndex(x => x.SearchText);
        });

        b.Entity<Building>(e =>
        {
            e.HasOne(x => x.Address).WithOne(a => a.Building)
                .HasForeignKey<Building>(x => x.AddressId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ManagingOrganization).WithMany(m => m.Buildings)
                .HasForeignKey(x => x.ManagingOrganizationId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ManagingOrganization>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.Inn).HasMaxLength(20);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.EmergencyPhone).HasMaxLength(50);
        });

        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.MaxUserId).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200);
        });

        b.Entity<UserBuildingLink>(e =>
        {
            e.HasOne(x => x.AppUser).WithMany(u => u.Links)
                .HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Building).WithMany(x => x.Links)
                .HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AppUserId, x.BuildingId }).IsUnique();
            e.Property(x => x.Apartment).HasMaxLength(20);
        });

        b.Entity<DialogState>(e =>
        {
            e.HasOne(x => x.AppUser).WithOne(u => u.DialogState)
                .HasForeignKey<DialogState>(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Step).HasMaxLength(100);
        });

        b.Entity<TelemetryEvent>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.CategoryCode).HasMaxLength(100);
            e.HasIndex(x => new { x.Name, x.OccurredAt });
        });

        b.Entity<ProblemCategory>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(300);
        });

        b.Entity<ResponsibilityZone>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(300);
        });

        b.Entity<CategoryResponsibility>(e =>
        {
            e.HasOne(x => x.ProblemCategory).WithMany(c => c.Responsibilities)
                .HasForeignKey(x => x.ProblemCategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ResponsibilityZone).WithMany(z => z.Responsibilities)
                .HasForeignKey(x => x.ResponsibilityZoneId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClarifyingOption>(e =>
        {
            e.HasOne(x => x.ProblemCategory).WithMany(c => c.ClarifyingOptions)
                .HasForeignKey(x => x.ProblemCategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ResponsibilityZone).WithMany()
                .HasForeignKey(x => x.ResponsibilityZoneId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Text).HasMaxLength(200);
            e.Property(x => x.LegalBasis).HasMaxLength(300);
        });

        b.Entity<Request>(e =>
        {
            e.HasOne(x => x.AppUser).WithMany().HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProblemCategory).WithMany().HasForeignKey(x => x.ProblemCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ClarifyingOption).WithMany().HasForeignKey(x => x.ClarifyingOptionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ResponsibilityZone).WithMany().HasForeignKey(x => x.ResponsibilityZoneId)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.DeadlineDescription).HasMaxLength(100);
            e.Property(x => x.DeadlineLegalBasis).HasMaxLength(300);
            // Фоновая проверка сроков ходит именно по этим полям.
            e.HasIndex(x => new { x.Status, x.DeadlineAt });
        });

        b.Entity<NormativeDeadline>(e =>
        {
            e.HasOne(x => x.ProblemCategory).WithMany(c => c.Deadlines)
                .HasForeignKey(x => x.ProblemCategoryId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.LegalBasis).HasMaxLength(300);
        });
    }
}
