namespace Sledge2Resonite
{
    public class VMTPreprocessor : Preprocessor
    {
        public bool ParseVmt(string input, out string parsedFile)
        {
            try
            {
                input = input.ToLower();

                if (!Preprocess(input))
                {
                    Console.WriteLine("Invalid file string array supplied");
                    parsedFile = string.Empty;
                    return false;
                }

                // Remove enclosing quotation marks from first line
                // UniLog.Log("Remove enclosing quotation marks from first line");

                // remove trailing comments
                parsedFile = replaceComMulti.Replace(input, replaceComSubMulti); // works :)
                // UniLog.Log(parsedFile);
                // UniLog.Log("add missing quotation marks");
                // add missing quotation marks
                parsedFile = suffixReplaceMulti.Replace(parsedFile, suffixReplaceSubMulti);
                // UniLog.Log(parsedFile);
                // remove double quotation marks around float values
                parsedFile = parsedFile.Replace("\"\"{", "\"{");
                parsedFile = parsedFile.Replace("}\"\"", "}\"");
                // make all paths us \ instead of /
                parsedFile = parsedFile.Replace("\\", "/");
                // remove all whitespace lines
                parsedFile = sufficReplaceWsMulti.Replace(parsedFile, string.Empty);
                // clean up all object name lines
                parsedFile = newObjQMarkMulti.Replace(parsedFile, objQMarkReplaceSub);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ParseVmt failed with error: {ex}");
                parsedFile = string.Empty;
            }

            return false;
        }
    }
}