
using System.Text;
using System.Text.RegularExpressions;
using GenealogyCode;

internal partial class Program
{
    private static readonly System.Buffers.SearchValues<char> s_myChars = System.Buffers.SearchValues.Create("0123456789");
    public static string Chapter { get; set;} = "chapter4";
    private static async Task Main(string[] args)
    {
        var theFile = await File.ReadAllLinesAsync($"../Documents/Genealogy/{Chapter}");
        var i = 0;

        List<Person> people = [];
        List<Family> families = [];
        var familyNumber = 0;

        foreach (var line in theFile)
        {
            if (i > 7)
            {
                var person = new Person();

                if (GetGen().IsMatch(line))
                {
                    person.Indi = $"@I{i}@ INDI";
                }
                string lineText = line.Substring(0, line.Length); //3, 7, 11, , 16
                person = GetFullName(line, person);
                var sex = GetSex().Match(line).Value;
                person.Sex = sex != "" ? sex.Substring(1,2).Trim() : "";
                var (dateonly, partialDate) = IsValidDate(line, "born");
                if (dateonly is not null && string.IsNullOrEmpty(partialDate)) 
                {
                    person.CompleteBirthDate = dateonly.Value;
                }
                else if (dateonly is null && !string.IsNullOrEmpty(partialDate))
                {
                    person.IncompleteBirthDate = partialDate;
                }
                var location = GetLocation(line);
                (dateonly, partialDate) = IsValidDate(line, "died");
                if (dateonly is not null && string.IsNullOrEmpty(partialDate)) 
                {
                    person.CompleteDeathDate = dateonly.Value;
                }
                else if (dateonly is null && !string.IsNullOrEmpty(partialDate))
                {
                    person.IncompleteDeathDate = partialDate;
                }
                if (MarriedInfo().IsMatch(line))
                {
                    if (person.FullName.Contains("Russell")){
                        var juststop = true;
                    }
                    person.Children = GetChildren(line, i, theFile);
                    
                    var marriedInfo = MarriedInfo().Match(line).Value.TrimEnd();
                    
                    marriedInfo = marriedInfo.Replace("married ", "").Trim();
                    if (marriedInfo.Contains('('))
                        marriedInfo = marriedInfo.Replace("(1) ", "").Replace("(2) ", "");
                    var index = marriedInfo.AsSpan().IndexOfAny(s_myChars);
                    var marriageDate = "";
                    
                    if (index > 0)
                    {
                        if (marriedInfo.Contains("ABT"))
                        {
                            marriageDate = marriedInfo.Substring(index - 4).Replace(",", "").TrimEnd();
                            person.SpouseName = marriedInfo.Substring(0, index - 4).Replace(",", "").TrimEnd();
                        }
                        else 
                        {
                            marriageDate = marriedInfo.Substring(index).Replace(",", "");
                            person.SpouseName = marriedInfo.Substring(0, index).Replace(",", "").TrimEnd();
                        }
                    } 
                    else 
                    {
                        person.SpouseName = marriedInfo;
                    }
                        
                    var spouse = GetFullName(person.SpouseName, new Person());
                    spouse.Indi = $"@S{i}@ INDI";
                    if (!string.IsNullOrEmpty(person.Sex))
                    {
                        if (person.Sex == "M") spouse.Sex = "F";
                        else if (person.Sex == "F") spouse.Sex = "M";
                    }
                    var lineArray = line.Split(",").ToList();
                    var indexM = lineArray.FindIndex(x=> x.Contains(" married"));
                    for (var j = indexM; j < lineArray.Count; j++) 
                        if (indexM > 0 && !string.IsNullOrEmpty(lineArray[j]))
                            if (lineArray[j].Contains("was born"))
                            {
                                var bornIndex = lineArray[j].IndexOf("born");
                                spouse.IncompleteBirthDate = lineArray[j].Substring(bornIndex + 4).Trim();
                            }
                            else if (lineArray[j].Contains("died"))
                            {   if (spouse.FullName == "Doris Wanda /Shoup/")
                                {var diedIndex = lineArray[j].IndexOf("died");
                                spouse.IncompleteDeathDate = lineArray[j].Substring(diedIndex + 4).Trim();}
                            }

                    familyNumber++;
                    spouse.FamilySpouse = $"1 FAMS @F{familyNumber}@";
                    person.FamilySpouse = $"1 FAMS @F{familyNumber}@";
                    people.Add(spouse);
                    families.Add(CreateFamily(line, person, familyNumber, spouseIndi: $"@S{i}@ INDI", marriageDate));
                }
                
                people.Add(person);
            }   
            i++;
        }
        
        Console.WriteLine(i);
        Console.WriteLine(people.Count);
        foreach (var person in people)
        {
            foreach (var child in person.Children)
            {
                var indi = child.Replace("1 CHIL ", "");
                var myPerson = people.FirstOrDefault(x => x.Indi!.Contains(indi));
                if (myPerson != null)
                {
                    var fs = person?.FamilySpouse?.Replace("1 FAMS @", "");
                    myPerson.FamilyChildren = $"1 FAMC @{fs}";
                }
            }
        }
        ProduceGedcom(people, families);
        
        
    }

