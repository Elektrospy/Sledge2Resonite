using System.Linq;
using System.Text.RegularExpressions;

namespace Sledge2NeosVR;

public class VMTPreprocessor : IPreprocessor
{
    public bool Preprocess(string[] input, out string[] parsedFile)
    {
        base.Preprocess(input, out parsedFile);

        parsedFile[0] = input[0].Replace("\"", "");

        for (int i = 1; i < input.Length; i++)
        {
            // Determine if its suffix (rest of the string) starts and ends with quotation marks
            if (!validPrefix.Match(input[i]).Success)
            {
                continue;
            }

            // Remove any trailing comments
            string restOfString = Regex.Replace(input[i].Substring(input[i].LastIndexOf(' ')), @"\s+//.+?\n", string.Empty);

            var count = restOfString.ToCharArray().Count(x => x == '\"');

            if (count < 1 || count > 3)
            {
                parsedFile[i] = Convert(restOfString);
            }
            else
            {
                // If we have got here, then the passed line was valid and we can assign it to parsedFile
                parsedFile[i] = input[i];
            }
        }

        return true;
    }

    private string Convert(string input)
    {
        return string.Empty;
    }

    private void CreateBitmap()
    {

    }

}