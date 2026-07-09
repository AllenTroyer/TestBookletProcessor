using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TestBookletProcessor.Core.Utilities;

/// <summary>
/// Case-insensitive wildcard matching where * matches any sequence of characters.
/// Compiled patterns are cached because matching runs once per page per pattern.
/// </summary>
public static class WildcardMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new(StringComparer.OrdinalIgnoreCase);

    public static bool Matches(string? value, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        var regex = PatternCache.GetOrAdd(pattern, p => new Regex(
            "^" + Regex.Escape(p).Replace("\\*", ".*") + "$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled));

        return regex.IsMatch(value ?? string.Empty);
    }
}
