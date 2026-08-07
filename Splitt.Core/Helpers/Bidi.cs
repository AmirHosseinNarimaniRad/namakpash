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

    /// <summary>Forces RTL paragraph direction on a line that may start with Latin text.</summary>
    public static string Rtl(string line) => Rlm + line;
}
