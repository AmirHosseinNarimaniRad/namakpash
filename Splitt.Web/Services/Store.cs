using Microsoft.JSInterop;
using Splitt.Core.Data;

namespace Splitt.Web.Services;

/// <summary>
/// Owns the database and keeps it on the device.
///
/// SQLite runs against Emscripten's in-memory filesystem, which is emptied on every reload, so
/// the file is loaded out of OPFS at startup and written back after every change. Reads go
/// straight to <see cref="Db"/>; every write goes through <see cref="MutateAsync"/>, which is
/// what makes "did we remember to persist that?" impossible to get wrong.
/// </summary>
public sealed class Store : IAsyncDisposable
{
    // Emscripten's filesystem, not the device's. Nothing here survives a reload by itself.
    const string DatabasePath = "/namakpash.db3";

    readonly IJSRuntime _js;
    readonly SemaphoreSlim _saveGate = new(1, 1);
    IJSObjectReference? _module;

    public SplittDatabase Db { get; private set; } = null!;
    public bool Persisted { get; private set; }
    public long Quota { get; private set; }
    public long Usage { get; private set; }
    public string? LoadError { get; private set; }

    public Store(IJSRuntime js) => _js = js;

    public async Task InitializeAsync()
    {
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");

        // Ask before the first write, not after: a granted origin is exempt from eviction, and
        // the answer is worth having in the UI either way.
        Persisted = await _module.InvokeAsync<bool>("requestPersist");

        try
        {
            var bytes = await _module.InvokeAsync<byte[]?>("load");
            if (bytes is { Length: > 0 })
                await File.WriteAllBytesAsync(DatabasePath, bytes);
        }
        catch (Exception ex)
        {
            // A database that cannot be read must not look like an empty one — starting fresh
            // over the top of it would destroy what is there.
            LoadError = ex.Message;
        }

        Db = new SplittDatabase(DatabasePath);
        await Db.InitializeAsync();
        await RefreshEstimateAsync();
    }

    /// <summary>Runs a write and persists the result. Every mutation goes through here.</summary>
    public async Task MutateAsync(Func<SplittDatabase, Task> write)
    {
        await write(Db);
        await SaveAsync();
    }

    /// <inheritdoc cref="MutateAsync(Func{SplittDatabase, Task})"/>
    public async Task<T> MutateAsync<T>(Func<SplittDatabase, Task<T>> write)
    {
        var result = await write(Db);
        await SaveAsync();
        return result;
    }

    public async Task SaveAsync()
    {
        if (_module is null) return;

        // Two saves overlapping would interleave their writes to the same file.
        await _saveGate.WaitAsync();
        try
        {
            if (!File.Exists(DatabasePath)) return;
            var bytes = await File.ReadAllBytesAsync(DatabasePath);
            await _module.InvokeVoidAsync("save", bytes);
        }
        finally
        {
            _saveGate.Release();
        }

        await RefreshEstimateAsync();
    }

    async Task RefreshEstimateAsync()
    {
        if (_module is null) return;
        var estimate = await _module.InvokeAsync<StorageEstimate?>("estimate");
        if (estimate is null) return;
        Quota = estimate.Quota;
        Usage = estimate.Usage;
    }

    public async Task DownloadTextAsync(string filename, string text)
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("downloadText", filename, text);
    }

    public async Task<string?> PickTextFileAsync()
    {
        if (_module is null) return null;
        return await _module.InvokeAsync<string?>("pickTextFile");
    }

    public async Task PrintHtmlAsync(string html)
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("printHtml", html);
    }

    /// <summary>Used by the self-test route so an automated run can collect its result.</summary>
    public async Task<bool> PostJsonAsync(string url, string json)
    {
        if (_module is null) return false;
        return await _module.InvokeAsync<bool>("postJson", url, json);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }

    public sealed class StorageEstimate
    {
        public long Quota { get; set; }
        public long Usage { get; set; }
    }
}
