using System;
using System.Globalization;

namespace GuidingAnalyzer.Services
{
    public static class PolarInput
    {
        public static bool TryParse(string latitudeText, string sensitivityText,
            out double latitude, out double sensitivity, out string error)
        {
            sensitivity = 0;
            error = string.Empty;
            const NumberStyles style = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
                | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (!double.TryParse(latitudeText, style, CultureInfo.InvariantCulture, out latitude)
                || !double.IsFinite(latitude) || latitude < -90 || latitude > 90)
            {
                error = "Site latitude must be between -90 and 90. Use a dot for decimals (e.g. 48.8).";
                return false;
            }
            if (!double.TryParse(sensitivityText, style, CultureInfo.InvariantCulture, out sensitivity)
                || !double.IsFinite(sensitivity) || sensitivity <= 0)
            {
                error = "Screw sensitivity must be greater than zero. Use a dot for decimals (e.g. 30.5).";
                return false;
            }
            return true;
        }
    }
}