    public static List<string> GetChildren(string line, int currentIndex, string[] theFile)
    {
        var currentLevel = GetGen().Match(line).Value.Count(Char.IsWhiteSpace);
        List<string> children = [];
        var i = 1;
        if (currentIndex + i == theFile.Length)
        {
            return [];
        }
        var nextLine = theFile[currentIndex + i];
        var nextLevel = GetGen().Match(nextLine).Value.Count(Char.IsWhiteSpace);
        var last = theFile.Last();
        
        while (nextLevel > currentLevel && nextLine != last)
        {
            nextLine = theFile[currentIndex + i];
            nextLevel = GetGen().Match(nextLine).Value.Count(Char.IsWhiteSpace);
           
            if (nextLevel == currentLevel + 4)
                children.Add($"1 CHIL @I{currentIndex + i}@");
            i++;
        }
        return children;
        
    }

    public static Family CreateFamily(string line, Person person, int familyNumber, string spouseIndi, string marriageDate)
    {
        var family = new Family
        {
            Fam = $"0 @F{familyNumber}@ FAM",
        };
        var (dateonly, partialDate) = IsValidDate(line, "married");
        if (dateonly is not null && string.IsNullOrEmpty(partialDate)) 
        {
            family.CompleteMarriedDate = dateonly.Value;
        }
        else if (dateonly is null && !string.IsNullOrEmpty(partialDate))
        {
            family.IncompleteMarriedDate = partialDate;
        }
        if (person.Sex == "M")
        {
            family.Husband = $"1 HUSB {person.Indi}";
            family.Wife = $"1 WIFE {spouseIndi}";
        }
        else 
        {
            family.Wife = $"1 WIFE {person.Indi}";
            family.Husband = $"1 HUSB {spouseIndi}";
        }
        var lineArray = line.Split(",").ToList();
        var marriageLocation = lineArray.FindIndex(x => x.Contains(" married"));
        string marriagePlace = string.Empty;
        if (marriageLocation == -1)
            marriagePlace = "";
        else if (!string.IsNullOrEmpty(lineArray[marriageLocation + 1]))
            marriagePlace = lineArray[marriageLocation + 1];
        family.Marriage = new()
        {
            MarriageDate = marriageDate,
            MarriagePlace = marriagePlace.TrimStart()
        };
        var children = person.Children;
        family.Children = [];
        foreach (var child in children)
        {
            family.Children.Add(child);
        }
        
        return family;
    }
    public static (DateOnly?,string) GetLocation(string line)
    {
        var locationindex = GetBirthDate().Match(line).Index;
        if (locationindex > 0)
        {
            int locationlength = GetBirthDate().Match(line).Length;
            var hmmm = line.Substring(locationindex + locationlength).Trim();
        }
        return (new DateOnly(), "string");
    }
    public static (DateOnly?,string) IsValidDate(string line, string dateType)
    {
        string? date = string.Empty; 
        string[] charsToRemove = [", born ", ",", "."];
        if (dateType.Contains("born"))
        {
            date = GetBirthDate().Match(line).Value;
        }
        else if (dateType.Contains("died"))
        {
            charsToRemove = [", died ", ",", "."];
            date = GetDeathDate().Match(line).Value;
        }
        else if (dateType.Contains("married"))
        {
            charsToRemove = [" married ", ",", "."];
            date = MarriedInfo().Match(line).Value;
            var index = date.AsSpan().IndexOfAny(s_myChars);
            if (index > 0)
                date = date.Substring(index);
        }
        
        foreach (var c in charsToRemove)
        {
            date = date.Replace(c, string.Empty);
        }
        
        if (DateOnly.TryParse(date, out DateOnly result))
        {
            return (result, string.Empty);
        }
        else
        {
            return (null, date);
        }            
    }
    public static Person GetFullName(string line, Person person)
    {
        var nameArray = line.Split(',');
        var name = nameArray[0].Trim();
        if (name.Contains('.'))
        {
            name = name.Substring(name.IndexOf('.') + 1, name.Length - name.IndexOf('.') - 1).TrimStart();
        }
        var surnameArray = name.Split(" ");
        person.Surname = surnameArray.Last();
        var lastNameIndex = name.LastIndexOf(' ') + 1;
        person.GivenName = name.Substring(0, lastNameIndex).Trim();
        name = name.Insert(lastNameIndex, "/");
        person.FullName = name.Insert(name.Length, "/");
        
        return person;
    }

    
    [GeneratedRegex(@",\s[M|F],")]
    private static partial Regex GetSex();

