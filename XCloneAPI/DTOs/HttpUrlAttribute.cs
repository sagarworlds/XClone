using System.ComponentModel.DataAnnotations;

namespace XCloneAPI.DTOs
{
    /// <summary>
    /// Accepts absolute http(s) URLs only. On a single string an empty value is allowed (used to clear a field);
    /// on a list of strings every entry must be a URL.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class HttpUrlAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var valid = value switch
            {
                null => true,
                string s => s.Length == 0 || IsHttpUrl(s),
                IEnumerable<string> items => items.All(IsHttpUrl),
                _ => false
            };

            return valid
                ? ValidationResult.Success
                : new ValidationResult(
                    ErrorMessage ?? $"{validationContext.DisplayName} must be an absolute http(s) URL.",
                    validationContext.MemberName is null ? null : new[] { validationContext.MemberName });
        }

        private static bool IsHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
