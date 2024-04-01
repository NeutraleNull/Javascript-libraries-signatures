using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Database;

public class FunctionSignature
{
    [Key]
    public int Id { get; set; }
    public string LibName { get; set; }
    public string? Namespace { get; set; }
    public string Version { get; set; }
    public string FunctionName { get; set; }
    public DateTime CreationDateTime { get; set; } = DateTime.UtcNow;
    public int[] SignatureMinhash { get; set; }
    public ulong[] SignatureSimhash { get; set; }
}