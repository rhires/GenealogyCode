using System.Text.RegularExpressions;
using GenealogyCode;

internal partial class Program
{
    public static readonly System.Buffers.SearchValues<char> s_myChars = System.Buffers.SearchValues.Create("0123456789");
    public static int Indi { get; set; } = 0;

    public static List<Person> People { get; set; } = [];

    public static int FamilyNumber { get; set; } = 0;
    public static List<Family> Families { get; set; } = [];
    public static string Chapter { get; set;} = "MasterFile";
    private static async Task Main(string[] args)
    {
        var TheFile = await File.ReadAllLinesAsync($"../Documents/Genealogy/{Chapter}");

        List<Person> people = [];
        List<Family> families = [];
        var i = 0;
        foreach (var line in TheFile)
        {
            if (string.IsNullOrEmpty(line)) continue;

            var person = new Person();
            if (GetGen().IsMatch(line))
                person.Indi = $"@I{i}@ INDI";

            string lineText = line.Substring(0, line.Length); //3, 7, 11, , 16
            person = GetFullName(line, person);
            var sex = GetSex().Match(line).Value;
            person.Sex = sex != "" ? sex.Substring(1, 2).Trim() : "";
            var lineList = line.Split(',').ToList();
            person = GetBirthOrDeathdInfo(lineList, person, "born");
            person = GetBirthOrDeathdInfo(lineList, person, "died");
            person = GetPartnerInfo(line, person, TheFile, i);

            People.Add(person);
            i++;
            Indi++;
        }

        Console.WriteLine(Indi);
        Console.WriteLine(People.Count);

        AttachChildrenToParents();

        ProduceGedcom();
    }

    private static void AttachChildrenToParents()
    {
        foreach (var person in People)
        {
            if (person.FamilySpouse == null)
                continue;
            var allTheChildren = new List<string>();
            foreach (var familySpouse in person.FamilySpouse)
            {
                var fs = familySpouse.Replace("1 FAMS @F", "").Replace("@", "");
                var children = person.FamilySpouse.Count > 1
                ? person.Children.Where(x => x.Contains($"F{fs}"))
                : person.Children;

                foreach (var theChild in children)
                {
                    var childsIndi = theChild.Replace("1 CHIL ", "").Replace($" F{fs}", "");
                    var personsChild = People.FirstOrDefault(x => x.Indi!.Contains(childsIndi));
                    if (personsChild != null)
                        personsChild.ChildOfFamily = $"1 FAMC @F{fs}@";
                }

                foreach (var child in person.Children.Where(x => x.Contains($"F{fs}")))
                {
                    allTheChildren.Add(child.Replace($"F{fs}", "").Trim());
                }
            }
            if (allTheChildren.Count > 0 && person.FamilySpouse.Count > 1)
            {
                person.Children = [];
                person.Children.AddRange(allTheChildren);
            }
        }
    }

