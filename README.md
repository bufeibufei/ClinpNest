# ClipNest

ClipNest is a Windows clipboard history app built with WPF and .NET 8.

## Run

```powershell
dotnet run --project .\ClipNest.csproj
```

If `dotnet` is not available in the current shell session after installing the SDK:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\ClipNest.csproj
```

## First version scope

- Text clipboard history
- SQLite local storage
- Duplicate detection by content hash
- Global quick panel hotkey
- Customizable hotkey in Settings
- Search by text or source app
- Paste selected history item
- Favorite and delete records
- Pause or resume clipboard capture
- System tray menu
- Basic sensitive content filtering

## Defaults

- Quick panel hotkey: `Ctrl + Shift + V`
- Database path: `%LOCALAPPDATA%\ClipNest\clipnest.db`

## Notes

The first version records text only. Image, file, OCR, cloud sync, encryption, and tagging are intentionally left for later iterations.
