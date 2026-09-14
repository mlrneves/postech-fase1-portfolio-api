using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Portfolio.Infra.Persistence.Converters;

/// <summary>
/// A coluna datetime2 do SQL Server nao guarda o <c>Kind</c> do DateTime. Sem estes
/// conversores o valor lido volta como <c>Unspecified</c> e o JSON sai sem o "Z"
/// no fim, fazendo o cliente interpretar UTC como hora local.
/// </summary>
internal static class UtcDateTime
{
    public static readonly ValueConverter<DateTime, DateTime> Converter = new(
        write => write.Kind == DateTimeKind.Utc ? write : write.ToUniversalTime(),
        read => DateTime.SpecifyKind(read, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> NullableConverter = new(
        write => write.HasValue
            ? (write.Value.Kind == DateTimeKind.Utc ? write.Value : write.Value.ToUniversalTime())
            : write,
        read => read.HasValue
            ? DateTime.SpecifyKind(read.Value, DateTimeKind.Utc)
            : read);
}
