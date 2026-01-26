using System.Runtime.CompilerServices;

namespace WebBudget;

public static class Extensions
{
    public static DateOnly ToDateOnly(this DateTime datetime)
    {
        return DateOnly.FromDateTime(datetime);
    }
}
