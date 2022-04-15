using System;

namespace CacheInstrumentation
{
    internal class SR
    {
        public static readonly string Invalid_expiration_combination = "absoluteExpiration must be DateTime.MaxValue or slidingExpiration must be timeSpan.Zero.";
        public static readonly string Cache_dependency_used_more_that_once = "An attempt was made to reference a CacheDependency object from more than one Cache entry.";
        public static readonly string Unhandled_Monitor_Exception = "An unhandled exception occurred while executing '{0}' in '{1}'.";

        public static string GetString(string input, params object[] values)
        {
            return String.Format(input, values);
        }
    }
}
