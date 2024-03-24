using ChunkL.Structure;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChunkL.Serialization;

internal sealed partial class BodyReader(TextReader reader)
{
    private readonly TextReader reader = reader ?? throw new ArgumentNullException(nameof(reader));

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ChunkDefinitionRegexPattern = @"^(?:(?:0x)?([0-9a-fA-F]{3}|[0-9a-fA-F]{8}))(?:\s+\((.+?)\))?(?:\s+\[(.+?)\])?\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ChunkMemberRegexPattern = @"^(\s+)(.+?)(\?)?(?:\s+(\w+|"".+""))?(?:\s*=\s*([\w\-.?]+))?(?:\s+\((.+?)\))?\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberVersionRegexPattern = @"^v([0-9]+)([+-=])$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberEnumRegexPattern = @"^(int|byte)<(\w+)>$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberIfRegexPattern = @"^if\s+(.*?)(?:\/\/)?\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberAssignRegexPattern = @"^(\w+)\s*=\s*([\w\-.?]+)\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ArchiveDefinitionRegexPattern = @"^archive(?:\s+(\w+))?(?:\s+\((.+?)\))?\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string IfConditionRegexPattern = @"\(|\)|&&|&|\|\||\||"".*""|\w+|""|<<|>>|!=|==|>=|<=|>|<|!|\+|-|\*|/|::";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string TypeRegexPattern = @"^(\w+)(?:<(\w+)(\*|\^)?>)?(\*|\^)?(\[(\w*)\])?(_deprec)?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string EnumDefinitionRegexPattern = @"^enum(?:\s+(\w+))\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string EnumValueRegexPattern = @"^(\s+)(\w+)(?:\s*=\s*([\w.?]+))?\s*(?:\/\/\s*(.*))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ThrowRegexPattern = @"^\s+throw(?:\s+(\w+)(?:\(""(.*)""\))?)?\s*(?:\/\/.*)?$";

#if NETSTANDARD2_0
    private static readonly Regex chunkDefinitionRegex = new(ChunkDefinitionRegexPattern, RegexOptions.Compiled);
    private static Regex ChunkDefinitionRegex() => chunkDefinitionRegex;

    private static readonly Regex chunkMemberRegex = new(ChunkMemberRegexPattern, RegexOptions.Compiled);
    private static Regex ChunkMemberRegex() => chunkMemberRegex;
    
    private static readonly Regex memberVersionRegex = new(MemberVersionRegexPattern, RegexOptions.Compiled);
    private static Regex MemberVersionRegex() => memberVersionRegex;

    private static readonly Regex memberEnumRegex = new(MemberEnumRegexPattern, RegexOptions.Compiled);
    private static Regex MemberEnumRegex() => memberEnumRegex;

    private static readonly Regex memberIfRegex = new(MemberIfRegexPattern, RegexOptions.Compiled);
    private static Regex MemberIfRegex() => memberIfRegex;

    private static readonly Regex memberAssignRegex = new(MemberAssignRegexPattern, RegexOptions.Compiled);
    private static Regex MemberAssignRegex() => memberAssignRegex;

    private static readonly Regex archiveDefinitionRegex = new(ArchiveDefinitionRegexPattern, RegexOptions.Compiled);
    private static Regex ArchiveDefinitionRegex() => archiveDefinitionRegex;

    private static readonly Regex ifConditionRegex = new(IfConditionRegexPattern, RegexOptions.Compiled);
    private static Regex IfConditionRegex() => ifConditionRegex;

    private static readonly Regex typeRegex = new(TypeRegexPattern, RegexOptions.Compiled);
    private static Regex TypeRegex() => typeRegex;

    private static readonly Regex enumDefinitionRegex = new(EnumDefinitionRegexPattern, RegexOptions.Compiled);
    private static Regex EnumDefinitionRegex() => enumDefinitionRegex;

    private static readonly Regex enumValueRegex = new(EnumValueRegexPattern, RegexOptions.Compiled);
    private static Regex EnumValueRegex() => enumValueRegex;

