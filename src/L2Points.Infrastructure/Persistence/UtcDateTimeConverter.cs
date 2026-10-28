using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace L2Points.Infrastructure.Persistence;

/// <summary>All DateTime values are stored as UTC; values are written unchanged and read back with Kind = Utc.</summary>
public class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
