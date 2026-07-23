using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// Pins the [V01.01.04.02] patchProp decision tree against @vue/runtime-dom's patchProp and
// modules (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/patchProp.ts).
// The tree runs on the .NET side over recorded leaf ops — no DOM, no interop — so every
// resolution's exact leaf sequence is asserted (each leaf is one interop call in production).
public class BrowserPropertyPatcherTests
{
    private const int Element = 7;

    private readonly List<string> _calls = [];
    private readonly BrowserPropertyLeafOperations _leaves;

    public BrowserPropertyPatcherTests()
    {
        _leaves = new BrowserPropertyLeafOperations
        {
            SetAttribute = (element, name, value) => _calls.Add($"setAttribute({element},{name},{value})"),
            RemoveAttribute = (element, name) => _calls.Add($"removeAttribute({element},{name})"),
            SetXlinkAttribute = (element, name, value) => _calls.Add($"setXlinkAttribute({element},{name},{value})"),
            RemoveXlinkAttribute = (element, name) => _calls.Add($"removeXlinkAttribute({element},{name})"),
            SetClassName = (element, value) => _calls.Add($"setClassName({element},{value})"),
            SetStringProperty = (element, name, value) => _calls.Add($"setStringProperty({element},{name},{value})"),
            SetBooleanProperty = (element, name, value) => _calls.Add($"setBooleanProperty({element},{name},{(value ? "true" : "false")})"),
            SetValueGuarded = (element, value) => _calls.Add($"setValueGuarded({element},{value})"),
            SetStyleText = (element, cssText) => _calls.Add($"setStyleText({element},{cssText})"),
            SetStyleProperty = (element, name, value, important) => _calls.Add($"setStyleProperty({element},{name},{value},{(important ? "important" : "normal")})"),
            RemoveStyleProperty = (element, name) => _calls.Add($"removeStyleProperty({element},{name})"),
            SetEventListener = (element, eventName, listener) => _calls.Add($"setEventListener({element},{eventName},{(listener is null ? "null" : "delegate")})"),
        };
    }

    private void Patch(string tag, string name, object? previous, object? next, string? elementNamespace = null)
        => BrowserPropertyPatcher.Patch(_leaves, Element, tag, name, previous, next, elementNamespace);

    // --- prop-vs-attribute dispatch (upstream shouldSetAsProp) ------------------------------

    [Theory]
    [InlineData("input")]
    [InlineData("textarea")]
    [InlineData("select")]
    public void Value_OnFormControls_IsSetAsAGuardedDomProperty(string tag)
    {
        Patch(tag, "value", null, "typed");
        _calls.ShouldBe([$"setValueGuarded({Element},typed)"]);
    }

    [Fact]
    public void Value_OnOtherElements_TakesTheAttributePath()
    {
        Patch("progress", "value", null, "5");
        _calls.ShouldBe([$"setAttribute({Element},value,5)"]);
    }

    [Fact]
    public void Value_Null_ClearsThroughTheGuardedSetter()
    {
        Patch("input", "value", "old", null);
        _calls.ShouldBe([$"setValueGuarded({Element},)"]);
    }

    [Fact]
    public void Form_IsAlwaysAnAttribute()
    {
        // The form IDL property is readonly (upstream parity).
        Patch("input", "form", null, "checkout");
        _calls.ShouldBe([$"setAttribute({Element},form,checkout)"]);
    }

    [Theory]
    [InlineData("checked")]
    [InlineData("disabled")]
    [InlineData("multiple")]
    [InlineData("selected")]
    public void NativeBooleanProperties_GoThroughThePropertyPath(string name)
    {
        Patch("input", name, null, true);
        _calls.ShouldBe([$"setBooleanProperty({Element},{name},true)"]);
    }

