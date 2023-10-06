using System.Linq;

namespace Backend.Commons;

public static class Utils
{
    // TDOD fix implementation of UnitConversion.ToDecimal() in Phantasma's code
    public static string ToDecimal(string amount, int tokenDecimals)
    {
        if ( amount == "0" || tokenDecimals == 0 )
            return amount;

        if ( amount.Length <= tokenDecimals )
        {
            return "0." + amount.PadLeft(tokenDecimals - amount.Length, '0');
        }

        var decimalPart = amount.Substring(amount.Length - tokenDecimals);
        decimalPart = decimalPart.Any(x => x != '0') ? decimalPart.TrimEnd('0') : null;
        return amount.Substring(0, amount.Length - tokenDecimals) + (decimalPart != null ? "." + decimalPart : "");
    }
}
