using System.Collections.Generic;
using System.Globalization;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharpExtensions;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Parses a template expression with Roslyn, validates it, classifies each referenced identifier against the
/// template scope and <see cref="BindingMetadata"/>, and rewrites the expression for code generation. The C#
/// analogue of Vue 3.5's <c>processExpression</c> and <c>rewriteIdentifier</c> (<c>@vue/compiler-core</c>
/// <c>transforms/transformExpression.ts</c>), with Roslyn (<c>SyntaxFactory.ParseExpression</c>) standing in
/// for the <c>@babel/parser</c> walk.
/// </summary>
/// <remarks>
/// <para>
/// Vuecs diverges from Vue where C# semantics require it, and those divergences are the compiler's contract
/// with code generation ([V01.01.05.05]) and the reactivity area, documented in the feature design notes:
/// </para>
/// <list type="bullet">
/// <item>
/// Vue relies on a runtime <c>Proxy</c> for automatic ref unwrapping; C# has none, so a
/// <see cref="BindingType.SetupReference"/> is rewritten to a <c>.Value</c> access in <b>both</b> read and
/// write positions (a settable <c>Ref&lt;T&gt;.Value</c> makes <c>count += 1</c>/<c>count++</c> work directly),
/// rather than Vue's read-time <c>unref</c> plus an <c>isRef</c> assignment guard.
/// </item>
/// <item>
/// Rewriting is always inline-mode: setup non-reference bindings stay bare (they are locals of the generated
/// render closure) and every unresolved or component-instance member is prefixed with <c>_ctx.</c>, the render
/// context bound by code generation.
/// </item>
/// <item>
/// An identifier absent from the scope, the global allow-list, and the binding metadata is prefixed with
/// <c>_ctx.</c> exactly as Vue does, but additionally surfaces a diagnostic when the metadata is the strict,
/// component-model-complete form (<see cref="BindingMetadata.ReportsUnresolvedIdentifiers"/>).
/// </item>
/// </list>
/// <para>
/// Intra-expression lambda/query scopes are handled by conservatively excluding any name declared anywhere in
/// the expression from rewriting; deeply nested same-name shadowing is therefore approximated (a documented
/// simplification versus Vue's exact per-scope walk). Template-scope shadowing — the case the acceptance
/// criteria pin — is exact, driven by the <see cref="TransformContext"/> identifier stack.
/// </para>
/// </remarks>
internal static class ExpressionProcessor
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    /// <summary>
    /// Processes <paramref name="node"/>: a no-op when prefixing is disabled, otherwise validates it and, unless
    /// <paramref name="asParams"/>, rewrites its identifiers (upstream <c>processExpression</c>).
    /// </summary>
    /// <param name="node">The expression to process.</param>
    /// <param name="context">The active transform context.</param>
    /// <param name="asParams">
    /// Whether the content is a declaration list (a <c>v-slot</c> prop or <c>v-for</c> alias) rather than a
    /// referenced expression; declarations are validated but not rewritten, and are registered in scope by the
    /// owning transform.
    /// </param>
    /// <param name="asRawStatements">
    /// Whether the content is one or more statements (a multi-statement inline <c>v-on</c> handler) rather than a
    /// single expression.
    /// </param>
    public static ExpressionNode ProcessExpression(
        SimpleExpressionNode node,
        TransformContext context,
        bool asParams = false,
        bool asRawStatements = false)
    {
        if (!context.PrefixIdentifiers)
        {
            return node;
        }

        var raw = node.Content;
        if (raw.Length == 0 || raw.Trim().Length == 0)
        {
            return node;
        }

        // Fast path: a lone identifier (upstream's isSimpleIdentifier branch).
        if (!asParams && CompilerText.IsSimpleIdentifier(raw))
        {
            if (context.IsLocalIdentifier(raw) || GlobalAllowList.IsAllowed(raw))
            {
                return node;
            }

            var rewritten = RewriteIdentifier(raw, context, isWriteTarget: false, node.Location);
            return rewritten == raw ? node : node with { Content = rewritten };
        }

        // Full parse: validates the whole text (consumeFullText) and yields a tree to walk.
        var prefixLength = asRawStatements ? 1 : 0;
        Microsoft.CodeAnalysis.SyntaxNode parsed = asRawStatements
            ? SyntaxFactory.ParseStatement("{" + raw + "}", 0, ParseOptions)
            : SyntaxFactory.ParseExpression(raw, 0, ParseOptions);

        if (TryGetFirstError(parsed, out var errorSpan, out var errorMessage))
        {
            context.ReportError(CreateInvalidExpressionError(node, raw, errorSpan, errorMessage, prefixLength));
            return node;
        }

        if (asParams)
        {
            // Declaration positions are validated above; scope registration is the owning transform's job.
            return node;
        }

        var references = CollectReferences(parsed, context, prefixLength);
        if (references.Count == 0)
        {
            return node;
        }

        return BuildCompound(node, raw, references, context);
    }

    // ---- identifier classification and rewriting (upstream rewriteIdentifier) ----

    private static string RewriteIdentifier(string raw, TransformContext context, bool isWriteTarget, SourceLocation location)
    {
        if (context.BindingMetadata.TryGetBindingType(raw, out var type))
        {
            return RewriteBoundIdentifier(raw, type, isWriteTarget, context);
        }

        if (context.BindingMetadata.ReportsUnresolvedIdentifiers)
        {
            var message = CompilerErrorMessages.GetMessage(CompilerErrorCode.XVuecsUnresolvedIdentifier) + "'" + raw + "'.";
            context.ReportError(new CompilerError(CompilerErrorCode.XVuecsUnresolvedIdentifier, message, location));
        }

        return "_ctx." + raw;
    }

    private static string RewriteBoundIdentifier(string raw, BindingType type, bool isWriteTarget, TransformContext context)
        => type switch
        {
            // A definite ref: insert .Value in read and write positions (the settable Ref<T>.Value contract).
            BindingType.SetupReference => raw + ".Value",

            // A maybe-ref: guard reads through unref; a WRITE unwraps to .Value — upstream rewrites
            // SETUP_MAYBE_REF to `.value` in assignment/update position because a write to a const
            // binding is only legal when it is a ref (vuejs/core v3.5 transformExpression.ts
            // rewriteIdentifier).
            BindingType.SetupMaybeReference => isWriteTarget
                ? raw + ".Value"
                : context.HelperString(HelperNames.Unref) + "(" + raw + ")",

            // A let binding may or may not hold a ref: reads guard through unref, matching upstream.
            // Writes stay bare — upstream emits an `isRef(x) ? x.value = y : x = y` runtime guard,
            // which is not expressible as a C# expression without a runtime helper; the divergence is
            // deliberate, recorded in docs/DESIGN.md, and a helper-backed guarded write is deferred to
            // the codegen work ([V01.01.05.05]).
            BindingType.SetupLet => isWriteTarget
                ? raw
                : context.HelperString(HelperNames.Unref) + "(" + raw + ")",

            // Non-reference setup state and literal constants are locals of the render closure: never unwrapped.
            BindingType.SetupConstant or BindingType.SetupReactiveConstant
                or BindingType.LiteralConstant => raw,

            // A destructured prop whose real name differs: resolve through the alias table.
            BindingType.PropertyAliased => "_ctx." + (context.BindingMetadata.GetPropertyAlias(raw) ?? raw),

            // Props, options-API members, and plain data are component-instance members reached through _ctx.
            _ => "_ctx." + raw,
        };

    // ---- reference collection ----

    private static List<ReferencedIdentifier> CollectReferences(
        Microsoft.CodeAnalysis.SyntaxNode root,
        TransformContext context,
        int prefixLength)
    {
        var declared = new HashSet<string>();
        new DeclaredIdentifierWalker(declared).Visit(root);

        var collector = new ReferenceWalker(context, declared, prefixLength);
        collector.Visit(root);
        collector.References.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        return collector.References;
    }

    private static ExpressionNode BuildCompound(
        SimpleExpressionNode node,
        string raw,
        List<ReferencedIdentifier> references,
        TransformContext context)
    {
        var parts = new List<object>();
        var cursor = 0;
        foreach (var reference in references)
        {
            var start = Clamp(reference.Start, 0, raw.Length);
            var length = reference.Length;
            if (start + length > raw.Length)
            {
                length = raw.Length - start;
            }

            if (length <= 0 || start < cursor)
            {
                continue;
            }

            if (start > cursor)
            {
                parts.Add(raw.Substring(cursor, start - cursor));
            }

            var subLocation = SubLocation(node.Location, raw, start, length);
            var replacement = RewriteIdentifier(reference.Name, context, reference.IsWriteTarget, subLocation);
            parts.Add(Ir.SimpleExpression(replacement, false, subLocation));
            cursor = start + length;
        }

        if (cursor < raw.Length)
        {
            parts.Add(raw.Substring(cursor));
        }

        if (parts.Count == 1 && parts[0] is SimpleExpressionNode only)
        {
            return only;
        }

        return new CompoundExpressionNode { Parts = new SyntaxList<object>(parts.ToArray()), Location = node.Location };
    }

    // ---- diagnostics ----

    private static bool TryGetFirstError(
        Microsoft.CodeAnalysis.SyntaxNode node,
        out Microsoft.CodeAnalysis.Text.TextSpan span,
        out string message)
    {
        foreach (var diagnostic in node.GetDiagnostics())
        {
            if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            {
                span = diagnostic.Location.SourceSpan;
                message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
                return true;
            }
        }

        span = default;
        message = string.Empty;
        return false;
    }

    private static CompilerError CreateInvalidExpressionError(
        SimpleExpressionNode node,
        string raw,
        Microsoft.CodeAnalysis.Text.TextSpan errorSpan,
        string errorMessage,
        int prefixLength)
    {
        var start = Clamp(errorSpan.Start - prefixLength, 0, raw.Length);
        var length = errorSpan.Length;
        if (start + length > raw.Length)
        {
            length = raw.Length - start;
        }

        if (length < 0)
        {
            length = 0;
        }

        var location = SubLocation(node.Location, raw, start, length);
        var message = CompilerErrorMessages.GetMessage(CompilerErrorCode.XInvalidExpression) + errorMessage;
        return new CompilerError(CompilerErrorCode.XInvalidExpression, message, location);
    }

    // ---- position mapping (upstream advancePositionWithClone) ----

    private static SourceLocation SubLocation(SourceLocation expressionLocation, string source, int offset, int length)
    {
        var start = Advance(expressionLocation.Start, source, 0, offset);
        var end = Advance(start, source, offset, length);
        var slice = offset >= 0 && offset + length <= source.Length
            ? source.Substring(offset, length)
            : string.Empty;
        return new SourceLocation(start, end, slice);
    }

    private static Position Advance(Position from, string source, int start, int count)
    {
        var line = from.Line;
        var lastNewLine = -1;
        var end = System.Math.Min(source.Length, start + count);
        for (var index = start; index < end; index++)
        {
            if (source[index] == '\n')
            {
                line++;
                lastNewLine = index;
            }
        }

        var column = lastNewLine == -1 ? from.Column + count : end - lastNewLine;
        return new Position(from.Offset + count, line, column);
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    // ---- referenced identifier ----

    private readonly struct ReferencedIdentifier
    {
        public ReferencedIdentifier(string name, int start, int length, bool isWriteTarget)
        {
            Name = name;
            Start = start;
            Length = length;
            IsWriteTarget = isWriteTarget;
        }

        public string Name { get; }

        public int Start { get; }

        public int Length { get; }

        public bool IsWriteTarget { get; }
    }

    // ---- Roslyn walkers ----

    // Collects identifiers declared inside the expression (lambda parameters, deconstruction designations, LINQ
    // range variables) so they are treated as locals, not references. Conservative: a name declared in any
    // nested scope is excluded everywhere within the expression.
    private sealed class DeclaredIdentifierWalker : CSharpSyntaxWalker
    {
        private readonly HashSet<string> declared;

        public DeclaredIdentifierWalker(HashSet<string> declared) => this.declared = declared;

        public override void VisitParameter(ParameterSyntax node)
        {
            var name = node.Identifier.ValueText;
            if (name.Length > 0)
            {
                declared.Add(name);
            }

            base.VisitParameter(node);
        }

        public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
        {
            declared.Add(node.Identifier.ValueText);
            base.VisitSingleVariableDesignation(node);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            // Locals declared in a multi-statement inline handler (`var next = …;`) are scope
            // identifiers, mirroring upstream walkIdentifiers' variable-declaration handling.
            declared.Add(node.Identifier.ValueText);
            base.VisitVariableDeclarator(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            declared.Add(node.Identifier.ValueText);
            base.VisitForEachStatement(node);
        }

        public override void VisitFromClause(FromClauseSyntax node)
        {
            declared.Add(node.Identifier.ValueText);
            base.VisitFromClause(node);
        }

        public override void VisitLetClause(LetClauseSyntax node)
        {
            declared.Add(node.Identifier.ValueText);
            base.VisitLetClause(node);
        }

        public override void VisitJoinClause(JoinClauseSyntax node)
        {
            declared.Add(node.Identifier.ValueText);
            base.VisitJoinClause(node);
        }

        public override void VisitQueryContinuation(QueryContinuationSyntax node)
        {
            declared.Add(node.Identifier.ValueText);
            base.VisitQueryContinuation(node);
        }
    }

    // Collects the identifiers to rewrite: referenced positions that are not member names, type positions,
    // locally declared names, template-scope locals, or allowed globals.
    private sealed class ReferenceWalker : CSharpSyntaxWalker
    {
        private readonly TransformContext context;
        private readonly HashSet<string> declared;
        private readonly int prefixLength;

        public ReferenceWalker(TransformContext context, HashSet<string> declared, int prefixLength)
        {
            this.context = context;
            this.declared = declared;
            this.prefixLength = prefixLength;
        }

        public List<ReferencedIdentifier> References { get; } = new();

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var name = node.Identifier.ValueText;
            if (IsReferencePosition(node) &&
                !declared.Contains(name) &&
                !context.IsLocalIdentifier(name) &&
                !GlobalAllowList.IsAllowed(name))
            {
                var span = node.Span;
                References.Add(new ReferencedIdentifier(name, span.Start - prefixLength, span.Length, IsWriteTarget(node)));
            }

            base.VisitIdentifierName(node);
        }

        private static bool IsReferencePosition(IdentifierNameSyntax node) => node.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name == node => false,
            MemberBindingExpressionSyntax memberBinding when memberBinding.Name == node => false,
            QualifiedNameSyntax qualified when qualified.Right == node => false,
            NameEqualsSyntax => false,
            NameColonSyntax => false,
            ObjectCreationExpressionSyntax creation when creation.Type == node => false,
            CastExpressionSyntax cast when cast.Type == node => false,
            TypeArgumentListSyntax => false,
            // The member name in an object initializer (`new Point { X = x }`) names a member of the
            // constructed type, never a component binding (upstream's walk never rewrites object keys).
            AssignmentExpressionSyntax initializerAssignment
                when initializerAssignment.Left == node &&
                     initializerAssignment.Parent is InitializerExpressionSyntax => false,
            // The nameof operand is a name, not a value read; rewriting it would change the produced
            // string.
            InvocationExpressionSyntax invocation
                when invocation.Expression == node && node.Identifier.ValueText == "nameof" => false,
            _ => !IsInsideNameOf(node),
        };

        private static bool IsInsideNameOf(Microsoft.CodeAnalysis.SyntaxNode node)
        {
            for (var current = node.Parent; current is not null; current = current.Parent)
            {
                if (current is InvocationExpressionSyntax invocation &&
                    invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWriteTarget(IdentifierNameSyntax node)
        {
            switch (node.Parent)
            {
                case AssignmentExpressionSyntax assignment when assignment.Left == node:
                    return true;
                case PrefixUnaryExpressionSyntax prefix
                    when prefix.Operand == node &&
                         (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)):
                    return true;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.Operand == node &&
                         (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression)):
                    return true;
                default:
                    return false;
            }
        }
    }
}
