using ChunkL.Structure;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChunkL.Serialization;

internal sealed partial class BodyReader(TextReader reader)
{
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ChunkDefinitionRegexPattern = @"^((0x)?([0-9a-fA-F]{3}))(\s+\((.+?)\))?\s*(\/\/\s*(.+))?";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ChunkMemberRegexPattern = @"^(\s+)(.+?)(\?)?(\s+(\w+))?\s*(\/\/\s*(.+))?$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberVersionRegexPattern = @"^v([0-9]+)([+-=])$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberEnumRegexPattern = @"^(int|byte)<(\w+)>$";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string MemberIfRegexPattern = @"^if\s+(!?\w+)(\s*(>=|<=|==|>|<)\s*(\w+))?\s*(\/\/\s*(.+))?";

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
#endif

    public BodyModel Read()
    {
        var chunkDefinitions = new List<ChunkDefinition>();

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
                throw new Exception("Deserialize failed: Expected chunk definition");
            }

            var chunkDefinition = new ChunkDefinition
            {
                Id = uint.Parse(chunkDefinitionMatch.Groups[3].Value, NumberStyles.HexNumber),
                Properties = chunkDefinitionMatch.Groups[5].Value,
                Description = chunkDefinitionMatch.Groups[7].Value
            };

            chunkDefinitions.Add(chunkDefinition);

            _ = ReadChunkMembers(chunkDefinition, chunkDefinition.Members, expectedIndent: 1, expectsLowerIndent: false);
        }

        return new BodyModel
        {
            ChunkDefinitions = chunkDefinitions
        };
    }

    private Match? ReadChunkMembers(ChunkDefinition chunkDefinition, List<IChunkMember> members, int expectedIndent, bool expectsLowerIndent)
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

            var ifMatch = MemberIfRegex().Match(memberMatch.Value.TrimStart());

            if (ifMatch.Success)
            {
                var ifMember = new ChunkIfStatement
                {
                    Left = ifMatch.Groups[1].Value,
                    Operator = ifMatch.Groups[3].Value,
                    Right = ifMatch.Groups[4].Value,
                    Description = ifMatch.Groups[6].Value
                };

                members.Add(ifMember);

                memberMatch = ReadChunkMembers(chunkDefinition, ifMember.Members, expectedIndent + 1, expectsLowerIndent: true);

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

                memberMatch = ReadChunkMembers(chunkDefinition, chunkVersion.Members, expectedIndent + 1, expectsLowerIndent: true);

                if (memberMatch is null)
                {
                    return null;
                }
                else
                {
                    goto MemberMatched;
                }
            }

            var nullable = memberMatch.Groups[3].Success;
            var name = memberMatch.Groups[5].Value;

            var enumMatch = MemberEnumRegex().Match(type);

            if (enumMatch.Success)
            {
                type = enumMatch.Groups[1].Value;
                var enumType = enumMatch.Groups[2].Value;

                members.Add(new ChunkEnum
                {
                    Type = type,
                    IsNullable = nullable,
                    Name = name,
                    Description = memberDescription,
                    EnumType = enumType
                });

                continue;
            }

            members.Add(new ChunkProperty
            {
                Type = type,
                IsNullable = nullable,
                Name = name,
                Description = memberDescription
            });
        }

        return null;
    }
}
