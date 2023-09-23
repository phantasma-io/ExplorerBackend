using System.Numerics;

namespace Backend.Commons;

public static class Utils
{
    // TDOD fix implementation of UnitConversion.ToDecimal() in Phantasma's code
    public static string ToDecimal(string amount, int tokenDecimals)
    {
        if ( amount == "0" || tokenDecimals == 0 )
            return amount;

        var quotient = BigInteger.DivRem(BigInteger.Parse(amount), BigInteger.Pow(10, tokenDecimals), out var remainder);
        
        return quotient + "." + remainder;
    }
}
