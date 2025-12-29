using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AlisverisSitesiFinal.Models
{
    public class IBANGuncelleViewModel
    {
        [Display(Name = "Ad Soyad")]
        public string AdSoyad { get; set; } = string.Empty;

        [Required(ErrorMessage = "IBAN alanı boş bırakılamaz.")]
        [CustomValidation(typeof(IBANGuncelleViewModel), nameof(ValidateIBAN))]
        public string IBAN { get; set; } = string.Empty;

        // Tüm boşluk türlerini temizle (normal boşluk, non‑breaking, tab vs.)
        public static string CleanIban(string? s) =>
            Regex.Replace(s ?? "", @"[\p{Z}\p{C}]+", "").ToUpperInvariant();

        // CustomValidation doğru imza: object value
        public static ValidationResult? ValidateIBAN(object? value, ValidationContext _)
        {
            var clean = CleanIban(value as string);

            // Sadece format kontrolü: TR + 24 rakam (toplam 26)
            if (clean.Length != 26 || !Regex.IsMatch(clean, @"^TR\d{24}$"))
                return new ValidationResult("Geçerli bir IBAN giriniz (TR ile başlayıp toplam 26 karakter olmalı).");

            return ValidationResult.Success;
        }
    }
}
