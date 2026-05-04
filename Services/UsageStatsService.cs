using ClipNest.Data;

namespace ClipNest.Services;

public sealed class UsageStatsService(SettingsRepository settingsRepository)
{
    private const string CopyCountKey = "usage_copy_count";
    private const string PasteCountKey = "usage_paste_count";

    public event EventHandler? Changed;

    public async Task<(int CopyCount, int PasteCount)> GetAsync()
        => (await GetIntAsync(CopyCountKey), await GetIntAsync(PasteCountKey));

    public async Task IncrementCopyAsync()
    {
        await IncrementAsync(CopyCountKey);
    }

    public async Task IncrementPasteAsync()
    {
        await IncrementAsync(PasteCountKey);
    }

    public async Task ResetAsync()
    {
        await settingsRepository.SetAsync(CopyCountKey, "0");
        await settingsRepository.SetAsync(PasteCountKey, "0");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task IncrementAsync(string key)
    {
        var value = await GetIntAsync(key);
        await settingsRepository.SetAsync(key, (value + 1).ToString());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<int> GetIntAsync(string key)
    {
        var value = await settingsRepository.GetAsync(key);
        return int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : 0;
    }
}
