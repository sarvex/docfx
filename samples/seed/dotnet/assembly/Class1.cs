using System.ComponentModel;

namespace BuildFromAssembly;

/// <summary>
/// This is a test class.
/// </summary>
public class Class1
{
    public static void HelloWorld() { }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void HiddenAPI() { }
}

public record struct Issue8623_RecordStruct(int X, int Y);

public record class Issue8623_RecordClass(int X, int Y);
