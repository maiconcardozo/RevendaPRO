using FluentValidation;
using RevendaPro.Global.Application.Authentication.Commands;

namespace RevendaPro.Global.Application.Authentication.Validators
{
    /// <summary>Validates the sign in input. Messages are in Portuguese: the user reads them.</summary>
    public class AuthenticateUserValidator : AbstractValidator<AuthenticateUserCommand>
    {
        public AuthenticateUserValidator()
        {
            RuleFor(c => c.Email)
                .NotEmpty().WithMessage("Informe o e-mail.")
                .EmailAddress().WithMessage("E-mail inválido.");

            RuleFor(c => c.Password)
                .NotEmpty().WithMessage("Informe a senha.");
        }
    }
}