    private static readonly Regex throwRegex = new(ThrowRegexPattern, RegexOptions.Compiled);
    private static Regex ThrowRegex() => throwRegex;
#else
    [GeneratedRegex(ChunkDefinitionRegexPattern)]
    private static partial Regex ChunkDefinitionRegex();

    [GeneratedRegex(ChunkMemberRegexPattern)]
    private static partial Regex ChunkMemberRegex();

    [GeneratedRegex(MemberVersionRegexPattern)]
    private static partial Regex MemberVersionRegex();

    [GeneratedRegex(MemberEnumRegexPattern)]
    private static partial Regex MemberEnumRegex();

    [GeneratedRegex(MemberIfRegexPattern)]
    private static partial Regex MemberIfRegex();

    [GeneratedRegex(MemberAssignRegexPattern)]
    private static partial Regex MemberAssignRegex();

    [GeneratedRegex(ArchiveDefinitionRegexPattern)]
    private static partial Regex ArchiveDefinitionRegex();

    [GeneratedRegex(IfConditionRegexPattern)]
    private static partial Regex IfConditionRegex();

    [GeneratedRegex(TypeRegexPattern)]
    private static partial Regex TypeRegex();

    [GeneratedRegex(EnumDefinitionRegexPattern)]
    private static partial Regex EnumDefinitionRegex();

    [GeneratedRegex(EnumValueRegexPattern)]
    private static partial Regex EnumValueRegex();

    [GeneratedRegex(ThrowRegexPattern)]
    private static partial Regex ThrowRegex();
#endif

    public BodyModel Read()
    {
        var chunkDefinitions = new List<ChunkDefinition>();
        var archiveDefinitions = new List<ArchiveDefinition>();
        var enumDefinitions = new List<EnumDefinition>();
        var existingNames = new HashSet<string>();

        // read chunk definitions until end
        string? chunkDef;
        while ((chunkDef = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(chunkDef))
            {
                continue;
            }

            var chunkDefinitionMatch = ChunkDefinitionRegex().Match(chunkDef);

            if (!chunkDefinitionMatch.Success)
            {
                var archiveDefinitionMatch = ArchiveDefinitionRegex().Match(chunkDef);

                if (!archiveDefinitionMatch.Success)
                {
                    var enumDefinitionMatch = EnumDefinitionRegex().Match(chunkDef);

                    if (!enumDefinitionMatch.Success)
                    {
                        throw new Exception("Deserialize failed: Expected chunk/archive/enum definition");
                    }

                    var enumDefinition = new EnumDefinition
                    {
                        Name = enumDefinitionMatch.Groups[1].Value,
                        Description = enumDefinitionMatch.Groups[2].Value
                    };

                    ReadEnumValues(enumDefinition.Values);

                    enumDefinitions.Add(enumDefinition);

                    continue;
                }

                var archiveDefinition = new ArchiveDefinition
                {
                    Name = archiveDefinitionMatch.Groups[1].Value,
                    Description = archiveDefinitionMatch.Groups[3].Value
                };

                ReadProperties(archiveDefinition.Properties, archiveDefinitionMatch.Groups[2].Value);

                archiveDefinitions.Add(archiveDefinition);

                _ = ReadMembers(archiveDefinition.Members, expectedIndent: 1, expectsLowerIndent: false, existingNames);

                continue;
            }

            var chunkDefinition = new ChunkDefinition
            {
                Id = uint.Parse(chunkDefinitionMatch.Groups[1].Value, NumberStyles.HexNumber),
                Description = chunkDefinitionMatch.Groups[4].Value
            };

            var properties = chunkDefinitionMatch.Groups[2].Value;
            var versions = chunkDefinitionMatch.Groups[3].Value;

            if (!string.IsNullOrEmpty(properties))
            {
                ReadProperties(chunkDefinition.Properties, properties);
            }

            if (!string.IsNullOrEmpty(versions))
            {
                ReadChunkVersions(chunkDefinition.Versions, versions);
            }

            chunkDefinitions.Add(chunkDefinition);

            _ = ReadMembers(chunkDefinition.Members, expectedIndent: 1, expectsLowerIndent: false, existingNames);
        }

        return new BodyModel
        {
            ChunkDefinitions = chunkDefinitions,
            ArchiveDefinitions = archiveDefinitions,
            EnumDefinitions = enumDefinitions
        };
    }

