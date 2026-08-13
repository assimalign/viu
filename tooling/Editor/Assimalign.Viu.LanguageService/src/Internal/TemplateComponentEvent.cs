namespace Assimalign.Viu.LanguageService;

/// <summary>One template-facing event on a semantically resolved component contract.</summary>
internal sealed record TemplateComponentEvent(
    string Name,
    string Detail);
