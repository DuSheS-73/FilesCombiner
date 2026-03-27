namespace FilesCombiner.Services;

public class BlacklistMatcher
{
    private readonly List<BlacklistEntry> _entries = new();

    public BlacklistMatcher(string pattern)
    {
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            var lines = pattern.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#"))
                {
                    _entries.Add(new BlacklistEntry(trimmedLine));
                }
            }
        }
    }

    public bool IsBlacklisted(string path, bool isDirectory)
    {
        // Normalize path separators to forward slashes for consistent matching
        var normalizedPath = path.Replace('\\', '/');

        foreach (var entry in _entries)
        {
            if (entry.IsMatch(normalizedPath, isDirectory))
            {
                return true;
            }
        }

        return false;
    }

    private class BlacklistEntry
    {
        private readonly string _pattern;
        private readonly bool _isDirectoryOnly;
        private readonly bool _isNegated;
        private readonly bool _isRooted;

        public BlacklistEntry(string pattern)
        {
            _pattern = pattern;

            // Check if it's a negated pattern
            if (_pattern.StartsWith("!"))
            {
                _isNegated = true;
                _pattern = _pattern.Substring(1);
            }

            // Check if it's directory only (ends with /)
            if (_pattern.EndsWith("/"))
            {
                _isDirectoryOnly = true;
                _pattern = _pattern.TrimEnd('/');
            }

            // Check if it's rooted (starts with /)
            _isRooted = _pattern.StartsWith("/");
            if (_isRooted)
            {
                _pattern = _pattern.Substring(1);
            }
        }

        public bool IsMatch(string path, bool isDirectory)
        {
            var pathToMatch = path;

            // If this entry is directory-only and we're matching a file, skip
            if (_isDirectoryOnly && !isDirectory)
                return false;

            // If it's a rooted pattern, match from the beginning
            if (_isRooted)
            {
                return IsMatchFromRoot(pathToMatch);
            }

            // Otherwise, match any segment
            return IsMatchAnySegment(pathToMatch);
        }

        private bool IsMatchFromRoot(string path)
        {
            // Check if the pattern matches the beginning of the path
            if (IsWildcardMatch(path, _pattern))
                return true;

            // Check for pattern with wildcards at any position
            return IsWildcardMatch(path, _pattern);
        }

        private bool IsMatchAnySegment(string path)
        {
            var segments = path.Split('/');

            // Check each segment individually
            foreach (var segment in segments)
            {
                if (IsWildcardMatch(segment, _pattern))
                    return true;
            }

            // Check if the entire path matches
            if (IsWildcardMatch(path, _pattern))
                return true;

            // Check for patterns like **/pattern/**
            if (_pattern.Contains("**"))
            {
                return IsRecursiveMatch(path);
            }

            return false;
        }

        private bool IsRecursiveMatch(string path)
        {
            var patternParts = _pattern.Split(new[] { "**" }, StringSplitOptions.None);

            if (patternParts.Length == 2)
            {
                var startPattern = patternParts[0].TrimEnd('/');
                var endPattern = patternParts[1].TrimStart('/');

                // Pattern like "**/pattern" - matches any path ending with pattern
                if (string.IsNullOrEmpty(startPattern))
                {
                    return path.EndsWith(endPattern) ||
                           (path.Contains('/') && path.Substring(path.LastIndexOf('/') + 1).StartsWith(endPattern));
                }

                // Pattern like "pattern/**" - matches any path starting with pattern
                if (string.IsNullOrEmpty(endPattern))
                {
                    return path.StartsWith(startPattern);
                }

                // Pattern like "pattern/**/pattern" - matches paths containing the pattern
                return path.Contains(startPattern) && path.Contains(endPattern);
            }

            return false;
        }

        private bool IsWildcardMatch(string input, string pattern)
        {
            // Convert gitignore-style pattern to regex
            var regexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")  // ** matches anything
                .Replace("\\*", "[^/]*")   // * matches anything except /
                .Replace("\\?", ".")        // ? matches single character
                .Replace("\\[", "[")        // Character classes
                .Replace("\\]", "]");

            // Add start and end anchors
            regexPattern = "^" + regexPattern + "$";

            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(input, regexPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public bool IsNegated => _isNegated;
    }
}