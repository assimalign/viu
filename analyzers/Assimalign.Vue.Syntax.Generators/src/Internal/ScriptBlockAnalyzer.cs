using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Assimalign.Vue.Syntax.SingleFileComponent;
using Assimalign.Vue.Syntax.Templates;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// Analyzes a parsed <c>@script</c> block's C# — the Vuecs analogue of <c>@vue/compiler-sfc</c>'s
/// <c>compileScript()</c> adapted to a C# partial-class body. It does two jobs, both purely syntactic
/// (the generator has no semantic model for code it is itself generating, and reflection is forbidden):
/// <list type="number">
/// <item>
/// <b>Validation.</b> The raw block content is Roslyn-parsed <em>inside a synthetic partial class</em> —
/// the exact syntactic context <see cref="SingleFileComponentSourceEmitter"/> emits it into — so member
/// declarations are legal and any malformed C# surfaces as a recoverable, position-mapped diagnostic
/// rather than a crash or a broken generated file. Positions are composed back to the <c>.viu</c> file by
/// <see cref="SingleFileComponentDiagnostics.CreateScript"/>, agreeing with the block-to-file mapping used
/// for dispatched-block diagnostics.
/// </item>
/// <item>
/// <b>Binding-metadata extraction.</b> Each top-level member is classified into a <see cref="BindingType"/>
/// (the C# port of Vue's <c>BindingTypes</c>), driving where the template compiler inserts
/// <c>.Value</c>. Classification is conservative: only a field/property whose declared type is a known
/// <c>Assimalign.Vue.Reactivity</c> reference type becomes <see cref="BindingType.SetupReference"/> (the
/// only binding the template ever unwraps), so a misclassification can never ship a wrong <c>.Value</c>.
/// </item>
/// </list>
/// See https://vuejs.org/api/sfc-script-setup.html and <c>docs/FORMAT.md</c> (the <c>@script</c> content
/// contract). Work item [V01.01.06.03].
/// </summary>
internal static class ScriptBlockAnalyzer
{
    // The raw @script content is parsed wrapped in this synthetic partial class so Roslyn parses it in
    // the same syntactic context the emitter places it — a partial-class body. Members (fields,
    // properties, methods) are therefore legal; a stray using directive or malformed member surfaces as
    // the mapped diagnostic the consumer's own compile would also raise. The two-newline prefix puts the
    // first content line at wrapper line index 2 (zero-based), so a wrapper-relative line maps to a
    // content-relative line by subtracting ProbeLineOffset; content columns are unchanged because each
    // content line keeps its own leading whitespace (nothing is prepended per line).
    private const string ProbePrefix = "partial class __VuecsScriptProbe\n{\n";
    private const string ProbeSuffix = "\n}\n";
    private const int ProbeLineOffset = 2;

