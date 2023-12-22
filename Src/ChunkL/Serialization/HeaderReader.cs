using ChunkL.Structure;
using System.Text.RegularExpressions;

namespace ChunkL.Serialization;

internal sealed class HeaderReader(TextReader reader)
{
    public HeaderModel Read()
    {
        var describer = reader.ReadLine() ?? throw new Exception("Deserialize failed: Expected describer");

        var describerMatch = Regex.Match(describer, @"^(\w+)\s+(0x)?([0-9A-F]{8})\s*(\/\/\s*(.+))?");

        if (!describerMatch.Success)
        {
            throw new Exception("Deserialize failed: Expected describer");
        }

        var name = describerMatch.Groups[1].Value;
        var id = uint.Parse(describerMatch.Groups[3].Value);
        var comment = describerMatch.Groups[5].Value;

        // read features until empty/whitespace line
        string? feature;
        while (!string.IsNullOrWhiteSpace(feature = reader.ReadLine()))
        {
            var featureMatch = Regex.Match(feature, @"^-\s*([a-zA-Z]+):\s*([\w\s]+)\s*(\/\/\s*(.+))?");

            if (!featureMatch.Success)
            {
                throw new Exception("Deserialize failed: Expected feature");
            }

            var featureName = featureMatch.Groups[1].Value;
            var featureValue = featureMatch.Groups[2].Value;
            var featureComment = featureMatch.Groups[4].Value;
        }

        return new HeaderModel();
    }
}
