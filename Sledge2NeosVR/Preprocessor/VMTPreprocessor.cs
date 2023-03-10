using BaseX;
using System;

namespace Sledge2NeosVR;

public class VMTPreprocessor : Preprocessor
{
    public bool ParseVmt(string input, out string parsedFile)
    {
        try
        {
            input = input.ToLower();

            if (!Preprocess(input))
            {
                UniLog.Error("Invalid file string array supplied in VMTPreprocessor");
                parsedFile = string.Empty;
                return false;
            }

            parsedFile = replaceCommentsMulti.Replace(input, replaceCommentsSubstitutionMulti); // works :)
            parsedFile = suffixReplacementMulti.Replace(parsedFile, suffixReplacementSubstitutionMulti);
            parsedFile = sufficReplaceWhitespacesMulti.Replace(parsedFile, string.Empty);
            parsedFile = parsedFile.Replace("\"\"{", "\"{");
            parsedFile = parsedFile.Replace("}\"\"", "}\"");
            parsedFile = parsedFile.Replace("\\", "/");
            string firstline = parsedFile.Substring(0, parsedFile.IndexOf(Environment.NewLine));

            string parsedFirstLine = firstline.Replace("\"", "");
            parsedFile = parsedFile.Replace(firstline, parsedFirstLine);

            return true;
        }
        catch (Exception ex)
        {
            UniLog.Error($"ParseVmt failed with error: {ex.Message}");    
            parsedFile = string.Empty;
        }

        return false;
    }
}