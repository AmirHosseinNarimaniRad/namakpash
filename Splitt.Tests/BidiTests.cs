using Splitt.Core.Helpers;

namespace Splitt.Tests;

/// <summary>
/// Both helpers exist because of bugs that only show up with Latin names — Persian
/// names hide them. These lock the invisible characters in place, since the symptom
/// (text rendering in the wrong order) is invisible to any assertion on the words alone.
/// </summary>
public class BidiTests
{
    [Fact]
    public void Rtl_PrefixesALineThatWouldOtherwiseStartWithLatinText()
    {
        // Without the mark Android picks direction from the first strong character,
        // so "Sara به Amir" lays out LTR and an RTL reader sees "Amir به Sara".
        var line = Bidi.Rtl("Sara به Amir");

        Assert.StartsWith(Bidi.Rlm, line);
        Assert.Equal("Sara به Amir", line[Bidi.Rlm.Length..]);
    }

    [Fact]
    public void Ltr_IsolatesARunSoItsPartsKeepTheirOrder()
    {
        // Two numeric runs with a space between them swap inside an RTL line: the
        // space takes the paragraph direction, so "1405/03/09 14:32" would read
        // "14:32 1405/03/09". The isolate pins the run's internal order.
        var run = Bidi.Ltr("1405/03/09 14:32");

        Assert.StartsWith("⁦", run);
        Assert.EndsWith("⁩", run);
        Assert.Contains("1405/03/09 14:32", run);
    }

    [Fact]
    public void Ltr_LeavesTheSurroundingLineAlone()
    {
        // The isolate is transparent from outside, so a line can still be marked RTL.
        var line = Bidi.Rtl($"پرداخت: Sara · {Bidi.Ltr("1405/03/09 14:32")}");

        Assert.StartsWith(Bidi.Rlm, line);
    }
}
