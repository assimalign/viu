using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins every state the Automatic Brace Completion override decision can be asked about
/// ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// Three booleans is eight states, so the whole truth table is written out rather than sampled: this
/// decision is the only thing standing between "Viu repairs a view Visual Studio broke" and "Viu
/// forces a feature on a user who turned it off", and the difference between those is one row.
/// </remarks>
public class ViuBraceCompletionEnablementTests
{
    [Theory]
    // The one state that is repaired: the legacy editor adapter defined the option false on the view
    // while the user's global choice is on.
    [InlineData(true, false, true, true)]
    // Locally defined and off, but the user turned brace completion off globally too - their off
    // switch, and clearing would be Viu overriding it.
    [InlineData(true, false, false, false)]
    // Locally defined and already on: somebody deliberately enabled it, nothing to repair.
    [InlineData(true, true, true, false)]
    // Locally defined true while the global is false - still on for this view, so still nothing to do.
    [InlineData(true, true, false, false)]
    // Not defined on the view at all: whatever the view resolves is already inherited from the user.
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, false)]
    public void ShouldClearViewOverride_EveryState(
        bool definedOnThisView,
        bool effectiveValue,
        bool globalValue,
        bool expected)
    {
        ViuBraceCompletionEnablement
            .ShouldClearViewOverride(definedOnThisView, effectiveValue, globalValue)
            .ShouldBe(expected);
    }

    [Fact]
    public void ShouldClearViewOverride_AfterClearing_IsFalse_SoTheRepairCannotLoop()
    {
        // Clearing removes the local definition and the option-changed notification re-asks
        // immediately. This is that second question, and answering it false is what bounds the
        // recursion at depth two without a guard flag.
        ViuBraceCompletionEnablement
            .ShouldClearViewOverride(definedOnThisView: true, effectiveValue: false, globalValue: true)
            .ShouldBeTrue();

        ViuBraceCompletionEnablement
            .ShouldClearViewOverride(definedOnThisView: false, effectiveValue: true, globalValue: true)
            .ShouldBeFalse();
    }
}
