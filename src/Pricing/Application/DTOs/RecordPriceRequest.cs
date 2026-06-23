namespace TCGTrading.Pricing.Application.DTOs;

public sealed record RecordPriceRequest(
    decimal Price,
    string? Currency = null,
    string? Source = null);
