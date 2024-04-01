using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database;

public class FunctionSignatureContext() : DbContext
{
    public DbSet<FunctionSignature> FunctionSignatures { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FunctionSignature>(entity =>
        {
            entity.ToTable("function_signatures");

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
        });
    }
}

public static class FunctionSignatureContextExtension
{
    public static void AddPostgresDB(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContextPool<FunctionSignatureContext>(options =>
        {
            options.UseNpgsql("Host=localhost;Database=js_signatures;Username=pg;Password=pgadmin").UseSnakeCaseNamingConvention();
        });
    } 
}