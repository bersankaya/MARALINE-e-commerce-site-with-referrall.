using Microsoft.AspNetCore.Identity;

namespace AlisverisSitesiFinal.Services
{
    public class TurkishIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError DefaultError() =>
            new() { Code = nameof(DefaultError), Description = "Bilinmeyen bir hata oluştu." };

        public override IdentityError ConcurrencyFailure() =>
            new() { Code = nameof(ConcurrencyFailure), Description = "Eşzamanlılık hatası. Lütfen tekrar deneyin." };

        public override IdentityError PasswordMismatch() =>
            new() { Code = nameof(PasswordMismatch), Description = "Şifre hatalı." };

        public override IdentityError InvalidToken() =>
            new() { Code = nameof(InvalidToken), Description = "Geçersiz doğrulama kodu." };

        public override IdentityError LoginAlreadyAssociated() =>
            new() { Code = nameof(LoginAlreadyAssociated), Description = "Bu kullanıcı girişe zaten bağlı." };

        public override IdentityError InvalidUserName(string? userName) =>
            new() { Code = nameof(InvalidUserName), Description = $"Geçersiz kullanıcı adı: '{userName}'." };

        public override IdentityError InvalidEmail(string? email) =>
            new() { Code = nameof(InvalidEmail), Description = $"Geçersiz e-posta: '{email}'." };

        public override IdentityError DuplicateUserName(string userName) =>
            new() { Code = nameof(DuplicateUserName), Description = $"'{userName}' kullanıcı adı zaten kullanılıyor." };

        public override IdentityError DuplicateEmail(string email) =>
            new() { Code = nameof(DuplicateEmail), Description = $"'{email}' e-posta adresi zaten kayıtlı." };

        public override IdentityError InvalidRoleName(string? role) =>
            new() { Code = nameof(InvalidRoleName), Description = $"Geçersiz rol adı: '{role}'." };

        public override IdentityError DuplicateRoleName(string role) =>
            new() { Code = nameof(DuplicateRoleName), Description = $"'{role}' rolü zaten mevcut." };

        public override IdentityError UserAlreadyHasPassword() =>
            new() { Code = nameof(UserAlreadyHasPassword), Description = "Kullanıcının zaten bir şifresi var." };

        public override IdentityError UserLockoutNotEnabled() =>
            new() { Code = nameof(UserLockoutNotEnabled), Description = "Bu kullanıcı için kilitleme etkin değil." };

        public override IdentityError UserAlreadyInRole(string role) =>
            new() { Code = nameof(UserAlreadyInRole), Description = $"Kullanıcı zaten '{role}' rolünde." };

        public override IdentityError UserNotInRole(string role) =>
            new() { Code = nameof(UserNotInRole), Description = $"Kullanıcı '{role}' rolünde değil." };

        public override IdentityError PasswordTooShort(int length) =>
            new() { Code = nameof(PasswordTooShort), Description = $"Şifre en az {length} karakter olmalıdır." };

        public override IdentityError PasswordRequiresNonAlphanumeric() =>
            new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Şifre en az bir alfasayısal olmayan karakter içermelidir." };

        public override IdentityError PasswordRequiresDigit() =>
            new() { Code = nameof(PasswordRequiresDigit), Description = "Şifre en az bir rakam içermelidir." };

        public override IdentityError PasswordRequiresLower() =>
            new() { Code = nameof(PasswordRequiresLower), Description = "Şifre en az bir küçük harf içermelidir." };

        public override IdentityError PasswordRequiresUpper() =>
            new() { Code = nameof(PasswordRequiresUpper), Description = "Şifre en az bir büyük harf içermelidir." };

        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
            new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Şifre en az {uniqueChars} farklı karakter içermelidir." };
    }
}
