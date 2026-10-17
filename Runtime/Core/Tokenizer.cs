using System.Collections.Generic;
using System.Text;

namespace UniversalConsole
{
    /// <summary>Splits a line into tokens with "double quotes", 'single quotes' and \ escaping.</summary>
    public static class Tokenizer
    {
        /// <summary>Tokenizes an input line.</summary>
        public static List<string> Tokenize(string line)
        {
            var out_ = new List<string>();
            if (string.IsNullOrEmpty(line)) return out_;
            var sb = new StringBuilder();
            bool inDouble = false, inSingle = false, escaped = false, hasToken = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (escaped) { sb.Append(ch); escaped = false; hasToken = true; continue; }
                if (ch == '\\' && (inDouble || inSingle)) { escaped = true; continue; }
                if (ch == '"' && !inSingle) { inDouble = !inDouble; hasToken = true; continue; }
                if (ch == '\'' && !inDouble) { inSingle = !inSingle; hasToken = true; continue; }
                if (!inDouble && !inSingle && (ch == ' ' || ch == '\t'))
                {
                    if (hasToken) { out_.Add(sb.ToString()); sb.Length = 0; hasToken = false; }
                    continue;
                }
                sb.Append(ch);
                hasToken = true;
            }
            if (inDouble || inSingle) throw new System.ArgumentException("Unclosed quote.");
            if (escaped) throw new System.ArgumentException("Trailing escape.");
            if (hasToken) out_.Add(sb.ToString());
            return out_;
        }
    }
}
