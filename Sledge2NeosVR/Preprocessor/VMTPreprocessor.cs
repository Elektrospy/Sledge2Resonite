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

            parsedFile = replaceComMulti.Replace(input, replaceComSubMulti); // works :)
            parsedFile = suffixReplaceMulti.Replace(parsedFile, suffixReplaceSubMulti);
            parsedFile = sufficReplaceWsMulti.Replace(parsedFile, string.Empty);
            parsedFile = parsedFile.Replace("\"\"{", "\"{");
            parsedFile = parsedFile.Replace("}\"\"", "}\"");
            parsedFile = parsedFile.Replace("\\", "/");
            parsedFile = newObjQMarkMulti.Replace(parsedFile, objQMarkReplaceSub);

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