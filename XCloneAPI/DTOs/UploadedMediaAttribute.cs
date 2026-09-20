using System.ComponentModel.DataAnnotations;
using XCloneAPI.Services;

namespace XCloneAPI.DTOs
{
    /// <summary>
    /// A list of images attached to a post: at most <see cref="MaxItems"/>, each one a URL this API handed out for an
    /// upload ("/uploads/&lt;name&gt;"), none of them twice. Addresses on other sites are not accepted, so a post can
    /// never make its readers' browsers contact somebody else's server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class UploadedMediaAttribute : ValidationAttribute
    {
        public const int MaxItems = 4;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var valid = value switch
            {
                null => true,
                IEnumerable<string> items => IsValidList(items.ToList()),
                _ => false
            };

            return valid
                ? ValidationResult.Success
                : new ValidationResult(
                    ErrorMessage ?? $"Attach at most {MaxItems} images, each uploaded through /api/media (and none twice).",
                    validationContext.MemberName is null ? null : new[] { validationContext.MemberName });
        }

        private static bool IsValidList(List<string> items) =>
            items.Count <= MaxItems
            && items.All(url => MediaNames.TryGetName(url, out _))
            && items.Distinct(StringComparer.Ordinal).Count() == items.Count;
    }
}
