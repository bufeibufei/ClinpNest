namespace ClipNest.Models;

public sealed class ClipboardItem
{
    public long Id { get; set; }
    public string ContentText { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string SourceApp { get; set; } = "Unknown";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? FavoritedAt { get; set; }
    public int UseCount { get; set; }
    public int FavoriteOrder { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }
    public string FavoriteAlias { get; set; } = string.Empty;
    public string FavoriteTag { get; set; } = string.Empty;

    public string Preview => ContentText.ReplaceLineEndings(" ");
    public string ShortPreview => Preview.Length <= 96 ? Preview : Preview[..96] + "...";
    public string DisplayTitle => string.IsNullOrWhiteSpace(FavoriteAlias) ? Preview : FavoriteAlias;
    public string ShortDisplayTitle => DisplayTitle.Length <= 42 ? DisplayTitle : DisplayTitle[..42] + "...";
    public string FavoriteMeta => string.IsNullOrWhiteSpace(FavoriteTag) ? SourceApp : $"{FavoriteTag} · {SourceApp}";
    public DateTime FavoriteDisplayTime => FavoritedAt ?? UpdatedAt;
}
