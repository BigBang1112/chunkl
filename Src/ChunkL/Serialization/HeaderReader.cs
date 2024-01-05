using ChunkL.Structure;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChunkL.Serialization;

internal sealed partial class HeaderReader(TextReader reader)
{
    private readonly TextReader reader = reader ?? throw new ArgumentNullException(nameof(reader));

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string DescriberRegexPattern = @"^(\w+)\s+(0x)?([0-9a-fA-F]{8})\s*(\/\/\s*(.+))?";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string FeatureRegexPattern = @"^-\s*(\w+):\s*(.+)";

#if NETSTANDARD2_0
    private static readonly Regex describerRegex = new(DescriberRegexPattern, RegexOptions.Compiled);
    private static Regex DescriberRegex() => describerRegex;

    private static readonly Regex featureRegex = new(FeatureRegexPattern, RegexOptions.Compiled);
    private static Regex FeatureRegex() => featureRegex;
#else
    [GeneratedRegex(DescriberRegexPattern)]
    private static partial Regex DescriberRegex();

    [GeneratedRegex(FeatureRegexPattern)]
    private static partial Regex FeatureRegex();
#endif

    public HeaderModel Read()
    {
        var describer = reader.ReadLine() ?? throw new Exception("Deserialize failed: Expected describer");

        var describerMatch = DescriberRegex().Match(describer);

        if (!describerMatch.Success)
        {
            throw new Exception("Deserialize failed: Expected describer");
        }

        var name = describerMatch.Groups[1].Value;
        var id = uint.Parse(describerMatch.Groups[3].Value, NumberStyles.HexNumber);
        var description = describerMatch.Groups[5].Value;

        var features = new Dictionary<string, string>();

        // read features until empty/whitespace line
        string? feature;
        while (!string.IsNullOrWhiteSpace(feature = reader.ReadLine()))
        {
            var featureMatch = FeatureRegex().Match(feature);

            if (!featureMatch.Success)
            {
                throw new Exception("Deserialize failed: Expected feature");
            }

            var featureName = featureMatch.Groups[1].Value;
            var featureValue = featureMatch.Groups[2].Value;

            features.Add(featureName, featureValue);
        }

        return new HeaderModel
        {
            Name = name,
            Id = id,
            Description = description,
            Features = features
        };
    }
}
