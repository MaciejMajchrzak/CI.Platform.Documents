using CI.Platform.Documents.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CI.Platform.Documents.Infrastructure;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<DocumentTemplate>       Templates      => Set<DocumentTemplate>();
    public DbSet<DocumentsOutboxMessage> OutboxMessages => Set<DocumentsOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);

        modelBuilder.Entity<DocumentsOutboxMessage>(b =>
        {
            b.ToTable("DocumentsOutboxMessages");
            b.HasIndex(m => new { m.Status, m.CreatedAt });
        });

        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<DocumentTemplate>()
                .Property(p => p.RowVersion)
                .HasColumnType("xid").HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
    }
}
