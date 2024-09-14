
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using GenealogyCode;

internal partial class Program
{
    private static readonly System.Buffers.SearchValues<char> s_myChars = System.Buffers.SearchValues.Create("0123456789");
    
    public static string Chapter { get; set;} = "chapter6";
    private static async Task Main(string[] args)
    {
        var theFile = await File.ReadAllLinesAsync($"../Documents/Genealogy/{Chapter}");
        var i = 0;

        List<Person> people = [];
        List<Family> families = [];
        var familyNumber = 1;
        var marriageDate = "";
        foreach (var line in theFile)
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
            
            person.IncompleteBirthDate = GetDate(line, "born");;
            person.IncompleteDeathDate = GetDate(line, "died");;

            int marriageNumber = 1; 
            List<int> indexNumbers = [];
            
            while (line.Contains($"({marriageNumber})"))
            {
                indexNumbers.Add(line.IndexOf($"({marriageNumber})"));
                marriageNumber++;    
            }
            marriageNumber = 1;
            
            if (indexNumbers.Count > 0)
            {
                person.SpouseName = [];
                
                foreach (var item in indexNumbers)
                {
                    string ss;
                    if (indexNumbers.IndexOf(item) == indexNumbers.Count - 1) 
                        ss = line.Substring(item, line.Length - item);
                    else 
                        ss = 0 == indexNumbers[marriageNumber - 1] - item
                            ? line.Substring(item, indexNumbers[marriageNumber] - item)
                            : line.Substring(item, indexNumbers[marriageNumber - 1] - item);

                    var ssArray = ss.Split([',']);
                    ssArray[0] = ssArray[0].Replace($"({marriageNumber}) ", "");
                    var marriageDateIndex = ssArray[0].AsSpan().IndexOfAny(s_myChars); 
                    if (marriageDateIndex > 0)
                    {
                        var abtIndex = ssArray[0].Contains("ABT") ? 4 : 0;

                        marriageDate = ssArray[0].Substring(marriageDateIndex - abtIndex).TrimEnd();
                        person.SpouseName.Add(ssArray[0].Substring(0, marriageDateIndex - abtIndex).TrimEnd());                        
                    }
                    else 
                    {
                        person.SpouseName.Add(ssArray[0].Replace(",", "").TrimEnd());
                    }

                    var spouse = GetFullName(person.SpouseName[marriageNumber - 1], new Person());
                    spouse.Indi = $"@S{i}-{marriageNumber}@ INDI";
                    if (!string.IsNullOrEmpty(person.Sex))
                    {
                        if (person.Sex == "M") spouse.Sex = "F";
                        else if (person.Sex == "F") spouse.Sex = "M";
                    }
                    var index = ssArray.TakeWhile(t => !t.Contains("born")).Count();
                    if (index < ssArray.Length) 
                    {
                        var wasBorn = ssArray.FirstOrDefault(x=> x.Contains("born"));
                        if (wasBorn != null)
                        {
                            var birthDateIndex = wasBorn.AsSpan().IndexOfAny(s_myChars);
                            if (birthDateIndex > 0)
                            { 
                                var abtIndex = wasBorn.Contains("ABT") ? 4 : 0;
                                spouse.IncompleteBirthDate = wasBorn.Substring(birthDateIndex - abtIndex).Replace(",", "");
                            }
                        }
                    }
                    
                    var died = ssArray.FirstOrDefault(x=> x.Contains("died"));
                    if (died != null)
                    {
                        var deathDateIndex = died.AsSpan().IndexOfAny(s_myChars);
                        if (deathDateIndex > 0)
                        {
                            var abtIndex = died.Contains("ABT") ? 4 : 0;
                            spouse.IncompleteDeathDate = died.Substring(deathDateIndex - abtIndex).Replace(",", "");
                        }
                    }
                    index = ssArray.TakeWhile(t => !t.Contains("died")).Count();
                    if (index == ssArray.Length)
                    {
                        spouse.DeathLocation = "";
                    }
                    else if (index + 1 < ssArray.Length)
                    {
                        spouse.DeathLocation = ssArray[index + 1].TrimStart().TrimEnd();
                    }
                    
                    var children = GetChildren(line, i, theFile, marriageNumber);
                    
                    familyNumber++;
                    
                    foreach (var child in children)
                        person.Children.Add($"{child} F{familyNumber}"); 
                    
                    person.FamilySpouse ??= [];
                    person.FamilySpouse.Add($"1 FAMS @F{familyNumber}@");
                    spouse.FamilySpouse = [];
                    spouse.FamilySpouse.Add($"1 FAMS @F{familyNumber}@");
                    people.Add(spouse);

                    families.Add(CreateFamily(line, person, familyNumber, spouse.Indi, marriageDate));

                    marriageNumber++;
                }
            }    

            else if (MarriedInfo().IsMatch(line))
            {
                Person spouse;
                SingleMarriage(theFile, i, out marriageDate, line, person, out spouse);

                familyNumber++;

                spouse.FamilySpouse = [];
                spouse.FamilySpouse.Add($"1 FAMS @F{familyNumber}@");
                person.FamilySpouse = [];
                person.FamilySpouse.Add($"1 FAMS @F{familyNumber}@");
                people.Add(spouse);
                families.Add(CreateFamily(line, person, familyNumber, spouseIndi: $"@S{i}@ INDI", marriageDate));
            }

            people.Add(person);   
            i++;
        }
        
