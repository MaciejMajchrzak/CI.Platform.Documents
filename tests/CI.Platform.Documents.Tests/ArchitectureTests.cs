using System.Reflection;
using CI.Kernel.ArchTests;
using CI.Platform.Documents.API.Controllers;
using CI.Platform.Documents.Core.Handlers;
using CI.Platform.Documents.Domain.Entities;
using CI.Platform.Documents.Infrastructure;
namespace CI.Platform.Documents.Tests;

public sealed class DocumentsArchitectureTests : ModuleArchitectureTests
{
    protected override Assembly DomainAssembly => typeof(DocumentTemplate).Assembly;
    protected override Assembly CoreAssembly   => typeof(CreateTemplateHandler).Assembly;
    protected override Assembly InfraAssembly  => typeof(DocumentsDbContext).Assembly;
    protected override Assembly ApiAssembly    => typeof(TemplatesController).Assembly;

    // DocumentsOutboxMessage extends OutboxMessage (not BaseEntity)
    protected override bool ExcludeFromBaseEntityCheck(Type t) =>
        t == typeof(DocumentsOutboxMessage);
}
