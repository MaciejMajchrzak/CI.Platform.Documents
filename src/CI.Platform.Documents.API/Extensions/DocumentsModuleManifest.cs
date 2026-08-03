using System.Reflection;
using CI.Kernel;
using CI.Platform.Documents.Domain.Events;
namespace CI.Platform.Documents.API.Extensions;

public sealed class DocumentsModuleManifest : IModuleManifest
{
    public ModuleDescriptor Describe()
    {
        var domainAssembly = typeof(DocumentTemplateCreatedEvent).Assembly;
        var coreAssembly   = typeof(Core.Commands.CreateTemplateCommand).Assembly;

        var events   = ScanEvents(domainAssembly);
        var commands = ScanCommands(coreAssembly);

        return new ModuleDescriptor("documents", "Documents", "1.0.0", events, commands, Array.Empty<QueryDescriptor>());
    }

    private static IReadOnlyList<EventDescriptor> ScanEvents(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEvent)) && t is { IsInterface: false, IsAbstract: false })
            .Select(t => new EventDescriptor(t.Name, t.FullName ?? t.Name, GetProperties(t)))
            .ToArray();

    private static IReadOnlyList<CommandDescriptor> ScanCommands(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(ICommand)) && t is { IsInterface: false, IsAbstract: false })
            .Select(t => new CommandDescriptor(t.Name, t.FullName ?? t.Name, GetProperties(t)))
            .ToArray();

    private static IReadOnlyList<PropertyDescriptor> GetProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Select(p => new PropertyDescriptor(p.Name, p.PropertyType.Name,
             !p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(Nullable<>)))
         .ToArray();
}