    // The stable analyzer parse options: the repo compiles with LangVersion=preview, so the script is
    // validated against the same language surface it will ultimately be compiled under.
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.None, SourceCodeKind.Regular);

    // The Assimalign.Vue.Reactivity reference-carrying types — the C# ports of Vue's ref/computed/
    // shallowRef/customRef. A field or property of one of these holds a reactive reference the template
    // compiler must unwrap through .Value (BindingType.SetupReference, upstream SETUP_REF). Matched by
    // simple type name because classification is syntactic; the generator references no runtime library.
    private static readonly HashSet<string> ReferenceTypeNames = new(StringComparer.Ordinal)
    {
        "Reference",
        "ShallowReference",
        "CustomReference",
        "Computed",
        "IReference",
    };

    /// <summary>
    /// Validates <paramref name="script"/> and extracts its binding metadata, appending any parse
    /// diagnostics (mapped to <paramref name="filePath"/> coordinates) to <paramref name="diagnostics"/>.
    /// </summary>
    /// <param name="filePath">The originating <c>.viu</c> file path (the diagnostic and <c>#line</c> anchor).</param>
    /// <param name="script">The parsed <c>@script</c> block.</param>
    /// <param name="diagnostics">The diagnostic accumulator the mapped script diagnostics are added to.</param>
    /// <returns>The value-equatable classified bindings, empty when the block declares no members.</returns>
    public static EquatableArray<ScriptBinding> Analyze(
        string filePath,
        SingleFileComponentScriptBlock script,
        List<DiagnosticInfo> diagnostics)
    {
        var tree = CSharpSyntaxTree.ParseText(ProbePrefix + script.Content + ProbeSuffix, ParseOptions);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        foreach (var diagnostic in tree.GetDiagnostics())
        {
            diagnostics.Add(SingleFileComponentDiagnostics.CreateScript(
                filePath,
                diagnostic,
                script.ContentLocation.Start,
                ProbePrefix.Length,
                ProbeLineOffset));
        }

        var probe = FindProbe(root);
        if (probe is null)
        {
            return EquatableArray<ScriptBinding>.Empty;
        }

        var bindings = new List<ScriptBinding>();
        foreach (var member in probe.Members)
        {
            Classify(member, bindings);
        }

        return bindings.Count == 0
            ? EquatableArray<ScriptBinding>.Empty
            : new EquatableArray<ScriptBinding>(bindings.ToArray());
    }

    // Locates the synthetic wrapper class. It is normally the sole top-level member; a search by name is
    // used so malformed content that injects stray top-level declarations still finds the real members.
    private static ClassDeclarationSyntax? FindProbe(CompilationUnitSyntax root)
    {
        foreach (var member in root.Members)
        {
            if (member is ClassDeclarationSyntax type && string.Equals(type.Identifier.Text, "__VuecsScriptProbe", StringComparison.Ordinal))
            {
                return type;
            }
        }

        return null;
    }

    // Classifies one top-level member. Fields, properties, and methods are template-facing bindings;
    // constructors, events, nested types, operators, and the like are not, so they are skipped.
    private static void Classify(MemberDeclarationSyntax member, List<ScriptBinding> bindings)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
            {
                var type = ClassifyField(field);
                foreach (var variable in field.Declaration.Variables)
                {
                    bindings.Add(new ScriptBinding(variable.Identifier.Text, type));
                }

                break;
            }

            case PropertyDeclarationSyntax property:
                bindings.Add(new ScriptBinding(property.Identifier.Text, ClassifyProperty(property)));
                break;

            case MethodDeclarationSyntax method:
                // A method is a fixed callable, never a reactive reference and never unwrapped
                // (upstream SETUP_CONST) — the render path emits a direct call, not a .Value access.
                bindings.Add(new ScriptBinding(method.Identifier.Text, BindingType.SetupConstant));
                break;
        }
    }

    // Field classification, checked in precedence order so a wrong .Value decision can never ship:
    //   reference type          -> SetupReference   (SETUP_REF; the only unwrapped binding)
    //   const                   -> LiteralConstant  (LITERAL_CONST; a C# compile-time literal, folded)
    //   readonly (non-reference) -> SetupConstant    (SETUP_CONST; fixed after construction, never unwrapped)
    //   otherwise (mutable)     -> SetupLet         (SETUP_LET; a reassignable binding, never unwrapped)
    // The finer SETUP_MAYBE_REF / SETUP_REACTIVE_CONST buckets need reassignment/factory-call flow analysis
    // over a semantic model the generator cannot obtain here, so they are deliberately not produced.
    private static BindingType ClassifyField(FieldDeclarationSyntax field)
    {
        if (IsReferenceType(field.Declaration.Type))
        {
            return BindingType.SetupReference;
        }

        if (HasModifier(field.Modifiers, SyntaxKind.ConstKeyword))
        {
            return BindingType.LiteralConstant;
        }

        return HasModifier(field.Modifiers, SyntaxKind.ReadOnlyKeyword)
            ? BindingType.SetupConstant
            : BindingType.SetupLet;
    }

    // Property classification: a reference-typed property is unwrapped (SETUP_REF); otherwise a property
    // with a set accessor is a reassignable binding (SETUP_LET) and a get-only/init-only property is fixed
    // (SETUP_CONST). Neither non-reference case is ever unwrapped.
    private static BindingType ClassifyProperty(PropertyDeclarationSyntax property)
    {
        if (IsReferenceType(property.Type))
        {
            return BindingType.SetupReference;
        }

        return IsWritable(property) ? BindingType.SetupLet : BindingType.SetupConstant;
    }

    private static bool IsWritable(PropertyDeclarationSyntax property)
    {
        // An expression-bodied property (`=> value`) is get-only. Otherwise a `set` accessor makes it
        // reassignable; an `init` accessor does not (it is fixed after object initialization).
        if (property.AccessorList is null)
        {
            return false;
        }

        foreach (var accessor in property.AccessorList.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReferenceType(TypeSyntax type)
    {
        var name = GetSimpleTypeName(type);
        return name is not null && ReferenceTypeNames.Contains(name);
    }

    // The right-most simple identifier of a type reference, unwrapping nullable annotations and namespace
    // qualification so `Reference<int>`, `Reactivity.Reference<int>`, and `Reference<int>?` all classify.
    private static string? GetSimpleTypeName(TypeSyntax type)
        => type switch
        {
            NullableTypeSyntax nullable => GetSimpleTypeName(nullable.ElementType),
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => GetSimpleTypeName(qualified.Right),
            AliasQualifiedNameSyntax alias => GetSimpleTypeName(alias.Name),
            _ => null,
        };

    private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }
}
