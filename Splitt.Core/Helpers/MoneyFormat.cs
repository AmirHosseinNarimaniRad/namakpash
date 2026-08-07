using System.Globalization;

namespace Splitt.Core.Helpers;

public static class MoneyFormat
{
    /// <summary>1234567 → "1,234,567" (English digits, thousand separators).</summary>
    public static string Format(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>1234567 → "1,234,567 تومان"</summary>
    public static string FormatToman(decimal amount) =>
        Format(amount) + " تومان";

    /// <summary>Parses user input, tolerating separators and Persian/Arabic digits. Null if invalid.</summary>
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var digits = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (ch is >= '0' and <= '9')
                digits.Append(ch);
            else if (ch is >= '۰' and <= '۹')      // Persian digits
                digits.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩')      // Arabic-Indic digits
                digits.Append((char)('0' + (ch - '٠')));
            else if (ch is ',' or '٬' or ' ' or '‏' or '‎')
                continue;
            else
                return null;
        }

        if (digits.Length == 0)
            return null;

        return decimal.TryParse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
