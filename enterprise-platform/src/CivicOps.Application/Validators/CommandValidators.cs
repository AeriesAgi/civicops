using CivicOps.Application.Commands.Auth;
using CivicOps.Application.Commands.Fleet;
using CivicOps.Application.Commands.Incidents;
using CivicOps.Application.Commands.Dispatch;
using FluentValidation;

namespace CivicOps.Application.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(320);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Request.Registration)
            .NotEmpty().WithMessage("Registration is required.")
            .MaximumLength(20).WithMessage("Registration must not exceed 20 characters.")
            .Matches(@"^[A-Z0-9\-\s]+$").WithMessage("Registration contains invalid characters.");

        RuleFor(x => x.Request.Year)
            .InclusiveBetween(1990, DateTime.UtcNow.Year + 1)
            .When(x => x.Request.Year.HasValue)
            .WithMessage("Year must be between 1990 and next year.");

        RuleFor(x => x.Request.FuelCapacityL)
            .GreaterThan(0).When(x => x.Request.FuelCapacityL.HasValue)
            .WithMessage("Fuel capacity must be positive.");

        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.CreatedById).NotEmpty();
    }
}

public class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Incident title is required.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.");

        RuleFor(x => x.Request.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Request.Latitude.HasValue)
            .WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Request.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Request.Longitude.HasValue)
            .WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.CreatedById).NotEmpty();
    }
}

public class IngestGpsEventCommandValidator : AbstractValidator<IngestGpsEventCommand>
{
    public IngestGpsEventCommandValidator()
    {
        RuleFor(x => x.GpsEvent.VehicleId).NotEmpty();
        RuleFor(x => x.GpsEvent.TenantId).NotEmpty();

        RuleFor(x => x.GpsEvent.Latitude)
            .InclusiveBetween(-90m, 90m).WithMessage("Invalid latitude.");

        RuleFor(x => x.GpsEvent.Longitude)
            .InclusiveBetween(-180m, 180m).WithMessage("Invalid longitude.");

        RuleFor(x => x.GpsEvent.SpeedKmh)
            .InclusiveBetween(0m, 300m).WithMessage("Speed out of valid range.");

        RuleFor(x => x.GpsEvent.RecordedAt)
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
            .WithMessage("RecordedAt cannot be in the future.");

        RuleFor(x => x.GpsEvent.RecordedAt)
            .GreaterThan(DateTime.UtcNow.AddHours(-24))
            .WithMessage("RecordedAt is too old (>24h).");
    }
}

public class GetDispatchRecommendationCommandValidator
    : AbstractValidator<GetDispatchRecommendationCommand>
{
    public GetDispatchRecommendationCommandValidator()
    {
        RuleFor(x => x.Request.IncidentLatitude)
            .InclusiveBetween(-90m, 90m).WithMessage("Invalid latitude.");

        RuleFor(x => x.Request.IncidentLongitude)
            .InclusiveBetween(-180m, 180m).WithMessage("Invalid longitude.");

        RuleFor(x => x.Request.MaxResults)
            .InclusiveBetween(1, 10).WithMessage("MaxResults must be between 1 and 10.");
    }
}

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.Request.VehicleId).NotEmpty().WithMessage("Vehicle ID is required.");
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DispatcherId).NotEmpty();
    }
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain a special character.");

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must differ from current password.");
    }
}
