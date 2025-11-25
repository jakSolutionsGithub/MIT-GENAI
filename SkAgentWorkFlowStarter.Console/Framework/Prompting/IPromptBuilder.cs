using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkAgentWorkFlowStarter.Console.Framework.Prompting;

public interface IPromptBuilder
{
    ValueTask<string> BuildAsync(string templateName, KernelArguments kernelArguments,
        string templateFormat = "handlebars",
        Assembly? assembly = null,
        CancellationToken ct = default);
}


public class PromptBuilder(Kernel kernel) : IPromptBuilder
{
    public async ValueTask<string> BuildAsync(string templateName, KernelArguments kernelArguments,
        string templateFormat = "handlebars",
        Assembly? assembly = null,
        CancellationToken ct = default)
    {
        var yaml = EmbeddedResourceManager.Read($"{templateName}.yaml", assembly);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var prompt = deserializer.Deserialize<Prompt>(yaml);

        var templateFactory = new HandlebarsPromptTemplateFactory();
        var promptTemplateConfig = new PromptTemplateConfig
        {
            Template = prompt.Template,
            TemplateFormat = templateFormat,
            Name = templateName
        };

        return
            await templateFactory.Create(promptTemplateConfig).RenderAsync(kernel, kernelArguments, ct);
    }
}