    [Fact]
    public void UnknownKeys_FallBackToAttributes()
    {
        Patch("div", "data-testid", null, "widget");
        Patch("div", "aria-label", null, "Close");
        Patch("div", "custom-thing", null, "x");
        _calls.ShouldBe(
        [
            $"setAttribute({Element},data-testid,widget)",
            $"setAttribute({Element},aria-label,Close)",
            $"setAttribute({Element},custom-thing,x)",
        ]);
    }

    [Fact]
    public void ListOnInput_AndTypeOnTextarea_AndMediaDimensions_StayAttributes()
    {
        Patch("input", "list", null, "suggestions");
        Patch("textarea", "type", null, "text");
        Patch("img", "width", null, 640);
        Patch("canvas", "height", null, 480);
        _calls.ShouldBe(
        [
            $"setAttribute({Element},list,suggestions)",
            $"setAttribute({Element},type,text)",
            $"setAttribute({Element},width,640)",
            $"setAttribute({Element},height,480)",
        ]);
    }

    [Fact]
    public void InnerHtmlAndTextContent_AreStringProperties()
    {
        Patch("div", "innerHTML", null, "<b>x</b>");
        Patch("div", "textContent", null, "plain");
        _calls.ShouldBe(
        [
            $"setStringProperty({Element},innerHTML,<b>x</b>)",
            $"setStringProperty({Element},textContent,plain)",
        ]);
    }

    // --- class fast paths -------------------------------------------------------------------

    [Fact]
    public void Class_OnHtml_IsASingleClassNameWrite()
    {
        Patch("div", "class", null, "card active");
        _calls.ShouldBe([$"setClassName({Element},card active)"]);
    }

    [Fact]
    public void Class_OnSvg_UsesSetAttribute()
    {
        // SVGElement.className is an SVGAnimatedString — must go through setAttribute
        // (upstream parity).
        Patch("circle", "class", null, "dot", "svg");
        _calls.ShouldBe([$"setAttribute({Element},class,dot)"]);
    }

    [Fact]
    public void Class_Null_RemovesTheAttribute()
    {
        Patch("div", "class", "old", null);
        _calls.ShouldBe([$"removeAttribute({Element},class)"]);
    }

    // --- style ------------------------------------------------------------------------------

    [Fact]
    public void Style_StringValues_UseTheCssTextFastPath_AndSkipWhenUnchanged()
    {
        Patch("div", "style", null, "color:red");
        Patch("div", "style", "color:red", "color:red");
        Patch("div", "style", "color:red", "color:blue");
        _calls.ShouldBe(
        [
            $"setStyleText({Element},color:red)",
            $"setStyleText({Element},color:blue)",
        ]);
    }

    [Fact]
    public void Style_Maps_PatchOnlyChangedKeys_AndRemoveStaleKeys()
    {
        var previous = new Dictionary<string, object?>
        {
            ["color"] = "red",
            ["width"] = "10px",
            ["margin"] = "1px",
        };
        var next = new Dictionary<string, object?>
        {
            ["color"] = "red",     // unchanged: no call
            ["width"] = "20px",    // changed: one call
            ["padding"] = "2px",   // added: one call
        };

        Patch("div", "style", previous, next);

        _calls.ShouldBe(
        [
            $"removeStyleProperty({Element},margin)",
            $"setStyleProperty({Element},width,20px,normal)",
            $"setStyleProperty({Element},padding,2px,normal)",
        ]);
    }

    [Fact]
    public void Style_SupportsCustomPropertiesAndImportant()
    {
        var next = new Dictionary<string, object?>
        {
            ["--brand-color"] = "#123456",
            ["color"] = "red !important",
        };

        Patch("div", "style", null, next);

        _calls.ShouldBe(
        [
            $"setStyleProperty({Element},--brand-color,#123456,normal)",
            $"setStyleProperty({Element},color,red,important)",
        ]);
    }

    [Fact]
    public void Style_Null_RemovesTheAttribute()
    {
        Patch("div", "style", "color:red", null);
        _calls.ShouldBe([$"removeAttribute({Element},style)"]);
    }

