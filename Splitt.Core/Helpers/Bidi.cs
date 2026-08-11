namespace Splitt.Core.Helpers;

/// <summary>
/// Bidi (bidirectional text) helpers. Android and chat apps pick a line's direction
/// from its first strong character, so a Persian sentence that happens to start with
/// a Latin name ("Sara به Amir") gets laid out LTR and reads reversed to an RTL
/// reader. Prefixing an invisible Right-to-Left Mark forces the correct direction.
/// </summary>
public static class Bidi
{
    /// <summary>Right-to-Left Mark (U+200F): invisible, strongly RTL.</summary>
    public const string Rlm = "\u200F";

    /// <summary>Left-to-Right Isolate (U+2066) … Pop Directional Isolate (U+2069).</summary>
    private const string Lri = "⁦";
    private const string Pdi = "⁩";

    /// <summary>Forces RTL paragraph direction on a line that may start with Latin text.</summary>
    public static string Rtl(string line) => Rlm + line;

    /// <summary>
    /// Keeps the parts of a run in written order inside an RTL line. Two numeric runs
    /// separated by a space ("1405/03/09 14:32") would otherwise swap places: the space
    /// between two numbers takes the paragraph direction, so the parts get laid out
    /// right-to-left and the time reads first. The isolate is invisible and leaves the
    /// direction of the surrounding line untouched.
    /// </summary>
    public static string Ltr(string run) => Lri + run + Pdi;
}
