using System.Reflection;

namespace SkAgentWorkFlowStarter.Console.Framework.Prompting;

public class EmbeddedResourceManager
{
    public static string Read(string resourceName, Assembly? currentAssembly = null)
    {
        var assembly = currentAssembly ?? Assembly.GetExecutingAssembly();
        var fullResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(resourceName));

        if (fullResourceName == null)
            throw new FileNotFoundException($"Resource '{resourceName}' not found.");

        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
