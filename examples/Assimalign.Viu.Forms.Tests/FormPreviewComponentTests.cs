using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Forms.Components;
using Assimalign.Viu.RuntimeCore;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Forms.Tests;

// Mounts the live-preview component through the in-memory Assimalign.Viu.Testing renderer and asserts
// it reflects the reactive model — including a reactive update after the model changes and the
// scheduler flushes. (The form's v-model inputs are a browser concern and are not mounted here.)
public sealed class FormPreviewComponentTests
{
    [Fact]
    public async Task Preview_ReflectsTheModel_AndUpdatesWhenItChanges()
    {
        var form = new RegistrationForm();
        var options = new ComponentMountOptions
        {
            Properties = VirtualNodeFactory.Properties(("form", form)),
        };
        using var wrapper = ViuTest.Mount(new FormPreviewComponent(), options);

        wrapper.Get(".status").Text().ShouldBe("Incomplete");
        wrapper.Get(".summary").Text().ShouldBe(form.Summary.Value);
        wrapper.Get(".summary").Text().ShouldContain("(no name)");

        form.FullName.Value = "Ada Lovelace";
        form.Email.Value = "ada@example.com";
        form.AcceptsTerms.Value = true;
        await wrapper.FlushAsync();

        wrapper.Get(".status").Text().ShouldBe("Ready to submit");
        wrapper.Get(".summary").Text().ShouldContain("Ada Lovelace");
    }
}
