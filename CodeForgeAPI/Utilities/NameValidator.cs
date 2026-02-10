using System.Text.RegularExpressions;

namespace CodeForgeAPI.Utilities;

public static class NameValidator
{
    private static readonly Regex ValidIdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    
    private static readonly HashSet<string> CSharpReservedKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while"
    };
    
    private static readonly HashSet<string> JavaScriptReservedKeywords = new()
    {
        "abstract", "arguments", "await", "boolean", "break", "byte", "case", "catch", "char",
        "class", "const", "continue", "debugger", "default", "delete", "do", "double", "else",
        "enum", "eval", "export", "extends", "false", "final", "finally", "float", "for",
        "function", "goto", "if", "implements", "import", "in", "instanceof", "int", "interface",
        "let", "long", "native", "new", "null", "package", "private", "protected", "public",
        "return", "short", "static", "super", "switch", "synchronized", "this", "throw",
        "throws", "transient", "true", "try", "typeof", "var", "void", "volatile", "while",
        "with", "yield"
    };
    
    public static bool IsValidIdentifier(string name, string targetStack)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        
        // Check format (must start with letter or underscore, contain only alphanumerics and underscores)
        if (!ValidIdentifierRegex.IsMatch(name))
            return false;
        
        // Check against reserved keywords
        var keywords = targetStack.StartsWith("CSharp") ? CSharpReservedKeywords : JavaScriptReservedKeywords;
        if (keywords.Contains(name.ToLower()))
            return false;
        
        return true;
    }
    
    public static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "UnnamedField";
        
        // Remove special characters, keep only alphanumerics and underscores
        var sanitized = Regex.Replace(name, @"[^A-Za-z0-9_]", "");
        
        // Ensure it starts with a letter or underscore
        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]) && sanitized[0] != '_')
            sanitized = "_" + sanitized;
        
        return string.IsNullOrEmpty(sanitized) ? "UnnamedField" : sanitized;
    }
}
