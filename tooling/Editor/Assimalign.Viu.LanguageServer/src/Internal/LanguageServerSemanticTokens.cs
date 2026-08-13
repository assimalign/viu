using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Translates host-neutral C# classifications to the
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_semanticTokens">
/// Language Server Protocol 3.17 semantic-token vocabulary and relative integer encoding</see>.
/// [V01.01.12.07.11]
/// </summary>
internal static class LanguageServerSemanticTokens
{
    private static readonly IReadOnlyList<string> TokenTypes =
    [
        "namespace",
        "type",
        "class",
        "enum",
        "interface",
        "struct",
        "typeParameter",
        "parameter",
        "variable",
        "property",
        "enumMember",
        "event",
        "function",
        "method",
        "macro",
        "label",
        "comment",
        "string",
        "keyword",
        "number",
        "regexp",
        "operator",
        "decorator",
    ];

    /// <summary>Creates the server capability advertised for full-document semantic tokens.</summary>
    internal static JsonObject CreateProviderCapability()
    {
        var tokenTypes = new JsonArray();
        foreach (var tokenType in TokenTypes)
        {
            tokenTypes.Add((JsonNode?)JsonValue.Create(tokenType));
        }

        return new JsonObject
        {
            ["legend"] = new JsonObject
            {
                ["tokenTypes"] = tokenTypes,
                ["tokenModifiers"] = new JsonArray(),
            },
            ["full"] = true,
        };
    }

    /// <summary>Creates one full-document semantic-token response.</summary>
    internal static JsonObject CreateResult(
        IReadOnlyList<LanguageClassification> classifications)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        var tokens = new List<(LanguageRange Range, int TokenType)>();
        foreach (var classification in classifications)
        {
            if (TryGetTokenType(classification.ClassificationTypeName, out var tokenType) &&
                classification.Range.Start.Line == classification.Range.End.Line &&
                classification.Range.End.Character > classification.Range.Start.Character)
            {
                tokens.Add((classification.Range, tokenType));
            }
        }

        tokens.Sort(
            static (left, right) =>
            {
                var lineComparison = left.Range.Start.Line.CompareTo(right.Range.Start.Line);
                return lineComparison != 0
                    ? lineComparison
                    : left.Range.Start.Character.CompareTo(right.Range.Start.Character);
            });

        var data = new JsonArray();
        var previousLine = 0;
        var previousCharacter = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var (range, tokenType) = tokens[index];
            var line = range.Start.Line;
            var character = range.Start.Character;
            var deltaLine = index == 0 ? line : line - previousLine;
            var deltaCharacter = index == 0 || deltaLine != 0
                ? character
                : character - previousCharacter;

            data.Add((JsonNode?)JsonValue.Create(deltaLine));
            data.Add((JsonNode?)JsonValue.Create(deltaCharacter));
            data.Add((JsonNode?)JsonValue.Create(range.End.Character - character));
            data.Add((JsonNode?)JsonValue.Create(tokenType));
            data.Add((JsonNode?)JsonValue.Create(0));

            previousLine = line;
            previousCharacter = character;
        }

        return new JsonObject
        {
            ["data"] = data,
        };
    }

    private static bool TryGetTokenType(string classificationTypeName, out int tokenType)
    {
        tokenType = classificationTypeName switch
        {
            LanguageClassificationTypeNames.NamespaceName => 0,
            LanguageClassificationTypeNames.DelegateName => 1,
            LanguageClassificationTypeNames.ClassName => 2,
            LanguageClassificationTypeNames.EnumName => 3,
            LanguageClassificationTypeNames.InterfaceName => 4,
            LanguageClassificationTypeNames.StructName => 5,
            LanguageClassificationTypeNames.TypeParameterName => 6,
            LanguageClassificationTypeNames.ParameterName => 7,
            LanguageClassificationTypeNames.LocalName or
                LanguageClassificationTypeNames.FieldName or
                LanguageClassificationTypeNames.ConstantName => 8,
            LanguageClassificationTypeNames.PropertyName => 9,
            LanguageClassificationTypeNames.EnumMemberName => 10,
            LanguageClassificationTypeNames.EventName => 11,
            LanguageClassificationTypeNames.MethodName => 13,
            LanguageClassificationTypeNames.LabelName => 15,
            _ => -1,
        };

        return tokenType >= 0;
    }
}