        Console.WriteLine(i);
        Console.WriteLine(people.Count);
        
        foreach (var person in people) //108, 109, 110
        {
            if (people.IndexOf(person) == 109){
                Console.WriteLine(person);
            }
            if (person.FamilySpouse == null)
                continue;
            var allTheChildren = new List<string>();
            foreach (var familySpouse in person.FamilySpouse)
            {
                if (familySpouse.StartsWith("1 FAMS @F36@")){
                    var stop = true;
                } 
                if (person.FamilySpouse.Count() > 1)
                    Console.WriteLine($"Person {person.Indi} has more than one spouse");    
                var fs = familySpouse.Replace("1 FAMS @F", "").Replace("@", "");
                var children = person.FamilySpouse.Count() > 1 
                ? person.Children.Where(x => x.Contains($"F{fs}"))
                : person.Children;
                
                foreach(var theChild in children)
                {
                    var childsIndi = theChild.Replace("1 CHIL ", "").Replace($" F{fs}", "");
                    var personsChild = people.FirstOrDefault(x => x.Indi!.Contains(childsIndi));
                    if (personsChild != null)
                        personsChild.ChildOfFamily = $"1 FAMC @F{fs}@";
                }
                
                foreach (var child in person.Children.Where(x => x.Contains($"F{fs}")))
                {
                    allTheChildren.Add(child.Replace($"F{fs}", "").Trim());
                }
            }
            if (person.Children.Any(x => x.Contains("@I75@ F36")))
                Console.WriteLine($"Person {person.Indi} has a child with a family that is not");
            if (allTheChildren.Count() > 0 && person.FamilySpouse.Count() > 1)
            {
                person.Children = [];
                person.Children.AddRange(allTheChildren);
            }
        }
        
