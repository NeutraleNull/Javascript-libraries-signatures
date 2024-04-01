using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

public class FunctionSignatureContext : DbContext
{
    public DbSet<FunctionSignature> FunctionSignatures { get; set; }
    
    public FunctionSignatureContext(DbContextOptions<FunctionSignatureContext> options)
        : base(options)
    {
    }

    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FunctionSignature>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LibName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Namespace)
                .HasMaxLength(255);

            entity.Property(e => e.Version)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(e => e.FunctionName)
                .HasMaxLength(255);

            entity.Property(e => e.CreationDateTime)
                .IsRequired();

            entity.Property(e => e.SignatureMinhash)
                .IsRequired();

            entity.Property(e => e.SignatureSimhash)
                .IsRequired();

            entity.HasIndex(e => e.SignatureSimhash);
            entity.HasIndex(e => e.SignatureMinhash);
        });
    }
}

public static class FunctionSignatureContextExtension
{
    public static void AddPostgresDB(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContextPool<FunctionSignatureContext>(options =>
        {
            options.UseNpgsql("Host=localhost;Database=js_signatures;Username=pg;Password=pgadmin", pgOptions =>
                {
                    pgOptions.CommandTimeout(180);
                    pgOptions.EnableRetryOnFailure(10);
                    pgOptions.MaxBatchSize(10000);
                })
                .UseSnakeCaseNamingConvention()
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
    } 
}