    private static Person GetPartnerInfo(string line, Person person, string[] theFile, int lineNumber)
    {
        int marriageNumber = 1;
        List<int> indexNumbers = [];

        while (line.Contains($"({marriageNumber})") || line.Contains($"[{marriageNumber}]"))
        {
            if (line.Contains($"[{marriageNumber}]"))
                indexNumbers.Add(line.IndexOf($"[{marriageNumber}]"));
            else
                indexNumbers.Add(line.IndexOf($"({marriageNumber})"));
            marriageNumber++;
        }

        string pType = "married";
        if (marriageNumber == 1)
        {
            if (line.Contains($"{pType}"))
                indexNumbers.Add(line.IndexOf($"{pType}"));
            else if (line.Contains("partnered"))
            {
                pType = "partnered";
                indexNumbers.Add(line.IndexOf($"{pType}"));
            }
        }
        marriageNumber = 1;
        person.SpouseName = [];
        person.MarriedDate = [];
        foreach (var item in indexNumbers)
        {
            string ss;
            if (indexNumbers.IndexOf(item) == indexNumbers.Count - 1)
                ss = line.Substring(item, line.Length - item);
            else
                ss = 0 == indexNumbers[marriageNumber - 1] - item
                    ? line.Substring(item, indexNumbers[marriageNumber] - item)
                    : line.Substring(item, indexNumbers[marriageNumber - 1] - item);
            bool isMarried = true;
            var ssArray = ss.Split([',']).ToList();
            if (ssArray[0].Contains($"[{marriageNumber}]"))
            {
                isMarried = false;
            }
            ssArray[0] = ssArray[0].Replace($"({marriageNumber}) ", "").Replace($"[{marriageNumber}] ", "").Replace($"{pType}","");
            Indi++; 
            
            var spouse = new Person
            {
                Indi = isMarried ? $"@S{lineNumber}@ INDI" : $"@P{lineNumber}@ INDI"
            };
            if (indexNumbers.Count == 1) 
                marriageNumber = 0;
            else
                spouse.Indi = isMarried 
                    ? $"@S{lineNumber}_{marriageNumber}@ INDI"
                    : $"@P{lineNumber}_{marriageNumber}@ INDI";
            spouse = GetSpouseNameAndMarriedDate(spouse, ssArray[0]);
            person.SpouseName.Add(spouse.FullName!);
            person.MarriedDate.Add(spouse.MarriedDate![0]);
            spouse.SpouseName ??= [];
            spouse.SpouseName.Add(person.FullName!);
            var marriageLocation = "";
            if (ssArray.Count > 1 && ssArray[1].Contains('|'))
                marriageLocation = ssArray[1].Replace('|', ',');
            
            spouse = GetSpouseBirthDateDeathDateAndLocation(spouse, ssArray);
            spouse = AddSpouseParents(spouse, ssArray);
            if (!string.IsNullOrEmpty(person.Sex))
            {
                if (person.Sex == "M") spouse.Sex = "F";
                else if (person.Sex == "F") spouse.Sex = "M";
            }
            var children = GetChildren(line, lineNumber, marriageNumber, theFile);
            FamilyNumber++;

            foreach (var child in children)
            {
                person.Children.Add($"{child} F{FamilyNumber}");
                spouse.Children.Add($"{child} F{FamilyNumber}");
            }
            person.FamilySpouse ??= [];
            person.FamilySpouse.Add($"1 FAMS @F{FamilyNumber}@");
            spouse.FamilySpouse = [];
            spouse.FamilySpouse.Add($"1 FAMS @F{FamilyNumber}@");
            People.Add(spouse);

            Families.Add(CreateFamily(line, person, FamilyNumber, spouse.Indi!));

            marriageNumber++;
        }
        return person;
    }
    private static Person AddSpouseParents(Person spouse, List<string> info)
    {
        var index = info.FindIndex(x => x.Contains("son of") || x.Contains("daughter of"));
        if (index != -1)
        {
            var ps = info[index].Split(" of ");
            var parents = ps[1].Split(" and ");
            FamilyNumber++;
            if (FamilyNumber == 188)
                Console.WriteLine("here");
            foreach (var parent in parents)
            {
                Indi++;
                var sParent = GetFullName(parent, new Person());
                sParent.Indi = $"@SP{Indi}@ INDI";
                if (parent == parents[0]) 
                    sParent.Sex = "M";
                else
                    sParent.Sex = "F";

                sParent.FamilySpouse ??= [];
                sParent.FamilySpouse.Add($"1 FAMS @F{FamilyNumber}@");    
                spouse.Parents.Add(sParent);    
                People.Add(sParent);
            }
            spouse.ChildOfFamily = $"1 FAMC @F{FamilyNumber}@";

            Families.Add(CreateFamily(spouse.Parents, spouse.Indi!));
        }
        return spouse;
    }
    private static Family CreateFamily(List<Person> parents, string indi)
    {
        var family = new Family
        {
            Fam = $"0 @F{FamilyNumber}@ FAM",
            Husband = $"1 HUSB {parents[0].Indi}"
        };
        if (parents.Count == 2)    
            family.Wife = $"1 WIFE {parents[1].Indi}";
        family.Children = [];
        family.Children.Add($"1 CHIL {indi.Replace("INDI", "")}");
        return family;
    }
    private static Person GetSpouseBirthDateDeathDateAndLocation(Person spouse, List<string> info)
    {
        var index = info.FindIndex(x => x.Contains("born"));
        if (index != -1)
        {
            spouse.BirthDate = GetDate().Match(info[index]).Value.TrimStart();
            if (index + 1 < info.Count && info[index + 1].Contains('|'))
            {
                spouse.BirthLocation = info[index + 1].Replace("|", ",").TrimStart();
            }
        }
        index = info.FindIndex(x => x.Contains("died"));
        if (index != -1)
        {
            spouse.DeathDate = GetDate().Match(info[index]).Value.TrimStart();
            if (info.Count < index + 1 && info[index + 1].Contains('|'))
            {
                spouse.DeathLocation = info[index + 1].Replace("|", ",");
            }
        }
        return spouse;
    }
    private static Person GetSpouseNameAndMarriedDate(Person spouse, string marriageInfo)
    {
        var marriageDateIndex = GetDate().Match(marriageInfo).Index;
        string spouseName;
        if (marriageDateIndex > 0)
            spouseName = marriageInfo.Substring(0, marriageDateIndex);
        else 
            spouseName = marriageInfo;
        spouse = GetFullName(spouseName, spouse);
        var marriageDate = GetDate().Match(marriageInfo).Value.TrimStart().TrimEnd();
        spouse.MarriedDate ??= [];
        spouse.MarriedDate?.Add(marriageDate);
        return spouse;
    }
    private static Person GetBirthOrDeathdInfo(List<string> lineArray, Person person, string type)
    {
        var typeIndex = lineArray.FindIndex(x => x.Contains($" {type} "));
        var match = lineArray.FirstOrDefault(x => x.Contains($" {type} ") && typeIndex < 6);
        
        if (match != null) 
            if (type.Contains("born"))
                person.BirthDate = GetDate().Match(match).Value.TrimStart();
            else 
                person.DeathDate = GetDate().Match(match).Value.TrimStart();

        if (typeIndex != -1 && typeIndex + 1 < lineArray.Count && lineArray[typeIndex + 1].Contains('|'))
            {
                var location = lineArray[typeIndex + 1].Replace("|", ",").TrimStart().TrimEnd(',');
                if (type.Contains("born"))
                    person.BirthLocation = location;
                else
                    person.DeathLocation = location;
            }
        
        return person;
    }
    public static List<string> GetChildren(string line, int currentIndex, int marriageNumber, string[] theFile)
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
    public static Family CreateFamily(string line, Person person, int familyNumber, string spouseIndi)
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
            MarriageDate = person.MarriedDate[0],
            MarriagePlace = marriagePlace.TrimStart().Replace("|", ",")
        };
        var children = person.Children;
        family.Children = [];
        if (children.Any(x => x.Contains('F'))) 
            children = children.Where(x => x.Contains($"F{familyNumber}")).ToList();
            
        foreach (var child in children)
        {
            var theChild = child;
            theChild = theChild.Replace($"F{familyNumber}", "").Trim();
            family.Children.Add(theChild);
        }
        return family;
    }
    public static string GetDate(string line, string dateType)
    {
        string? date = string.Empty; 
        if (dateType.Contains("born " ))
        {
            date = GetBirthDate().Match(line).Groups["date"].Value.TrimStart();
        }
        else if (dateType.Contains("died " ))
        {
            date = GetDeathDate().Match(line).Groups["date"].Value.TrimStart();
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
        
        if (GetSuffix().IsMatch(name))
        {
            var suffValue = GetSuffix().Match(name).Value;
            name = name.Replace(suffValue, "").Replace("  ", " ").Trim();
            person.Suffix = suffValue;
        }
        var surnameArray = name.Split(" ");
        person.Surname = surnameArray.Last();
        var lastNameIndex = name.LastIndexOf(' ') + 1;
        person.GivenName = name.Substring(0, lastNameIndex).Trim();       
        name = name.Insert(lastNameIndex, "/");
        person.FullName = name.Insert(name.Length, "/");
        return person;
    }

    public static void ProduceGedcom()
    {
        string docPath =
          Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Write the string array to a new file named "WriteLines.txt".
        using StreamWriter outputFile = new(Path.Combine(docPath, $"{Chapter}.ged"));
        outputFile.WriteLine("0 HEAD");
        outputFile.WriteLine("1 GEDC");
        outputFile.WriteLine("2 VERS 7.0");
        outputFile.WriteLine("1 SOUR Genealogy Reader");
        outputFile.WriteLine("2 VERS 1.0");
        outputFile.WriteLine("2 NAME Genealogy Reader"); 
        outputFile.WriteLine("2 CORP Russell Hires");
        outputFile.WriteLine("3 EMAIL rhires@earthlink.net");
        outputFile.WriteLine($"3 WWW https://freepages.rootsweb.com/~dmorgan/genealogy/{Chapter}.html");       
        //outputFile.WriteLine("2 DATA Captain George Barber of Georgia"); //the name
        
        outputFile.WriteLine($"1 DATE {DateTime.Today.Date:d MMM yyyy}".ToUpper());
        outputFile.WriteLine("0 @SUBM1@ SUBM");
        outputFile.WriteLine("1 NAME Russell /Hires/");
        outputFile.WriteLine("1 ADDR private");
        outputFile.WriteLine("2 CITY Tampa");
        outputFile.WriteLine("2 STAE FL");
        outputFile.WriteLine("2 CTRY USA");
        outputFile.WriteLine("1 EMAIL rhires@earthlink.net");
        
        foreach (var pers in People)
        {
            outputFile.WriteLine($"0 {pers.Indi}");
            outputFile.WriteLine($"1 NAME {pers.FullName?.Replace("(1C) ", "").Replace("(2C) ", "").Replace("(3C) ", "")}");
            outputFile.WriteLine($"2 GIVN {pers.GivenName?.Replace("(1C) ", "").Replace("(2C) ", "").Replace("(3C) ", "")}");
            outputFile.WriteLine($"2 SURN {pers.Surname}");
            if (!string.IsNullOrEmpty(pers.Suffix))
                outputFile.WriteLine($"2 NSFX {pers.Suffix}");
            if (!string.IsNullOrEmpty(pers.Sex))
                outputFile.WriteLine($"1 SEX {pers.Sex}");
            
            outputFile.WriteLine($"1 BIRT");
            
            if (!string.IsNullOrEmpty(pers.BirthDate))
                outputFile.WriteLine($"2 DATE {pers.BirthDate}");
            if (!string.IsNullOrEmpty(pers.BirthLocation))
            {
                outputFile.WriteLine($"2 PLAC {pers.BirthLocation}");
            }
            outputFile.WriteLine($"1 DEAT");
            
            if (!string.IsNullOrEmpty(pers.DeathDate))
            {
                outputFile.WriteLine($"2 DATE {pers.DeathDate}");
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

        foreach (var family in Families)
        {
            outputFile.WriteLine(family.Fam);
            if (family.Husband != null)
                outputFile.WriteLine(family.Husband.Replace("INDI", ""));
            if (family.Wife != null)
                outputFile.WriteLine(family.Wife.Replace("INDI", ""));
            if (family.Children != null)
                foreach (var child in family.Children)
                    outputFile.WriteLine(child);
                
            if (family.Marriage != null)
            {   
                if (family.Husband!.Contains('P') || family.Wife!.Contains('P'))
                    outputFile.WriteLine($"1 NO MARR");
                else
                    outputFile.WriteLine($"1 MARR");
                if (!string.IsNullOrEmpty(family.Marriage.MarriageDate))
                    outputFile.WriteLine($"2 DATE {family.Marriage.MarriageDate}");
                if (!string.IsNullOrEmpty(family.Marriage.MarriagePlace))
                    outputFile.WriteLine($"2 PLAC {family.Marriage.MarriagePlace}");
            }
        }
        
        outputFile.WriteLine("0 TRLR");
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
    [GeneratedRegex(@"((?:^|\W)married(?:$|\W)[A-Za-z0-9\s(1)""']*\,\.)|(married (_*\s[A-Za-z\s]*)|married\s[A-Za-z0-9\s(1)""'\._]*\W)")]
    private static partial Regex MarriedInfo();
//---  
//---
    [GeneratedRegex(@"(ABT|BEF|AFT|BET)*((\d))* (?<month>(JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?\s*[0-9]{4}\b( AND [\w \d}]*)*")]
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
    [GeneratedRegex(@"(Jr)|(Sr)|(III)|(II)|(IV)")]
    private static partial Regex GetSuffix();

}
