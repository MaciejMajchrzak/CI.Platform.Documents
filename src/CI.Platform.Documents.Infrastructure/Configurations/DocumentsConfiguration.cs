using CI.Platform.Documents.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CI.Platform.Documents.Infrastructure.Configurations;

public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> b)
    {
        b.ToTable("DocumentTemplates");
        // TenantId (Guid.Empty = platform default) is inherited from BaseEntity — no special mapping needed
        b.HasIndex(x => new { x.Key, x.Type, x.LanguageCode, x.TenantId }).IsUnique();
        b.Property(x => x.Key).HasMaxLength(200);
        b.Property(x => x.Type).HasMaxLength(20);
        b.Property(x => x.LanguageCode).HasMaxLength(10);
    }
}