    [GeneratedRegex(@"^[0-9]{4}")]
    private static partial Regex IsYearOnly();

    [GeneratedRegex(@"\s*[A-Za-z0-9]*")]
    private static partial Regex GetGen();

    [GeneratedRegex(@"((?:^|\W)married(?:$|\W)[A-Za-z0-9\s(1)""\.]*\,\.)|(married (_*\s[A-Za-z\s]*)|married\s[A-Za-z0-9\s(1)""\._]*\W)")]
    private static partial Regex MarriedInfo();


    [GeneratedRegex(@", born ([^,]*)(,|\.)")]
    private static partial Regex GetBirthDate();

    [GeneratedRegex(@", died ([^,]*)(,|\.)")]
    private static partial Regex GetDeathDate();

    [GeneratedRegex(@"^\s*[A-Z]\.")]
    private static partial Regex GetName();
    public static void ProduceGedcom(List<Person> people, List<Family> families)
    {
        string docPath =
          Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Write the string array to a new file named "WriteLines.txt".
        using StreamWriter outputFile = new(Path.Combine(docPath, $"{Chapter}.ged"));
        outputFile.WriteLine("0 HEAD");
        outputFile.WriteLine("1 GEDC");
        outputFile.WriteLine("2 VERS 5.5.5");
        outputFile.WriteLine("2 FORM LINEAGE-LINKED");
        outputFile.WriteLine("3 VERS 5.5.5");
        outputFile.WriteLine("1 CHAR UTF-8");
        outputFile.WriteLine("1 SOUR");
        outputFile.WriteLine("0 @I172@ SUBM");
        outputFile.WriteLine("1 NAME Russell Hires");
        foreach (var pers in people)
        {
            outputFile.WriteLine($"0 {pers.Indi}");
            outputFile.WriteLine($"1 NAME {pers.FullName}");
            outputFile.WriteLine($"2 GIVN {pers.GivenName}");
            outputFile.WriteLine($"2 SURN {pers.Surname}");
            if (!string.IsNullOrEmpty(pers.Sex))
            {
                outputFile.WriteLine($"1 SEX {pers.Sex}");
            }
            outputFile.WriteLine($"1 BIRT");
            if (pers.CompleteBirthDate != null)
            {
                outputFile.WriteLine($"2 DATE {pers.CompleteBirthDate:d MMM yyyy}".ToUpper());
            }
            else if (!string.IsNullOrEmpty(pers.IncompleteBirthDate))
            {
                outputFile.WriteLine($"2 DATE {pers.IncompleteBirthDate}");
            }
            if (!string.IsNullOrEmpty(pers.BirthLocation))
            {
                outputFile.WriteLine($"2 PLAC {pers.BirthLocation}");
            }
            outputFile.WriteLine($"1 DEAT");
            if (pers.CompleteDeathDate != null)
            {
                outputFile.WriteLine($"2 DATE {pers.CompleteDeathDate:d MMM yyyy}".ToUpper());
            }
            else if (!string.IsNullOrEmpty(pers.IncompleteDeathDate))
            {
                outputFile.WriteLine($"2 DATE {pers.IncompleteDeathDate}");
            }
            if (!string.IsNullOrEmpty(pers.DeathLocation))
            {
                outputFile.WriteLine($"2 PLAC {pers.DeathLocation}");
            }
            if (!string.IsNullOrEmpty(pers.FamilySpouse))
            {
                outputFile.WriteLine(pers.FamilySpouse);
            }
            if (!string.IsNullOrEmpty(pers.FamilyChildren))
            {
                outputFile.WriteLine(pers.FamilyChildren);
            }
        }

        foreach (var family in families)
        {
            outputFile.WriteLine(family.Fam);
            if (family.Husband != null)
                outputFile.WriteLine(family.Husband.Replace("INDI", ""));
            if (family.Wife != null)
                outputFile.WriteLine(family.Wife.Replace("INDI", ""));
            if (family.Children != null)
                foreach (var child in family.Children)
                {
                    outputFile.WriteLine(child);
                }
            outputFile.WriteLine($"1 MARR");
            if (family.Marriage != null)
            {   if (!string.IsNullOrEmpty(family.Marriage.MarriageDate))
                    outputFile.WriteLine($"2 DATE {family.Marriage.MarriageDate}");
                if (!string.IsNullOrEmpty(family.Marriage.MarriagePlace))
                    outputFile.WriteLine($"2 PLAC {family.Marriage.MarriagePlace}");
            }
        }
        outputFile.WriteLine("0 TRLR");
    }

}
