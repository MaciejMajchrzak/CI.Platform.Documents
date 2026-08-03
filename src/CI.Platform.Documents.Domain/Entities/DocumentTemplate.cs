using CI.Kernel;
namespace CI.Platform.Documents.Domain.Entities;

public sealed class DocumentTemplate : BaseEntity
{
    // TenantId from BaseEntity (Guid) — Guid.Empty means platform default, non-empty is tenant-specific
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = TemplateType.Email;
    public string? SubjectTemplate { get; set; } // email only — Handlebars
    public string BodyTemplate { get; set; } = string.Empty; // Handlebars HTML
    public string LanguageCode { get; set; } = "en";
    public bool IsDefault { get; set; }          // true on platform defaults
}

public static class TemplateType
{
    public const string Email   = "Email";
    public const string Pdf     = "PDF";
    public const string Preview = "Preview";
}
