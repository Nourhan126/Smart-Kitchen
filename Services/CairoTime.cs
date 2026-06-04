using System.Globalization;

namespace SmartKitchen.API.Services;

public static class CairoTime
{
    private const string ActivityTimeFormat = "hh:mm tt";

    public static DateTime UtcNowForStorage()
    {
        // ApplicationDbContext currently adds Cairo offset on DateTime save.
        // Supplying UTC here preserves Cairo-local values in existing columns.
        return DateTime.UtcNow;
    }

    public static DateTime NormalizeIncomingTimestamp(DateTime timestamp)
    {
        if (timestamp == default)
        {
            return UtcNowForStorage();
        }

        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => timestamp.AddHours(-3)
        };
    }

    public static string FormatActivityTime(DateTime value)
    {
        var cairo = value.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(value, GetZone())
            : value;

        return cairo.ToString(
            ActivityTimeFormat,
            CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo GetZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }
}
