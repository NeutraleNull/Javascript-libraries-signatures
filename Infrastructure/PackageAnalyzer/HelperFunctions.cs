using System.Text.RegularExpressions;

namespace Infrastructure.PackageAnalyzer;

public static class HelperFunctions
{
    /// <summary>
    /// This is a function that helps to determine whether a provided file is a module or not.
    /// It also requires the code as a string to avoid double reading in the file scenarios. 
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    public static bool IsModule(string filePath, string code)
    {
        // Check for common module file extensions
        if (filePath.EndsWith(".mjs") || filePath.EndsWith(".esm.js"))
        {
            return true;
        }

        // Check for the presence of import or export statements
        if (code.Contains("import") || code.Contains("export"))
        {
            // Exclude cases where import or export are used as variable or function names
            if (Regex.IsMatch(code, @"\b(var|let|const)\s+import\b", RegexOptions.Compiled) ||
                Regex.IsMatch(code, @"\b(var|let|const)\s+export\b", RegexOptions.Compiled) ||
                Regex.IsMatch(code, @"\bfunction\s+import\b", RegexOptions.Compiled) ||
                Regex.IsMatch(code, @"\bfunction\s+export\b", RegexOptions.Compiled))
            {
                return false;
            }

            // Exclude cases where import or export are used inside comments or strings
            var commentRegex = new Regex(@"(/\*[\s\S]*?\*/|//.*$)", RegexOptions.Multiline | RegexOptions.Compiled);
            var stringRegex = new Regex("\"(?:\\\\\"|[^\"])*\"|'(?:\\\\'|[^'])*'", RegexOptions.Compiled);
            var codeWithoutCommentsAndStrings = commentRegex.Replace(code, "");
            codeWithoutCommentsAndStrings = stringRegex.Replace(codeWithoutCommentsAndStrings, "");

            if (codeWithoutCommentsAndStrings.Contains("import") ||
                codeWithoutCommentsAndStrings.Contains("export"))
            {
                return true;
            }
        }

        // Check for the presence of require or module.exports
        if (code.Contains("require") || code.Contains("module.exports"))
        {
            // Exclude cases where require or module.exports are used as variable or function names
            if (Regex.IsMatch(code, @"\b(var|let|const)\s+require\b", RegexOptions.Compiled) ||
                Regex.IsMatch(code, @"\b(var|let|const)\s+module\.exports\b", RegexOptions.Compiled) ||
                Regex.IsMatch(code, @"\bfunction\s+require\b", RegexOptions.Compiled) ||
                Regex.IsMatch(code, @"\bfunction\s+module\.exports\b", RegexOptions.Compiled))
            {
                return false;
            }

            // Exclude cases where require or module.exports are used inside comments or strings
            var commentRegex = new Regex(@"(/\*[\s\S]*?\*/|//.*$)", RegexOptions.Multiline | RegexOptions.Compiled);
            var stringRegex = new Regex("\"(?:\\\\\"|[^\"])*\"|'(?:\\\\'|[^'])*'", RegexOptions.Compiled);
            var codeWithoutCommentsAndStrings = commentRegex.Replace(code, "");
            codeWithoutCommentsAndStrings = stringRegex.Replace(codeWithoutCommentsAndStrings, "");

            if (codeWithoutCommentsAndStrings.Contains("require") ||
                codeWithoutCommentsAndStrings.Contains("module.exports"))
            {
                return true;
            }
        }

        return false;
    }
    
    
}