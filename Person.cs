namespace GenealogyCode;
public class Person
{
    public string? FullName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? BirthDate { get; set; }
    public string? DeathDate { get; set; }
    public List<string>? MarriedDate { get; set; }
    public string? BirthLocation { get; set; }
    public string? DeathLocation { get; set; }

    public List<string>? SpouseName { get; set; }
    public string? Suffix { get; set; }
    public List<string> Children { get; set; } = [];

    public List<Person> Parents { get; set; } = [];
    public int? DarNumber { get; set; }
    public string? Indi { get; set; }
    public string? Sex { get; set; }
    public List<string>? FamilySpouse { get; set; }
    public string? ChildOfFamily { get; set; }

}