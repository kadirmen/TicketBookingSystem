using FluentValidation;
using AuthServiceAPI.Dtos;
using System.Text.RegularExpressions;

namespace AuthServiceAPI.Validators
{
    public class UserRegisterDtoValidator : AbstractValidator<UserRegisterDto>
    {
        public UserRegisterDtoValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Kullanıcı adı boş olamaz.")
                .MinimumLength(3).WithMessage("Kullanıcı adı en az 3 karakter olmalıdır.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Şifre boş olamaz.")
                .MinimumLength(6).WithMessage("Şifre en az 6 karakter uzunluğunda olmalıdır.")
                .Must(ContainNumber).WithMessage("Şifre en az bir rakam içermelidir.")
                .Must(ContainUpperCase).WithMessage("Şifre en az bir büyük harf içermelidir.");
        }

        private bool ContainNumber(string password)
        {
            return Regex.IsMatch(password, @"\d");
        }

        private bool ContainUpperCase(string password)
        {
            return Regex.IsMatch(password, @"[A-Z]");
        }
    }
}
