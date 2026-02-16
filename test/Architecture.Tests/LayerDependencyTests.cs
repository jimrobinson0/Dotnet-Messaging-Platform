using Messaging.Application.Messages;
using Messaging.Persistence.Messages;

namespace Messaging.Architecture.Tests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Application_should_not_reference_Persistence_internal_namespaces()
    {
        var appAssembly = typeof(IMessageApplicationService).Assembly;

        var referenced = appAssembly.GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToList();

        Assert.DoesNotContain("Messaging.Persistence.Tests", referenced);
    }

    [Fact]
    public void Persistence_should_not_reference_Application()
    {
        var persistenceAssembly = typeof(MessageRepository).Assembly;

        var referenced = persistenceAssembly.GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToList();

        Assert.DoesNotContain("Messaging.Application", referenced);
    }
}
