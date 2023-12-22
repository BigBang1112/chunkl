#if NETSTANDARD2_0

namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class StringSyntaxAttribute(string syntax) : Attribute
{
    public string Syntax { get; } = syntax;

    public const string Regex = nameof(Regex);
}

#endif