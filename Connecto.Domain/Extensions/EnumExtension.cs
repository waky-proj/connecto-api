namespace Connecto.Domain.Extensions
{
    /// <summary>
    /// Provides extension helpers for enum values used across the domain model.
    /// </summary>
    public static class EnumExtension
    {
        public static string ToFriendlyString(this Enum value)
        {
            return value.ToString();
        }

        public static T Parse<T>(this Enum value, string strValue)
        {
            var lowerValue = strValue.ToLowerInvariant();
            var enumValues = Enum.GetValues(typeof(T)).Cast<T>();
            foreach (var enumType in enumValues) 
            {
                if (enumType?.ToString()?.ToLowerInvariant()?.Equals(lowerValue) ?? false)
                    return enumType;
            }
            return enumValues.First();
        }
    }
}
