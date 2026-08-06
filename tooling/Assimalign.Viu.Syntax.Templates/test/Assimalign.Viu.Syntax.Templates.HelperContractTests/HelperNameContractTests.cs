using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Syntax.Templates.HelperContractTests;

// [V01.01.14.08] Every canonical compiler spelling must bind to the designated public runtime facade;
// this catches a broken by-name contract in Viu's own build instead of a consuming application's build.
public sealed class HelperNameContractTests
{
    [Fact]
    public void HelperNames_EveryEntryResolvesToItsPublicRuntimeSurface()
    {
        var helperEntries = typeof(HelperNames)
            .GetFields(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Where(field => field.FieldType == typeof(RuntimeHelper))
            .Select(field => new
            {
                FieldName = field.Name,
                Helper = field.GetValue(null).ShouldBeOfType<RuntimeHelper>(),
            })
            .OrderBy(entry => entry.FieldName, StringComparer.Ordinal)
            .ToArray();
        HashSet<RuntimeHelper> tableEntries = helperEntries
            .Select(entry => entry.Helper)
            .ToHashSet();
        HashSet<RuntimeHelper> domHelpers = HelperNames.DomHelpers.ToHashSet();

        HelperNames.DomHelpers.ShouldAllBe(helper => tableEntries.Contains(helper));

        var missingMembers = new List<string>();
        foreach (var entry in helperEntries)
        {
            Type surface = domHelpers.Contains(entry.Helper)
                ? typeof(DomRenderHelpers)
                : typeof(RenderHelpers);
            string memberName = "_" + entry.Helper.Name;
            if (surface.GetMember(
                    memberName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0)
            {
                missingMembers.Add($"{entry.FieldName} -> {surface.FullName}.{memberName}");
            }
        }

        missingMembers.ShouldBeEmpty();
    }
}
