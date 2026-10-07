using System.Globalization;
using System.Text;

namespace ClassCommander.Testing.Core.Import;

internal static class MyTestRtfText
{
    public static string Read(string rtf)
    {
        var result = new StringBuilder();
        var states = new Stack<(bool Skip, int UnicodeFallback, Encoding Encoding)>();
        var state = (Skip: false, UnicodeFallback: 1, Encoding: Encoding.GetEncoding(1251));
        var fallback = 0;
        for (var i = 0; i < rtf.Length; i++)
        {
            var ch = rtf[i];
            if (ch == '{')
            { states.Push(state); continue; }
            if (ch == '}')
            { if (states.Count > 0) state = states.Pop(); continue; }
            if (ch is '\r' or '\n')
                continue;
            if (ch != '\\')
            {
                if (fallback > 0)
                    fallback--;
                else if (!state.Skip)
                    result.Append(ch);
                continue;
            }

            if (++i >= rtf.Length)
                break;
            ch = rtf[i];
            if (ch == '*')
            { state.Skip = true; continue; }
            if (ch == '\'' && i + 2 < rtf.Length)
            {
                if (byte.TryParse(rtf.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                {
                    if (fallback > 0)
                        fallback--;
                    else if (!state.Skip)
                        result.Append(state.Encoding.GetString([value]));
                    i += 2;
                }
                continue;
            }
            if (!char.IsAsciiLetter(ch))
            {
                if (fallback > 0)
                    fallback--;
                else if (!state.Skip)
                    result.Append(ch switch { '~' => ' ', '_' => '-', _ => ch });
                continue;
            }

            var start = i;
            while (i < rtf.Length && char.IsAsciiLetter(rtf[i]))
                i++;
            var word = rtf[start..i];
            var numberStart = i;
            if (i < rtf.Length && rtf[i] == '-')
                i++;
            while (i < rtf.Length && char.IsAsciiDigit(rtf[i]))
                i++;
            var hasNumber = int.TryParse(rtf.AsSpan(numberStart, i - numberStart), out var number);
            if (i < rtf.Length && rtf[i] == ' ')
                i++;
            i--;
            if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object" or "fldinst")
                state.Skip = true;
            if (word == "bin" && hasNumber)
            { i += Math.Min(Math.Max(number, 0), rtf.Length - i - 1); continue; }
            if (state.Skip)
                continue;
            switch (word)
            {
                case "ansicpg" when hasNumber:
                    state.Encoding = Encoding.GetEncoding(number);
                    break;
                case "uc" when hasNumber:
                    state.UnicodeFallback = Math.Clamp(number, 0, 16);
                    break;
                case "u" when hasNumber:
                    result.Append(unchecked((char)number));
                    fallback = state.UnicodeFallback;
                    break;
                case "par":
                case "line":
                    result.Append('\n');
                    break;
                case "tab":
                    result.Append('\t');
                    break;
                case "emdash":
                    result.Append('—');
                    break;
                case "endash":
                    result.Append('–');
                    break;
                case "bullet":
                    result.Append('•');
                    break;
                case "lquote":
                    result.Append('‘');
                    break;
                case "rquote":
                    result.Append('’');
                    break;
                case "ldblquote":
                    result.Append('“');
                    break;
                case "rdblquote":
                    result.Append('”');
                    break;
            }
        }
        return result.ToString().Trim();
    }
}
