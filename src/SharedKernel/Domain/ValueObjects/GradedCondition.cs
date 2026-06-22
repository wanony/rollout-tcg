namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public record GradedCondition(
    string GradingService,
    decimal Grade,
    string CertificationNumber) : CardPhysicalCondition
{
    public string Display => $"{GradingService} {Grade}";
}
