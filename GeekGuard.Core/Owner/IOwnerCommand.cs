namespace GeekGuard.Core.Owner;

/// <summary>
/// A command only the bot's owners can use, in the private chat (e.g. /unflag). Plugins add their own with
/// <c>services.AddSingleton&lt;IOwnerCommand, T&gt;()</c>. Others get no answer at all.
/// </summary>
public interface IOwnerCommand
{
    /// <summary>Without the slash, lowercase, e.g. "unflag".</summary>
    string Name { get; }

    /// <summary>Runs the command. <paramref name="args"/> are the words after it. Returns the reply (HTML).</summary>
    Task<string> RunAsync(string[] args, CancellationToken ct);
}

/// <summary>Adds lines to the owner's /stats. Register with <c>services.AddSingleton&lt;IOwnerStats, T&gt;()</c>.</summary>
public interface IOwnerStats
{
    /// <summary>Lines like «🛡 اسپمرهای شناخته‌شده: <b>12</b>».</summary>
    Task<IEnumerable<string>> LinesAsync(CancellationToken ct);
}