    // --- falsy semantics (upstream attrs module) ---------------------------------------------

    [Fact]
    public void NullValues_RemoveTheAttribute()
    {
        Patch("div", "title", "old", null);
        _calls.ShouldBe([$"removeAttribute({Element},title)"]);
    }

    [Fact]
    public void False_RemovesGenuineBooleanAttributes()
    {
        Patch("input", "checked", true, false);
        Patch("input", "readonly", true, false); // attribute-path boolean (IDL name differs)
        _calls.ShouldBe(
        [
            $"setBooleanProperty({Element},checked,false)",
            $"removeAttribute({Element},readonly)",
        ]);
    }

    [Fact]
    public void False_WritesTheStringFalse_ForEnumeratedAttributes()
    {
        // spellcheck/draggable are enumerated: removal would mean "inherit", so false must be
        // written out (upstream parity).
        Patch("div", "spellcheck", null, false);
        Patch("div", "draggable", null, false);
        _calls.ShouldBe(
        [
            $"setAttribute({Element},spellcheck,false)",
            $"setAttribute({Element},draggable,false)",
        ]);
    }

    [Fact]
    public void True_WritesEmptyString_ForAttributePathBooleans()
    {
        Patch("form", "novalidate", null, true);
        _calls.ShouldBe([$"setAttribute({Element},novalidate,)"]);
    }

    // --- xlink namespace ----------------------------------------------------------------------

    [Fact]
    public void XlinkAttributes_OnSvg_SetAndRemoveInTheXlinkNamespace()
    {
        Patch("use", "xlink:href", null, "#icon", "svg");
        Patch("use", "xlink:href", "#icon", null, "svg");
        _calls.ShouldBe(
        [
            $"setXlinkAttribute({Element},xlink:href,#icon)",
            $"removeXlinkAttribute({Element},xlink:href)",
        ]);
    }

    // --- events -------------------------------------------------------------------------------

    [Fact]
    public void EventProps_FlowTheRawNameToTheInvokerRegistry()
    {
        // The registry parses the event name and Once/Capture/Passive suffixes
        // ([V01.01.04.03]); the patcher passes the raw prop through untouched.
        var handler = (Action)(() => { });
        Patch("button", "onClick", null, handler);
        Patch("button", "onClickCaptureOnce", null, handler);
        Patch("button", "onClick", handler, null);
        _calls.ShouldBe(
        [
            $"setEventListener({Element},onClick,delegate)",
            $"setEventListener({Element},onClickCaptureOnce,delegate)",
            $"setEventListener({Element},onClick,null)",
        ]);
    }

    // --- SVG general routing ------------------------------------------------------------------

    [Fact]
    public void SvgElements_RouteOrdinaryKeysToAttributes()
    {
        Patch("circle", "cx", null, 50, "svg");
        _calls.ShouldBe([$"setAttribute({Element},cx,50)"]);
    }

    // --- formatting ---------------------------------------------------------------------------

    [Fact]
    public void NonStringValues_FormatWithInvariantCulture()
    {
        Patch("div", "data-ratio", null, 1.5);
        // Invariant culture: "1.5" even under comma-decimal locales.
        _calls.ShouldBe([$"setAttribute({Element},data-ratio,1.5)"]);
    }

    [Fact]
    public void BooleanAttributeInventory_CoversTheUpstreamSet()
    {
        // Spot-pins the curated knowledge sets (full per-tag tables land with [V01.01.01.03]).
        BrowserPropertyPatcher.IsBooleanAttributeName("checked").ShouldBeTrue();
        BrowserPropertyPatcher.IsBooleanAttributeName("readonly").ShouldBeTrue();
        BrowserPropertyPatcher.IsBooleanAttributeName("itemscope").ShouldBeTrue();
        BrowserPropertyPatcher.IsBooleanAttributeName("allowfullscreen").ShouldBeTrue();
        BrowserPropertyPatcher.IsBooleanAttributeName("title").ShouldBeFalse();
    }
}
