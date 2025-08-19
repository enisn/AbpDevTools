
namespace AbpDevTools.Extensions;

public static class StringExtensions
{
    public static string EnsureEndsWith(this string value, string suffix)
    {
        if (value.EndsWith(suffix))
        {
            return value;
        }
        return value + suffix;
    }
}