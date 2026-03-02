using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Backend.Commons;

public static class Utils
{
    public static bool HasPositiveMaxSupply(string rawSupply)
    {
        if (string.IsNullOrWhiteSpace(rawSupply))
            return false;

        return BigInteger.TryParse(rawSupply, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed > 0;
    }

    // TDOD fix implementation of UnitConversion.ToDecimal() in Phantasma's code
    public static string ToDecimal(string amount, int tokenDecimals)
    {
        if (amount == "0" || tokenDecimals == 0)
            return amount;

        if (amount.Length <= tokenDecimals)
        {
            return "0." + amount.PadLeft(tokenDecimals, '0').TrimEnd('0');
        }

        var decimalPart = amount.Substring(amount.Length - tokenDecimals);
        decimalPart = decimalPart.Any(x => x != '0') ? decimalPart.TrimEnd('0') : null;
        return amount.Substring(0, amount.Length - tokenDecimals) + (decimalPart != null ? "." + decimalPart : "");
    }
}
