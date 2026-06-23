namespace TCGTrading.Portfolio.Application.DTOs;

public record PortfolioSummaryResponse(
    int TotalItems,
    int TotalCards,
    decimal TotalAcquisitionCostUsd);
