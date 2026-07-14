using FluentValidation;
using Habitrack.Api.Dtos;

namespace Habitrack.Api.Validators;

public class CreateHabitRequestValidator : AbstractValidator<CreateHabitRequest>
{
    public CreateHabitRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório")
            .MinimumLength(3).WithMessage("Nome deve ter pelo menos 3 caracteres")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description is not null);

        RuleFor(x => x.WeeklyFrequency)
            .InclusiveBetween(1, 7)
            .WithMessage("Frequência semanal deve ser entre 1 e 7");

        RuleFor(x => x.Color)
            .Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
            .When(x => x.Color is not null)
            .WithMessage("Cor deve ser um hexadecimal válido (#RRGGBB)");

        RuleFor(x => x)
            .Must(x => x.WeeklyFrequency <= 3 || !string.IsNullOrEmpty(x.Description))
            .WithMessage("Hábitos com frequência > 3 exigem descrição");
    }
}

public class UpdateHabitRequestValidator : AbstractValidator<UpdateHabitRequest>
{
    public UpdateHabitRequestValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(3).When(x => x.Name is not null)
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description is not null);

        RuleFor(x => x.WeeklyFrequency)
            .InclusiveBetween(1, 7).When(x => x.WeeklyFrequency.HasValue);

        RuleFor(x => x.Color)
            .Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
            .When(x => x.Color is not null);
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(6).MaximumLength(100);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