    private static void ReadProperties(Dictionary<string, string> dictionary, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var split = input.Split(',');

        foreach (var prop in split)
        {
            var propSplit = prop.Split(':');

            if (propSplit.Length < 1 || propSplit.Length > 2)
            {
                throw new Exception("Deserialize failed: Expected properties");
            }

            var key = propSplit[0].Trim();
            var value = "";

            if (propSplit.Length == 2)
            {
                value = propSplit[1].Trim();
            }

            dictionary[key] = value;
        }
    }

    private static void ReadChunkVersions(Dictionary<string, int?> dictionary, string input)
    {
        var split = input.Split(',');

        foreach (var prop in split)
        {
            var propSplit = prop.Split([".v"], StringSplitOptions.RemoveEmptyEntries);

            if (propSplit.Length < 1 || propSplit.Length > 2)
            {
                throw new Exception("Deserialize failed: Expected chunk versions");
            }

            var key = propSplit[0].Trim();
            var value = default(int?);

            if (propSplit.Length == 2)
            {
                value = int.Parse(propSplit[1].Trim());
            }

            dictionary[key] = value;
        }
    }

    private Match? ReadMembers(List<IChunkMember> members, int expectedIndent, bool expectsLowerIndent, HashSet<string> existingNames)
    {
        // read features until empty/whitespace line
        string? member;
        while (!string.IsNullOrWhiteSpace(member = reader.ReadLine()))
        {
            var memberMatch = ChunkMemberRegex().Match(member);

            if (!memberMatch.Success)
            {
                throw new Exception("Deserialize failed: Expected chunk member");
            }

            MemberMatched:

            var givenIndent = memberMatch.Groups[1].Value.Length;

            if (givenIndent < expectedIndent)
            {
                if (expectsLowerIndent)
                {
                    return memberMatch;
                }
                else
                {
                    throw new Exception("Deserialize failed: Expected chunk member");
                }
            }

            if (givenIndent != 0 && givenIndent != expectedIndent)
            {
                throw new Exception("Deserialize failed: Expected chunk member");
            }

            var justMember = memberMatch.Value.TrimStart();

            var ifMatch = MemberIfRegex().Match(justMember);

            if (ifMatch.Success)
            {
                var condition = ifMatch.Groups[1].Value;

                var matches = IfConditionRegex().Matches(condition);

                if (matches.Count == 0)
                {
                    throw new Exception("Deserialize failed: Expected if condition");
                }

                var ifMember = new ChunkIfStatement
                {
                    Description = ifMatch.Groups[2].Value
                };

                foreach (var match in matches.Cast<Match>())
                {
                    ifMember.Condition.Add(match.Value);
                }

                members.Add(ifMember);

                memberMatch = ReadMembers(ifMember.Members, expectedIndent + 1, expectsLowerIndent: true, existingNames);

                if (memberMatch is null)
                {
                    return null;
                }
                else
                {
                    goto MemberMatched;
                }
            }

            var type = memberMatch.Groups[2].Value;
            var properties = memberMatch.Groups[6].Value;

            var propertiesDict = default(Dictionary<string, string>);
            if (!string.IsNullOrEmpty(properties))
            {
                propertiesDict = [];
                ReadProperties(propertiesDict, properties);
            }

            var memberDescription = memberMatch.Groups[7].Value;

            var versionMatch = MemberVersionRegex().Match(type);

            if (versionMatch.Success)
            {
                var chunkVersion = new ChunkVersion
                {
                    Number = int.Parse(versionMatch.Groups[1].Value),
                    Operator = versionMatch.Groups[2].Value,
                    Description = memberDescription
                };

                members.Add(chunkVersion);

                memberMatch = ReadMembers(chunkVersion.Members, expectedIndent + 1, expectsLowerIndent: true, existingNames);

                if (memberMatch is null)
                {
                    return null;
                }
                else
                {
                    goto MemberMatched;
                }
            }

            if (type == "throw" || type.StartsWith("throw "))
            {
                var throwMatch = ThrowRegex().Match(member);

                if (!throwMatch.Success)
                {
                    throw new Exception("Deserialize failed: Expected throw statement");
                }

                members.Add(new ChunkThrow
                {
                    Exception = throwMatch.Groups[1].Value,
                    Message = throwMatch.Groups[2].Value,
                    Description = memberDescription
                });

                continue;
            }

            var nullable = memberMatch.Groups[3].Success;
            var name = memberMatch.Groups[4].Value.Trim('"');
            if (!string.IsNullOrWhiteSpace(name)) existingNames.Add(name);
            var defaultValue = memberMatch.Groups[5].Value;

            var enumMatch = MemberEnumRegex().Match(type);

            if (enumMatch.Success)
            {
                type = enumMatch.Groups[1].Value;
                var enumType = enumMatch.Groups[2].Value;

                members.Add(new ChunkEnum
                {
                    Type = new PropertyType { PrimaryType = type },
                    IsNullable = nullable,
                    Name = name,
                    Description = memberDescription,
                    EnumType = enumType,
                    DefaultValue = defaultValue,
                    Properties = propertiesDict ?? []
                });

                continue;
            }

            var assignMatch = MemberAssignRegex().Match(justMember);

            if (assignMatch.Success)
            {
                var left = assignMatch.Groups[1].Value;

                if (existingNames.Contains(left))
                {
                    members.Add(new ChunkMemberAssign
                    {
                        Left = left,
                        Right = assignMatch.Groups[2].Value,
                        Description = assignMatch.Groups[3].Value
                    });

                    continue;
                }
            }

            var typeBreakdown = TypeRegex().Match(type);

            var propertyType = new PropertyType
            {
                PrimaryType = typeBreakdown.Groups[1].Value,
                GenericType = typeBreakdown.Groups[2].Value,
                GenericTypeMarker = typeBreakdown.Groups[3].Value,
                PrimaryTypeMarker = typeBreakdown.Groups[4].Value,
                IsArray = typeBreakdown.Groups[5].Success,
                ArrayLength = typeBreakdown.Groups[6].Value,
                IsDeprec = typeBreakdown.Groups[7].Success
            };

            members.Add(new ChunkProperty
            {
                Type = propertyType,
                IsNullable = nullable,
                Name = name,
                Description = memberDescription,
                DefaultValue = defaultValue,
                Properties = propertiesDict ?? []
            });
        }

        return null;
    }

    private void ReadEnumValues(List<EnumValue> values)
    {
        var currentIndent = 0;

        string? value;
        while (!string.IsNullOrWhiteSpace(value = reader.ReadLine()))
        {
            var valueMatch = EnumValueRegex().Match(value);

            if (!valueMatch.Success)
            {
                throw new Exception("Deserialize failed: Expected enum value");
            }

            var givenIndent = valueMatch.Groups[1].Value.Length;

            if (givenIndent < currentIndent)
            {
                return;
            }

            if (currentIndent != 0 && givenIndent > currentIndent)
            {
                throw new Exception("Deserialize failed: Expected enum value");
            }

            currentIndent = givenIndent;

            var name = valueMatch.Groups[2].Value;
            var explicitValue = valueMatch.Groups[3].Value;
            var description = valueMatch.Groups[4].Value;

            values.Add(new EnumValue
            {
                Name = name,
                ExplicitValue = explicitValue,
                Description = description
            });
        }
    }
}
