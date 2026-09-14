using System.Globalization;
using System.Text;

namespace Portfolio.Core.Abstractions;

/// <summary>
/// Gera a forma canonica de um texto livre: sem acentos, minusculo e com
/// separadores unicos. Usado para comparar nomes digitados pelo usuario sem
/// depender do collation do banco ("Ações", "ACOES" e "acoes" viram "acoes").
/// </summary>
public static class Slugger
{
    public static string From(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSeparator = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSeparator && builder.Length > 0) builder.Append('-');
                pendingSeparator = false;
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
