using System;
using System.Globalization;
using System.Linq;

namespace Impostor.Api.Innersloth
{
    public class GameVersion
    {
        public static int GetVersion(int year, int month, int day, int rev = 0)
        {
            return (year * 25000) + (month * 1800) + (day * 50) + rev;
        }

        public static ValueTuple<int, int, int, int> GetVersionComponents(int broadcastVersion)
        {
            int num = broadcastVersion / 25000;
            broadcastVersion -= num * 25000;
            int num2 = broadcastVersion / 1800;
            broadcastVersion -= num2 * 1800;
            int num3 = broadcastVersion / 50;
            broadcastVersion -= num3 * 50;
            int num4 = broadcastVersion;
            return new ValueTuple<int, int, int, int>(num, num2, num3, num4);
        }

        public static string Version2String(int version)
        {
            (int num, int num2, int num3, int num4) = GetVersionComponents(version);
            string versionString = $"{num}.{num2}.{num3}";
            if (num4 != 0)
            {
                versionString += $".{num4}";
            }
            return versionString;
        }

        public static DateTime ParseVersion(string dateString)
        {
            var parts = dateString.Split('.');

            if (parts.Length < 3)
            {
                throw new FormatException("Invalid date format. Expected formats: yyyy.M.d or yyyy.MM.dd");
            }

            var validDateString = string.Join(".", parts.Take(3));
            string[] formats = { "yyyy.M.d", "yyyy.MM.dd" };

            if (DateTime.TryParseExact(validDateString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                return parsedDate;
            }
            else
            {
                throw new FormatException("Invalid date format. Expected formats: yyyy.M.d or yyyy.MM.dd");
            }
        }
    }
}
