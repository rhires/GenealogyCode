namespace GenealogyCode;

public class Family
{
    public string? Fam { get; set; }
    public string? Husband { get; set; }
    public string? Wife { get; set; }
    public List<string>? Children { get; set; }
    public string? IncompleteMarriedDate { get; set; }
    public DateOnly? CompleteMarriedDate { get; set; }
    public Marriage? Marriage { get; set; }
}