using System.Linq;
using System.Text.RegularExpressions;

namespace Sledge2NeosVR;

/// <summary>
/// Does not touch source file, only makes a copy and returns a parsed version
/// </summary>
public abstract class IPreprocessor
{
    protected string commentsRegex = @"\s+//.+?\n";
    protected Regex replaceComments = new Regex(@"\s+//.+?\n");
    protected Regex suffixReplacement = new Regex("(?:\"\"?([\\$\\%\\d\\w]+)\"\"?[\\r\\t\\f ]+\"\"?([^/][^\\n]*?)\"\"?( // .+?)?\\n)");

    public bool Preprocess(string[] input, out string[] parsedFile)
    {
        if (input.Length == 0)
        {
            parsedFile = Enumerable.Empty<string>().ToArray();
            return false;
        }

        // Regex to identify valid prefixes
        parsedFile = new string[input.Length];
        return true;
    }
}