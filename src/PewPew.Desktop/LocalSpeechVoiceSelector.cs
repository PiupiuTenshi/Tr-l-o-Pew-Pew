using PewPew.Application.Speech;

namespace PewPew.Desktop;

/// <summary>
/// Selects an installed local voice from the text language without sending text to a service.
/// </summary>
public static class LocalSpeechVoiceSelector
{
    private static readonly string[] VietnameseMarkers =
    [
        " bạn ", " tôi ", " không ", " được ", " chức năng ", " văn bản ", " cục bộ ", " xin ", " chào ", " cảm ơn "
    ];

    public static bool IsVietnamese(string text)
    {
        if (text.Any(character => "ăâđêôơưĂÂĐÊÔƠƯáàảãạấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ".Contains(character)))
        {
            return true;
        }

        var normalized = $" {text.Trim().ToLowerInvariant()} ";
        return VietnameseMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    public static string ResolveVoiceId(string text, IReadOnlyList<LocalSpeechVoice> availableVoices)
    {
        ArgumentNullException.ThrowIfNull(availableVoices);

        var candidates = availableVoices.Where(voice => voice.Id != LocalSpeechVoice.AutomaticId).ToArray();
        if (candidates.Length == 0)
        {
            return string.Empty;
        }

        var culturePrefix = IsVietnamese(text) ? "vi" : "en";
        return candidates.FirstOrDefault(voice => voice.CultureName.StartsWith(culturePrefix, StringComparison.OrdinalIgnoreCase))?.Id
            ?? candidates[0].Id;
    }
}
