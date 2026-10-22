using System.Globalization;
using System.Numerics;

namespace Json;

public sealed record JsonNumber(string Text)
{
    internal static (string Digits, BigInteger Exponent) Normalize(object value)
    {
        var text = value switch
        {
            JsonNumber number => number.Text,
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()!
        };
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        var exponent = exponentIndex < 0 ? BigInteger.Zero : BigInteger.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
        var decimalIndex = mantissa.IndexOf('.');

        if (decimalIndex >= 0)
        {
            exponent -= mantissa.Length - decimalIndex - 1;
        }

        var negative = mantissa.StartsWith('-');
        var digits = mantissa.Replace(".", "").TrimStart('-').TrimStart('0');

        if (digits.Length == 0)
        {
            return ("0", BigInteger.Zero);
        }

        var significantDigits = digits.TrimEnd('0');
        exponent += digits.Length - significantDigits.Length;

        return (negative ? "-" + significantDigits : significantDigits, exponent);
    }

    public override string ToString() => Text;
}
