using System.ComponentModel.DataAnnotations;

namespace PackageAnalyzer;

using Microsoft.EntityFrameworkCore;

public class FunctionSignatureContext() : DbContext
{
    public DbSet<FunctionSignature> FunctionSignatures { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //optionsBuilder.UseSqlite($"Data Source={sqlFilePath}", options =>
        //{
        //});
        optionsBuilder.UseNpgsql("Host=localhost;Database=js_signatures;Username=pg;Password=pgadmin");
    }
    
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

public class FunctionSignature
{
    [Key]
    public int Id { get; set; }
    public string LibName { get; set; }
    public string? Namespace { get; set; }
    public string Version { get; set; }
    public string FunctionName { get; set; }
    public DateTime CreationDateTime { get; set; } = DateTime.UtcNow;
    public byte[] SignatureMinhash { get; set; }
    public byte[] SignatureSimhash { get; set; }
}