        ProduceGedcom(people, families);
    }

    private static void SingleMarriage(string[] theFile, int i, out string marriageDate, string line, Person person, out Person spouse)
    {
        var marriedInfo = MarriedInfo().Match(line).Value.TrimEnd();

        marriedInfo = marriedInfo.Replace("married ", "").Trim();
        var marriages = new List<Marriage>();
        int index = marriedInfo.AsSpan().IndexOfAny(s_myChars);
        person.SpouseName = [];
        person.Children = GetChildren(line, i, theFile);
        marriageDate = "";
        if (index > 0)
        {
            if (marriedInfo.Contains("ABT"))
            {
                marriageDate = marriedInfo.Substring(index - 4).Replace(",", "").TrimEnd();
                person.SpouseName.Add(marriedInfo.Substring(0, index - 4).Replace(",", "").TrimEnd());
            }
            else
            {
                marriageDate = marriedInfo.Substring(index).Replace(",", "");
                person.SpouseName.Add(marriedInfo.Substring(0, index).Replace(",", "").TrimEnd());
            }
        }
        else
        {
            person.SpouseName.Add(marriedInfo);
        }

        spouse = GetFullName(person.SpouseName[0], new Person());
        spouse.Indi = $"@S{i}@ INDI";
        if (!string.IsNullOrEmpty(person.Sex))
        {
            if (person.Sex == "M") spouse.Sex = "F";
            else if (person.Sex == "F") spouse.Sex = "M";
        }
        var lineArray = line.Split(",").ToList();
        var indexM = lineArray.FindIndex(x => x.Contains("married"));
        for (var j = indexM; j < lineArray.Count; j++)
            if (indexM > 0 && !string.IsNullOrEmpty(lineArray[j]))
                if (lineArray[j].Contains("born"))
                {
                    var bornIndex = lineArray[j].IndexOf("born");
                    spouse.IncompleteBirthDate = lineArray[j].Substring(bornIndex + 4).Trim();
                }
                else if (lineArray[j].Contains("died"))
                {
                    var diedIndex = lineArray[j].IndexOf("died");
                    spouse.IncompleteDeathDate = lineArray[j].Substring(diedIndex + 4).Trim();
                }
    }

    public static List<string> GetChildren(string line, int currentIndex, string[] theFile, int marriageNumber = 0)
    {
        var currentLevel = GetGen().Match(line).Value.Count(Char.IsWhiteSpace);
        List<string> children = [];
        var i = 1;
        if (currentIndex + i >= theFile.Length)
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
            {
                if (nextLine.Contains($". ({marriageNumber}") || marriageNumber == 0)
                    children.Add($"1 CHIL @I{currentIndex + i}@");
            }
            i++;
        }
        return children;
        
    }

    public static Family CreateFamily(string line, Person person, int familyNumber, string spouseIndi, string marriageDate)
    {
        var family = new Family
        {
            Fam = $"0 @F{familyNumber}@ FAM",
            IncompleteMarriedDate = GetDate(line, "married")
        };

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
        if (children.Any(x => x.Contains("F"))) 
            children = children.Where(x => x.Contains($"F{familyNumber}")).ToList();
            
        try
        {
            foreach (var child in children)
            {
                var theChild = child;
                theChild = theChild.Replace($"F{familyNumber}", "").Trim();
                family.Children.Add(theChild);
            }
               
        }        
        catch (Exception ex)
        {
            var exc = ex;
        }

        return family;
    }
    
    public static string GetDate(string line, string dateType)
    {
        string? date = string.Empty; 
        if (dateType.Contains("born"))
        {
            date = GetBirthDate().Match(line).Groups["date"].Value.TrimStart();
        }
        else if (dateType.Contains("died"))
        {
            date = GetDeathDate().Match(line).Groups["date"].Value;
        }
        else if (dateType.Contains("married"))
        {
            date = MarriedInfo().Match(line).Value;
            var index = date.AsSpan().IndexOfAny(s_myChars);
            if (index > 0)
            {
                date = date.Substring(index);
                if (GetDate().IsMatch(date))
                    date = GetDate().Match(date).Value;
            }
        }
        return date;            
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
//---  
//---
    [GeneratedRegex(@"(?<!\d) ((JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)) [0-9]{4}")]
    private static partial Regex MonthYear();
//---  
//---
    [GeneratedRegex(@",\s[M|F],")]
    private static partial Regex GetSex();
//---  
//---
    [GeneratedRegex(@"^[0-9]{4}")]
    private static partial Regex IsYearOnly();
//---  
//---
    [GeneratedRegex(@"\s*[A-Za-z0-9]*")]
    private static partial Regex GetGen();
//---  
//---
    [GeneratedRegex(@"((?:^|\W)married(?:$|\W)[A-Za-z0-9\s(1)""\.]*\,\.)|(married (_*\s[A-Za-z\s]*)|married\s[A-Za-z0-9\s(1)""\._]*\W)")]
    private static partial Regex MarriedInfo();
//---  
//---
    [GeneratedRegex(@"(ABT|BEF|AFT)*((\d))* (JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)*\s*[0-9]{4}\b")]
    private static partial Regex GetDate();
//---  
//---  
    [GeneratedRegex(@"(?<! died)born *(?<date>(ABT|BEF|AFT|BET)*((\d))* (?<month>(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?\s*[0-9]{4}\b( AND [\w \d}]*)*)")]
    private static partial Regex GetBirthDate();
//---  
//---
    [GeneratedRegex(@"(?<! born)died *(?<date>(ABT|BEF|AFT|BET)*((\d))* (?<month>(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?\s*[0-9]{4}\b( AND [\w \d}]*)*)")]
    private static partial Regex GetDeathDate();
//---  
//---
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
            if (!string.IsNullOrEmpty(pers.FamilySpouse?[0]))
            {
                foreach (var fs in pers.FamilySpouse)
                    outputFile.WriteLine(fs);
            }
            if (!string.IsNullOrEmpty(pers.ChildOfFamily))
            {
                outputFile.WriteLine(pers.ChildOfFamily);
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
