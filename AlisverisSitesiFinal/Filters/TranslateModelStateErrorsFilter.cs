// Filters/TranslateModelStateErrorsPageFilter.cs
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.RegularExpressions;

public class TranslateModelStateErrorsPageFilter : IPageFilter
{
    public void OnPageHandlerSelected(PageHandlerSelectedContext context) { }

    public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        var ms = context.ModelState;
        if (ms == null || ms.Count == 0) return;

        var pageModel = context.HandlerInstance as PageModel;
        var rootExplorer = pageModel?.ViewData?.ModelExplorer;

        foreach (var key in ms.Keys.ToList())
        {
            var entry = ms[key];
            if (entry?.Errors == null || entry.Errors.Count == 0) continue;

            // 1) ModelExplorer ile gerçek DisplayName’i bul
            string fieldDisplay = ResolveDisplayName(rootExplorer, key)
                                  ?? TrimInputPrefix(key)                // 2) Olmazsa Input. önekini temizle
                                  ?? key;

            for (int i = 0; i < entry.Errors.Count; i++)
            {
                var msg = entry.Errors[i].ErrorMessage?.Trim();
                if (string.IsNullOrEmpty(msg)) continue;

                var tr = Translate(msg, fieldDisplay);
                if (tr != msg)
                    entry.Errors[i] = new ModelError(tr);
            }
        }
    }

    public void OnPageHandlerExecuted(PageHandlerExecutedContext context) { }

    // "Input.Ad" -> "Ad" gibi segmentlere inip DisplayName’i çözer
    private static string? ResolveDisplayName(ModelExplorer? explorer, string key)
    {
        if (explorer == null || string.IsNullOrWhiteSpace(key)) return null;

        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = explorer;
        foreach (var seg in segments)
        {
            current = current.GetExplorerForProperty(seg);
            if (current == null) return null;
        }
        try
        {
            var name = current.Metadata.GetDisplayName();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch { return null; }
    }

    // Güvenli yedek: "Input.EmailConfirm" -> "EmailConfirm"
    private static string TrimInputPrefix(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return key;
        var idx = key.LastIndexOf('.');
        return idx >= 0 ? key[(idx + 1)..] : key;
    }

    private static string Translate(string msg, string field)
    {
        var m = Regex.Match(msg, @"^The (.+) field is required\.?$", RegexOptions.IgnoreCase);
        if (m.Success) return $"{field} alanı zorunludur.";

        if (msg.Contains("is not a valid e-mail address", StringComparison.OrdinalIgnoreCase))
            return "Geçerli bir e-posta adresi giriniz.";

        if (msg.Contains("The value '' is invalid", StringComparison.OrdinalIgnoreCase) ||
            msg.Equals("The value is not valid.", StringComparison.OrdinalIgnoreCase))
            return "Geçersiz değer girdiniz.";

        if (Regex.IsMatch(msg, @"must be a number", RegexOptions.IgnoreCase))
            return "Bu alan yalnızca sayı olmalıdır.";

        if (Regex.IsMatch(msg, @"must be at least", RegexOptions.IgnoreCase))
            return "Girilen değer çok kısa.";

        if (Regex.IsMatch(msg, @"must be at most", RegexOptions.IgnoreCase))
            return "Girilen değer çok uzun.";

        if (Regex.IsMatch(msg, @"and (.+) do not match", RegexOptions.IgnoreCase) ||
            msg.Contains("do not match", StringComparison.OrdinalIgnoreCase))
            return "Alanlar eşleşmiyor.";

        return msg;
    }
}
