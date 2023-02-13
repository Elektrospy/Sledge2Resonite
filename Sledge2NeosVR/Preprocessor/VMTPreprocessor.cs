using BaseX;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sledge2NeosVR;

public class VMTPreprocessor : Preprocessor
{
    public bool Preprocess(string[] input, out string[] parsedFile)
    {
        try
        {
            base.Preprocess(input, out parsedFile);
            // Remove enclosing quotation marks from first line
            UniLog.Log("Remove enclosing quotation marks from first line");
            parsedFile[0] = input[0].Replace("\"", "");
            // loop trough indiviual lines
            for (int i = 1; i < input.Length; i++)
            {
                // Determine if its suffix (rest of the string) starts and ends with quotation marks
                /*
                if (!validPrefix.Match(input[i]).Success)
                {
                    continue;
                }
                */
                // Remove any trailing comments
                UniLog.Log($"Remove any trailing comments from: {input[i]}");
                string restOfString = replaceComments.Replace(input[i], string.Empty);
                UniLog.Log($"cleaned string: {restOfString}");
                var count = restOfString.ToCharArray().Count(x => x == '\"');
                if (count < 1 || count > 3)
                {
                    // try to add missing quotation marks
                    parsedFile[i] = Convert(restOfString);
                }
                else
                {
                    // If we have got here, then the passed line was valid and we can assign it to parsedFile
                    parsedFile[i] = input[i];
                }
            }
        }
        catch (Exception ex)
        {
            UniLog.Error($"Preprocess failed with error: {ex}");
            parsedFile = new string[0];
            return false;
        }

        return true;
    }

    /// <summary>
    /// tries to fix misplaced or missing quotation marks in object body
    /// </summary>
    private string Convert(string input)
    {
        return suffixReplacement.Replace(input, string.Empty);
    }

}