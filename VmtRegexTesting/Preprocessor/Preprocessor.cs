using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sledge2NeosVR;

/// <summary>
/// Does not touch source file, only makes a copy and returns a parsed version
/// </summary>
public abstract class Preprocessor
{
    protected static readonly Regex replaceComments = new Regex(@"\s+//.*$");
    protected static readonly string replaceCommentsSubstitution = string.Empty;

    protected static readonly Regex replaceCommentsMulti = new Regex(@"\s+//.+?\n");
    protected static readonly string replaceCommentsSubstitutionMulti = Environment.NewLine;
               
    protected static readonly Regex suffixReplacement = new Regex(@"(?:""?([^\s""]+)""?[\r\t\f ]+""?([^{/\n\s][^\n]*?)""?\s*$)");
    protected static readonly string suffixReplacementSubstitution = @"""$1"" ""$2""";

    protected static readonly Regex suffixReplacementMulti = new Regex(@"(?:""?([^\s""]+)""?[\r\t\f ]+""?([^{/\n\s][^\n]*?)""?\s*\n)");
    protected const string suffixReplacementSubstitutionMulti = @"""$1"" ""$2""" + "\n";

    protected bool Preprocess(string input)
    {
        if (input.Length == 0)
        {
            return false;
        }

        // Regex to identify valid prefixes
        return true;
    }
}