using System.Globalization;
using Phantasma.Core.Numerics;

namespace Backend.Commons;

public static class Utils
{
    public static string ToDecimal(string amount, int tokenDecimals)
    {
        return UnitConversion.ToDecimal(amount, tokenDecimals).ToString(CultureInfo.InvariantCulture);
    }
}
