using System;
using System.Text.RegularExpressions;

namespace Sledge2NeosVR;

/// <summary>
/// Does not touch source file, only makes a copy and returns a parsed version
/// </summary>
public abstract class Preprocessor
{
    protected static readonly RegexOptions options = RegexOptions.Multiline;

    protected static readonly Regex replaceComments = new Regex(@"\s+//.*$");
    protected static readonly string replaceCommentsSubstitution = string.Empty;

    protected static readonly Regex replaceCommentsMulti = new Regex(@"\s*//.+?\n", options);
    protected static readonly string replaceCommentsSubstitutionMulti = Environment.NewLine;

    protected static readonly Regex suffixReplacement = new Regex(@"(?:""?([^\s""]+)""?[\r\t\f ]+""?([^{/\n\s][^\n]*?)""?\s*$)");
    protected static readonly string suffixReplacementSubstitution = @"""$1"" ""$2""";

    protected static readonly Regex suffixReplacementMulti = new Regex(@"(?:""?([^\s""]+)""?[\r\t\f ]+""?([^{/\n\s][^\n]*?)""?\s*\n)", options);
    protected const string suffixReplacementSubstitutionMulti = @"""$1"" ""$2""" + "\n";

    protected static readonly Regex sufficReplaceWhitespacesMulti = new Regex(@"^\s*$[\r\n]*", options);

    protected bool Preprocess(string input)
    {
        return input.Length != 0;
    }
}