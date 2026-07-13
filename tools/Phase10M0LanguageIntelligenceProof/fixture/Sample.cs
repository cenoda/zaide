using System;

namespace Fixture;

/// <summary>
/// Sample types used by the Phase 10 M0 language-intelligence proof.
/// Contains intentional diagnostics and multi-byte content for position tests.
/// </summary>
public static class Sample
{
    /// <summary>A simple greet helper for hover/definition/completion targets.</summary>
    public static string Greet(string name)
    {
        return "Hello, " + name;
    }

    // Intentional syntax error for diagnostics proof: missing semicolon.
    public static int Broken()
    {
        var value = 42
        return value;
    }

    // Non-BMP and CJK string content for position-encoding round-trip notes.
    public static string Celebration() => "庆祝 🎉";

    public static void CallSite()
    {
        var msg = Greet("world");
        Console.WriteLine(msg);
        Console.WriteLine(Celebration());
    }
}
