using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KRT.Onboarding.Infrastructure.Persistence;

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
