using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins that the editor diagnostics trace is dormant unless it is asked for, and that a trace call
/// can never throw ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// The trace exists to investigate a behavior that only reproduces inside <c>devenv.exe</c>, so what
/// is testable here is exactly what matters for shipping it: that a user who never sets the
/// environment variable pays nothing and gets no file, and that a diagnostics fault cannot become an
/// editing fault. The environment variable is read once at type initialization, which is why these
/// assert the off state rather than trying to toggle it mid-process.
/// </remarks>
public class ViuEditorDiagnosticsTests
{
    [Fact]
    public void IsEnabled_WithoutTheEnvironmentVariable_IsOff()
    {
        // The test process never sets it, so this is the shipped default: no trace, no file path.
        Environment.GetEnvironmentVariable(ViuEditorDiagnostics.EnvironmentVariableName)
            .ShouldBeNullOrEmpty();
        ViuEditorDiagnostics.IsEnabled.ShouldBeFalse();
        ViuEditorDiagnostics.LogFilePath.ShouldBeNull();
    }

    [Fact]
    public void Trace_WhileOff_NeverRunsTheMessageFactory()
    {
        // The factory not running is the whole cost story: with the trace off a call site pays a
        // static field read and does not even build its message.
        //
        // This deliberately does not assert that no log file exists. An earlier revision did, and it
        // failed on a machine where the developer had run the diagnostics for real - it was asserting
        // about the temporary directory's history rather than about this code. The invariant that
        // actually belongs to the sink is that it holds no path to write to, which the test above
        // pins.
        bool messageFactoryRan = false;
        ViuEditorDiagnostics.Trace(
            "test",
            () =>
            {
                messageFactoryRan = true;
                return "should never be built";
            });

        messageFactoryRan.ShouldBeFalse();
    }

    [Fact]
    public void Trace_WithAThrowingMessageFactory_DoesNotThrow()
    {
        // Total by contract: a diagnostics fault must cost a log line, never a keystroke.
        Should.NotThrow(() => ViuEditorDiagnostics.Trace(
            "test",
            () => throw new InvalidOperationException("message factory fault")));
    }

    [Fact]
    public void Describe_ControlCharacters_StayOnOneLine()
    {
        // One event is one line, so nothing a buffer can contain may break the file's shape.
        ViuEditorDiagnostics.Describe("a\r\nb").ShouldBe("\"a\\r\\nb\"");
        ViuEditorDiagnostics.Describe("\t").ShouldBe("\"\\t\"");
        ViuEditorDiagnostics.Describe("say \"hi\"").ShouldBe("\"say \\\"hi\\\"\"");
        ViuEditorDiagnostics.Describe("back\\slash").ShouldBe("\"back\\\\slash\"");
        ViuEditorDiagnostics.Describe("\u0001").ShouldBe("\"\\u0001\"");
    }

    [Fact]
    public void Describe_Null_AndEmpty_AreDistinguishable()
    {
        ViuEditorDiagnostics.Describe(null).ShouldBe("<null>");
        ViuEditorDiagnostics.Describe(string.Empty).ShouldBe("\"\"");
    }

    [Fact]
    public void Describe_ACharacter_IsQuotedLikeText()
    {
        ViuEditorDiagnostics.Describe('(').ShouldBe("\"(\"");
        ViuEditorDiagnostics.Describe('\n').ShouldBe("\"\\n\"");
    }

    [Fact]
    public void Describe_LongText_IsTruncatedWithTheRemainderCounted()
    {
        string described = ViuEditorDiagnostics.Describe(new string('x', 150));

        described.ShouldStartWith("\"" + new string('x', 120) + "\"");
        described.ShouldEndWith("(+30 more)");
    }
}
