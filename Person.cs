namespace GenealogyCode;
public class Person
{
    public string? FullName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? IncompleteBirthDate { get; set; }
    public DateOnly? CompleteBirthDate { get; set; }
    public string? IncompleteDeathDate { get; set; }
    public DateOnly? CompleteDeathDate { get; set; }
    public string? IncompleteMarriedDate { get; set; }
    public DateOnly? CompleteMarriedDate { get; set; }
    public string? BirthLocation { get; set; }
    public string? DeathLocation { get; set; }

    public List<string>? SpouseName { get; set; }

    public List<string> Children { get; set; } = [];

    public List<string> Parents { get; set; } = [];
    public int? DarNumber { get; set; }
    public string? Indi { get; set; }
    public string? Sex { get; set; }
    public List<string>? FamilySpouse { get; set; }
    public string? FamilyChildren { get; set; }

}