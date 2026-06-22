# TCG Trading Platform — Plan 1: Foundation & Shared Kernel

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish solution structure, shared domain primitives, integration event contracts, outbox base, and Docker Compose infrastructure that all subsequent service plans depend on.

**Architecture:** A .NET 8 solution with 7 stub service projects plus a SharedKernel class library. SharedKernel provides domain base classes (AggregateRoot, Entity, Result<T>), all integration event contracts, and the outbox pattern base. Docker Compose wires Postgres (6 databases), RabbitMQ, and Redis for local dev. No service business logic ships in this plan — only the shared foundation.

**Tech Stack:** .NET 8, C# 12, MassTransit 8 (RabbitMQ transport), xUnit 2, NSubstitute 5, FluentAssertions 6, Docker Compose

## Global Constraints

- Target framework: `net8.0`
- C# language version: `12`
- All projects: `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- No business logic in SharedKernel — primitives and contracts only
- MassTransit is the message bus — do not wrap in a custom interface (it IS the abstraction; Azure migration = swap transport registration only)
- All integration event records are immutable (positional record syntax, init-only)
- `Result<T>` never throws for business failures — discriminated success/failure only
- Central package management via `Directory.Packages.props` — no version attributes in individual `.csproj` files

---

### Task 1: Solution & Project Scaffolding

**Files:**
- Create: `TCGTrading.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `src/SharedKernel/TCGTrading.SharedKernel.csproj`
- Create: `src/IdentityService/TCGTrading.IdentityService.csproj` (stub)
- Create: `src/CardCatalog/TCGTrading.CardCatalog.Api.csproj` (stub)
- Create: `src/Portfolio/TCGTrading.Portfolio.Api.csproj` (stub)
- Create: `src/Marketplace/TCGTrading.Marketplace.Api.csproj` (stub)
- Create: `src/Pricing/TCGTrading.Pricing.Api.csproj` (stub)
- Create: `src/Notification/TCGTrading.Notification.Api.csproj` (stub)
- Create: `src/ApiGateway/TCGTrading.ApiGateway.csproj` (stub)
- Create: `tests/SharedKernel.Tests/TCGTrading.SharedKernel.Tests.csproj`

**Interfaces:**
- Produces: compilable solution — `dotnet build TCGTrading.sln` succeeds with 0 errors

- [ ] **Step 1: Create directory structure**

```bash
cd /Users/cameronshand/Documents/GitHub/dotnetproj2
dotnet new sln -n TCGTrading
mkdir -p src/SharedKernel src/IdentityService src/CardCatalog src/Portfolio
mkdir -p src/Marketplace src/Pricing src/Notification src/ApiGateway
mkdir -p tests/SharedKernel.Tests
mkdir -p infra/postgres/init infra/rabbitmq
```

- [ ] **Step 2: Create Directory.Build.props**

Create `Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create Directory.Packages.props**

Create `Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MassTransit" Version="8.3.0" />
    <PackageVersion Include="MassTransit.RabbitMQ" Version="8.3.0" />
    <PackageVersion Include="MassTransit.AzureServiceBus" Version="8.3.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageVersion Include="MediatR" Version="12.4.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageVersion Include="xunit" Version="2.9.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="NSubstitute" Version="5.1.0" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="3.10.0" />
    <PackageVersion Include="Testcontainers.RabbitMq" Version="3.10.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create SharedKernel project**

```bash
dotnet new classlib -n TCGTrading.SharedKernel -o src/SharedKernel --framework net8.0
rm src/SharedKernel/Class1.cs
```

Replace `src/SharedKernel/TCGTrading.SharedKernel.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MediatR" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create stub service projects**

```bash
dotnet new web -n TCGTrading.CardCatalog.Api -o src/CardCatalog --framework net8.0
dotnet new web -n TCGTrading.Portfolio.Api -o src/Portfolio --framework net8.0
dotnet new web -n TCGTrading.Marketplace.Api -o src/Marketplace --framework net8.0
dotnet new web -n TCGTrading.Pricing.Api -o src/Pricing --framework net8.0
dotnet new web -n TCGTrading.Notification.Api -o src/Notification --framework net8.0
dotnet new web -n TCGTrading.ApiGateway -o src/ApiGateway --framework net8.0
dotnet new web -n TCGTrading.IdentityService -o src/IdentityService --framework net8.0
```

- [ ] **Step 6: Create test project**

```bash
dotnet new xunit -n TCGTrading.SharedKernel.Tests -o tests/SharedKernel.Tests --framework net8.0
rm tests/SharedKernel.Tests/UnitTest1.cs
```

Replace `tests/SharedKernel.Tests/TCGTrading.SharedKernel.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="FluentAssertions" />
    <ProjectReference Include="../../src/SharedKernel/TCGTrading.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: Add all projects to solution**

```bash
dotnet sln TCGTrading.sln add src/SharedKernel/TCGTrading.SharedKernel.csproj
dotnet sln TCGTrading.sln add src/CardCatalog/TCGTrading.CardCatalog.Api.csproj
dotnet sln TCGTrading.sln add src/Portfolio/TCGTrading.Portfolio.Api.csproj
dotnet sln TCGTrading.sln add src/Marketplace/TCGTrading.Marketplace.Api.csproj
dotnet sln TCGTrading.sln add src/Pricing/TCGTrading.Pricing.Api.csproj
dotnet sln TCGTrading.sln add src/Notification/TCGTrading.Notification.Api.csproj
dotnet sln TCGTrading.sln add src/ApiGateway/TCGTrading.ApiGateway.csproj
dotnet sln TCGTrading.sln add src/IdentityService/TCGTrading.IdentityService.csproj
dotnet sln TCGTrading.sln add tests/SharedKernel.Tests/TCGTrading.SharedKernel.Tests.csproj
```

- [ ] **Step 8: Verify build**

```bash
dotnet build TCGTrading.sln
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Initialise git and commit**

```bash
git init
git add .
git commit -m "chore: scaffold solution with all service stubs and shared kernel"
```

---

### Task 2: SharedKernel — Domain Primitives

**Files:**
- Create: `src/SharedKernel/Domain/IDomainEvent.cs`
- Create: `src/SharedKernel/Domain/Entity.cs`
- Create: `src/SharedKernel/Domain/AggregateRoot.cs`
- Create: `src/SharedKernel/Application/Result.cs`
- Test: `tests/SharedKernel.Tests/Domain/AggregateRootTests.cs`
- Test: `tests/SharedKernel.Tests/Application/ResultTests.cs`

**Interfaces:**
- Produces:
  - `AggregateRoot` — `AddDomainEvent(IDomainEvent)`, `PopDomainEvents() → IReadOnlyList<IDomainEvent>`, `DomainEvents`
  - `Entity` — `Guid Id`, `DateTime CreatedAt`
  - `Result<T>` — `Result<T>.Success(T)`, `Result<T>.Failure(string)`, `.IsSuccess`, `.Value`, `.Error`

- [ ] **Step 1: Write failing tests**

Create `tests/SharedKernel.Tests/Domain/AggregateRootTests.cs`:
```csharp
using FluentAssertions;
using TCGTrading.SharedKernel.Domain;

namespace TCGTrading.SharedKernel.Tests.Domain;

public class AggregateRootTests
{
    private class TestAggregate : AggregateRoot
    {
        public void RaiseEvent(IDomainEvent @event) => AddDomainEvent(@event);
    }

    private record TestEvent(string Data) : IDomainEvent;

    [Fact]
    public void AddDomainEvent_StoresEvent()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseEvent(new TestEvent("test"));
        aggregate.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TestEvent>();
    }

    [Fact]
    public void PopDomainEvents_ReturnsThenClearsEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseEvent(new TestEvent("a"));
        aggregate.RaiseEvent(new TestEvent("b"));

        var popped = aggregate.PopDomainEvents();

        popped.Should().HaveCount(2);
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void NewAggregate_HasNoEvents()
    {
        var aggregate = new TestAggregate();
        aggregate.DomainEvents.Should().BeEmpty();
    }
}
```

Create `tests/SharedKernel.Tests/Application/ResultTests.cs`:
```csharp
using FluentAssertions;
using TCGTrading.SharedKernel.Application;

namespace TCGTrading.SharedKernel.Tests.Application;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_True_And_Value_Accessible()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_IsSuccess_False_And_Error_Accessible()
    {
        var result = Result<int>.Failure("something went wrong");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var result = Result<int>.Failure("error");
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Success_AccessingError_Throws()
    {
        var result = Result<int>.Success(1);
        var act = () => _ = result.Error;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsFailure_IsOppositeOfIsSuccess()
    {
        Result<string>.Success("x").IsFailure.Should().BeFalse();
        Result<string>.Failure("e").IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/SharedKernel.Tests/
```
Expected: FAIL — types not yet defined.

- [ ] **Step 3: Implement domain primitives**

Create `src/SharedKernel/Domain/IDomainEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain;

public interface IDomainEvent { }
```

Create `src/SharedKernel/Domain/Entity.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected init; } = DateTime.UtcNow;
}
```

Create `src/SharedKernel/Domain/AggregateRoot.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public IReadOnlyList<IDomainEvent> PopDomainEvents()
    {
        var events = _domainEvents.ToList().AsReadOnly();
        _domainEvents.Clear();
        return events;
    }
}
```

Create `src/SharedKernel/Application/Result.cs`:
```csharp
namespace TCGTrading.SharedKernel.Application;

public sealed class Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private Result(string error)
    {
        IsSuccess = false;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    public string Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error of a successful result.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/SharedKernel.Tests/
```
Expected: PASS — 8 tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/SharedKernel/Domain/ src/SharedKernel/Application/ tests/SharedKernel.Tests/
git commit -m "feat: add domain primitives — AggregateRoot, Entity, Result<T>"
```

---

### Task 3: SharedKernel — Value Objects

**Files:**
- Create: `src/SharedKernel/Domain/Exceptions/CurrencyMismatchException.cs`
- Create: `src/SharedKernel/Domain/ValueObjects/Money.cs`
- Create: `src/SharedKernel/Domain/ValueObjects/UngradedGrade.cs`
- Create: `src/SharedKernel/Domain/ValueObjects/CardPhysicalCondition.cs`
- Create: `src/SharedKernel/Domain/ValueObjects/UngradedCondition.cs`
- Create: `src/SharedKernel/Domain/ValueObjects/GradedCondition.cs`
- Test: `tests/SharedKernel.Tests/Domain/MoneyTests.cs`
- Test: `tests/SharedKernel.Tests/Domain/CardConditionTests.cs`

**Interfaces:**
- Produces:
  - `Money(decimal Amount, string Currency = "USD")` — `.Add(Money)`, `.Subtract(Money)`
  - `CardPhysicalCondition` — abstract record base
  - `UngradedCondition(UngradedGrade Grade) : CardPhysicalCondition`
  - `GradedCondition(string GradingService, decimal Grade, string CertificationNumber) : CardPhysicalCondition` — `.Display → string`
  - `UngradedGrade` enum: `NearMint, LightlyPlayed, ModeratelyPlayed, HeavilyPlayed, Damaged`

- [ ] **Step 1: Write failing tests**

Create `tests/SharedKernel.Tests/Domain/MoneyTests.cs`:
```csharp
using FluentAssertions;
using TCGTrading.SharedKernel.Domain.Exceptions;
using TCGTrading.SharedKernel.Domain.ValueObjects;

namespace TCGTrading.SharedKernel.Tests.Domain;

public class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(5.50m, "USD");
        a.Add(b).Should().Be(new Money(15.50m, "USD"));
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "GBP");
        var act = () => a.Add(b);
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Subtract_SameCurrency_ReturnsDifference()
    {
        var a = new Money(10m, "USD");
        var b = new Money(3m, "USD");
        a.Subtract(b).Should().Be(new Money(7m, "USD"));
    }

    [Fact]
    public void Subtract_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "USD");
        var b = new Money(3m, "GBP");
        var act = () => a.Subtract(b);
        act.Should().Throw<CurrencyMismatchException>();
    }
}
```

Create `tests/SharedKernel.Tests/Domain/CardConditionTests.cs`:
```csharp
using FluentAssertions;
using TCGTrading.SharedKernel.Domain.ValueObjects;

namespace TCGTrading.SharedKernel.Tests.Domain;

public class CardConditionTests
{
    [Fact]
    public void GradedCondition_Display_FormatsWholeGrade()
    {
        var condition = new GradedCondition("PSA", 10m, "12345678");
        condition.Display.Should().Be("PSA 10");
    }

    [Fact]
    public void GradedCondition_Display_FormatsDecimalGrade()
    {
        var condition = new GradedCondition("BGS", 9.5m, "87654321");
        condition.Display.Should().Be("BGS 9.5");
    }

    [Fact]
    public void UngradedCondition_HoldsGrade()
    {
        var condition = new UngradedCondition(UngradedGrade.NearMint);
        condition.Grade.Should().Be(UngradedGrade.NearMint);
    }

    [Fact]
    public void GradedCondition_IsCardPhysicalCondition()
    {
        CardPhysicalCondition condition = new GradedCondition("CGC", 9m, "ABC123");
        condition.Should().BeAssignableTo<CardPhysicalCondition>();
    }

    [Fact]
    public void UngradedCondition_IsCardPhysicalCondition()
    {
        CardPhysicalCondition condition = new UngradedCondition(UngradedGrade.HeavilyPlayed);
        condition.Should().BeAssignableTo<CardPhysicalCondition>();
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "MoneyTests|CardConditionTests"
```
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement value objects**

Create `src/SharedKernel/Domain/Exceptions/CurrencyMismatchException.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain.Exceptions;

public sealed class CurrencyMismatchException()
    : InvalidOperationException("Cannot perform arithmetic on money with different currencies.");
```

Create `src/SharedKernel/Domain/ValueObjects/Money.cs`:
```csharp
using TCGTrading.SharedKernel.Domain.Exceptions;

namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public record Money(decimal Amount, string Currency = "USD")
{
    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new CurrencyMismatchException();
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency) throw new CurrencyMismatchException();
        return this with { Amount = Amount - other.Amount };
    }
}
```

Create `src/SharedKernel/Domain/ValueObjects/UngradedGrade.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public enum UngradedGrade
{
    NearMint,
    LightlyPlayed,
    ModeratelyPlayed,
    HeavilyPlayed,
    Damaged
}
```

Create `src/SharedKernel/Domain/ValueObjects/CardPhysicalCondition.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public abstract record CardPhysicalCondition;
```

Create `src/SharedKernel/Domain/ValueObjects/UngradedCondition.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public record UngradedCondition(UngradedGrade Grade) : CardPhysicalCondition;
```

Create `src/SharedKernel/Domain/ValueObjects/GradedCondition.cs`:
```csharp
namespace TCGTrading.SharedKernel.Domain.ValueObjects;

public record GradedCondition(
    string GradingService,
    decimal Grade,
    string CertificationNumber) : CardPhysicalCondition
{
    public string Display => $"{GradingService} {Grade}";
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "MoneyTests|CardConditionTests"
```
Expected: PASS — 9 tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/SharedKernel/Domain/ tests/SharedKernel.Tests/Domain/
git commit -m "feat: add value objects — Money, CardPhysicalCondition, GradedCondition"
```

---

### Task 4: SharedKernel — Integration Event Contracts

**Files:**
- Create: `src/SharedKernel/Contracts/IntegrationEvent.cs`
- Create: `src/SharedKernel/Contracts/CardCatalogSyncedEvent.cs`
- Create: `src/SharedKernel/Contracts/CardPriceUpdatedEvent.cs`
- Create: `src/SharedKernel/Contracts/ListingCreatedEvent.cs`
- Create: `src/SharedKernel/Contracts/ListingPurchasedEvent.cs`
- Create: `src/SharedKernel/Contracts/OfferMadeEvent.cs`
- Create: `src/SharedKernel/Contracts/OfferAcceptedEvent.cs`
- Test: `tests/SharedKernel.Tests/Contracts/IntegrationEventTests.cs`

**Interfaces:**
- Produces: all event record types consumed by every service via MassTransit `IConsumer<T>` and published via `IPublishEndpoint`

- [ ] **Step 1: Write failing tests**

Create `tests/SharedKernel.Tests/Contracts/IntegrationEventTests.cs`:
```csharp
using FluentAssertions;
using TCGTrading.SharedKernel.Contracts;

namespace TCGTrading.SharedKernel.Tests.Contracts;

public class IntegrationEventTests
{
    [Fact]
    public void CardPriceUpdatedEvent_PropertiesAccessible()
    {
        var id = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var @event = new CardPriceUpdatedEvent(
            EventId: id,
            OccurredAt: DateTime.UtcNow,
            CardId: cardId,
            ProviderCardId: "xy1-1",
            TcgProvider: "Pokemon",
            MarketPriceUsd: 25.50m,
            LowPriceUsd: 20.00m,
            HighPriceUsd: 30.00m);

        @event.EventId.Should().Be(id);
        @event.CardId.Should().Be(cardId);
        @event.MarketPriceUsd.Should().Be(25.50m);
        @event.TcgProvider.Should().Be("Pokemon");
    }

    [Fact]
    public void ListingPurchasedEvent_PropertiesAccessible()
    {
        var @event = new ListingPurchasedEvent(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ListingId: Guid.NewGuid(),
            CardId: Guid.NewGuid(),
            BuyerId: Guid.NewGuid(),
            SellerId: Guid.NewGuid(),
            PurchasePriceUsd: 45.00m);

        @event.PurchasePriceUsd.Should().Be(45.00m);
    }

    [Fact]
    public void AllEvents_InheritIntegrationEvent()
    {
        var events = new IntegrationEvent[]
        {
            new CardPriceUpdatedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "x", "Pokemon", 1m, 1m, 1m),
            new ListingCreatedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "Pikachu", Guid.NewGuid(), 10m, "NearMint"),
            new ListingPurchasedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m),
            new OfferMadeEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 8m),
            new OfferAcceptedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 8m),
        };

        events.Should().AllBeAssignableTo<IntegrationEvent>();
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "IntegrationEventTests"
```
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement event contracts**

Create `src/SharedKernel/Contracts/IntegrationEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public abstract record IntegrationEvent(Guid EventId, DateTime OccurredAt);
```

Create `src/SharedKernel/Contracts/CardCatalogSyncedEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public record CardCatalogSyncedEvent(
    Guid EventId,
    DateTime OccurredAt,
    string TcgProvider,
    IReadOnlyList<string> ProviderCardIds) : IntegrationEvent(EventId, OccurredAt);
```

Create `src/SharedKernel/Contracts/CardPriceUpdatedEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public record CardPriceUpdatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid CardId,
    string ProviderCardId,
    string TcgProvider,
    decimal MarketPriceUsd,
    decimal LowPriceUsd,
    decimal HighPriceUsd) : IntegrationEvent(EventId, OccurredAt);
```

Create `src/SharedKernel/Contracts/ListingCreatedEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public record ListingCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ListingId,
    Guid CardId,
    string CardName,
    Guid SellerId,
    decimal AskingPriceUsd,
    string Condition) : IntegrationEvent(EventId, OccurredAt);
```

Create `src/SharedKernel/Contracts/ListingPurchasedEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public record ListingPurchasedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ListingId,
    Guid CardId,
    Guid BuyerId,
    Guid SellerId,
    decimal PurchasePriceUsd) : IntegrationEvent(EventId, OccurredAt);
```

Create `src/SharedKernel/Contracts/OfferMadeEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public record OfferMadeEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OfferId,
    Guid ListingId,
    Guid CardId,
    Guid BuyerId,
    Guid SellerId,
    decimal OfferedPriceUsd) : IntegrationEvent(EventId, OccurredAt);
```

Create `src/SharedKernel/Contracts/OfferAcceptedEvent.cs`:
```csharp
namespace TCGTrading.SharedKernel.Contracts;

public record OfferAcceptedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OfferId,
    Guid ListingId,
    Guid CardId,
    Guid BuyerId,
    Guid SellerId,
    decimal FinalPriceUsd) : IntegrationEvent(EventId, OccurredAt);
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "IntegrationEventTests"
```
Expected: PASS — 3 tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/SharedKernel/Contracts/ tests/SharedKernel.Tests/Contracts/
git commit -m "feat: add integration event contracts for all cross-service events"
```

---

### Task 5: SharedKernel — Outbox Pattern Base

**Files:**
- Create: `src/SharedKernel/Infrastructure/Outbox/OutboxMessage.cs`
- Create: `src/SharedKernel/Infrastructure/Outbox/IOutboxRepository.cs`
- Create: `src/SharedKernel/Infrastructure/Outbox/OutboxWorkerBase.cs`
- Test: `tests/SharedKernel.Tests/Infrastructure/OutboxMessageTests.cs`

**Interfaces:**
- Produces:
  - `OutboxMessage` — `Id`, `EventType`, `Payload`, `CreatedAt`, `ProcessedAt?`
  - `IOutboxRepository` — `GetUnprocessedAsync(int batchSize, CancellationToken)`, `MarkProcessedAsync(Guid, CancellationToken)`, `SaveAsync(OutboxMessage, CancellationToken)`
  - `OutboxWorkerBase : BackgroundService` — abstract; subclasses override `ResolveEventType(string) → Type?`; polls every 5s, publishes via MassTransit `IPublishEndpoint`, marks processed

- [ ] **Step 1: Write failing tests**

Create `tests/SharedKernel.Tests/Infrastructure/OutboxMessageTests.cs`:
```csharp
using FluentAssertions;
using TCGTrading.SharedKernel.Infrastructure.Outbox;

namespace TCGTrading.SharedKernel.Tests.Infrastructure;

public class OutboxMessageTests
{
    [Fact]
    public void OutboxMessage_DefaultProcessedAt_IsNull()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "CardPriceUpdatedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow
        };

        message.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void OutboxMessage_CanSetProcessedAt()
    {
        var now = DateTime.UtcNow;
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "CardPriceUpdatedEvent",
            Payload = "{}",
            CreatedAt = now,
            ProcessedAt = now
        };

        message.ProcessedAt.Should().Be(now);
    }

    [Fact]
    public void OutboxMessage_IsUnprocessed_WhenProcessedAtNull()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "TestEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow
        };

        message.ProcessedAt.HasValue.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "OutboxMessageTests"
```
Expected: FAIL — type not defined.

- [ ] **Step 3: Implement outbox types**

Create `src/SharedKernel/Infrastructure/Outbox/OutboxMessage.cs`:
```csharp
namespace TCGTrading.SharedKernel.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
}
```

Create `src/SharedKernel/Infrastructure/Outbox/IOutboxRepository.cs`:
```csharp
namespace TCGTrading.SharedKernel.Infrastructure.Outbox;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct);
    Task MarkProcessedAsync(Guid messageId, CancellationToken ct);
    Task SaveAsync(OutboxMessage message, CancellationToken ct);
}
```

Create `src/SharedKernel/Infrastructure/Outbox/OutboxWorkerBase.cs`:
```csharp
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TCGTrading.SharedKernel.Infrastructure.Outbox;

public abstract class OutboxWorkerBase(
    IOutboxRepository outboxRepository,
    IPublishEndpoint publishEndpoint,
    ILogger logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await outboxRepository.GetUnprocessedAsync(BatchSize, stoppingToken);

            foreach (var message in messages)
            {
                try
                {
                    var type = ResolveEventType(message.EventType);
                    if (type is null)
                    {
                        logger.LogWarning("Unknown outbox event type: {EventType}", message.EventType);
                        continue;
                    }

                    var @event = JsonSerializer.Deserialize(message.Payload, type);
                    if (@event is not null)
                        await publishEndpoint.Publish(@event, type, stoppingToken);

                    await outboxRepository.MarkProcessedAsync(message.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                }
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    // Each service overrides to map event type name → CLR type
    // Return null for unknown types (worker logs warning and skips)
    protected abstract Type? ResolveEventType(string eventType);
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/SharedKernel.Tests/ --filter "OutboxMessageTests"
```
Expected: PASS — 3 tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/SharedKernel/Infrastructure/ tests/SharedKernel.Tests/Infrastructure/
git commit -m "feat: add outbox pattern base (OutboxMessage, IOutboxRepository, OutboxWorkerBase)"
```

---

### Task 6: Docker Compose Infrastructure

**Files:**
- Create: `docker-compose.yml`
- Create: `docker-compose.override.yml`
- Create: `infra/postgres/init/01-create-databases.sql`
- Create: `.gitignore`

**Interfaces:**
- Produces: `docker compose up -d postgres rabbitmq redis` → all three containers healthy; 5 Postgres databases exist

- [ ] **Step 1: Create .gitignore**

Create `.gitignore`:
```
bin/
obj/
.vs/
*.user
.env
.env.local
docker-compose.override.local.yml
```

- [ ] **Step 2: Create Postgres init SQL**

Create `infra/postgres/init/01-create-databases.sql`:
```sql
CREATE DATABASE db_identity;
CREATE DATABASE db_cards;
CREATE DATABASE db_portfolio;
CREATE DATABASE db_marketplace;
CREATE DATABASE db_pricing;
```

- [ ] **Step 3: Create docker-compose.yml**

Create `docker-compose.yml`:
```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: tcg
      POSTGRES_PASSWORD: dev
      POSTGRES_DB: postgres
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infra/postgres/init:/docker-entrypoint-initdb.d
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tcg"]
      interval: 5s
      timeout: 5s
      retries: 5

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: tcg
      RABBITMQ_DEFAULT_PASS: dev
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

volumes:
  postgres_data:
  rabbitmq_data:
```

- [ ] **Step 4: Create docker-compose.override.yml**

Create `docker-compose.override.yml`:
```yaml
# Dev-only overrides — not committed with secrets in real projects
services:
  postgres:
    environment:
      POSTGRES_PASSWORD: dev
  rabbitmq:
    environment:
      RABBITMQ_DEFAULT_PASS: dev
```

- [ ] **Step 5: Start infrastructure and verify**

```bash
docker compose up -d postgres rabbitmq redis
```

Wait ~15 seconds then check health:
```bash
docker compose ps
```
Expected: All three containers show `healthy`.

```bash
docker compose exec postgres psql -U tcg -l
```
Expected: Output lists `db_identity`, `db_cards`, `db_portfolio`, `db_marketplace`, `db_pricing`.

```bash
docker compose exec rabbitmq rabbitmq-diagnostics ping
```
Expected: `Ping succeeded`

```bash
docker compose exec redis redis-cli ping
```
Expected: `PONG`

- [ ] **Step 6: Commit**

```bash
git add docker-compose.yml docker-compose.override.yml infra/ .gitignore
git commit -m "chore: add Docker Compose infrastructure — Postgres, RabbitMQ, Redis"
```

---

### Task 7: Full Build & Test Verification

**Interfaces:**
- Consumes: all prior tasks
- Produces: green solution build + all tests pass — Plan 1 complete, ready for Plan 2 (Identity Service)

- [ ] **Step 1: Run full test suite**

```bash
dotnet test TCGTrading.sln --verbosity normal
```
Expected: All tests pass. Should see tests from:
- `AggregateRootTests` (3 tests)
- `ResultTests` (5 tests)
- `MoneyTests` (4 tests)
- `CardConditionTests` (5 tests)
- `IntegrationEventTests` (3 tests)
- `OutboxMessageTests` (3 tests)

Total: 23 passing tests.

- [ ] **Step 2: Verify Release build**

```bash
dotnet build TCGTrading.sln -c Release
```
Expected: `Build succeeded. 0 Error(s) 0 Warning(s)`

- [ ] **Step 3: Verify Docker still healthy**

```bash
docker compose ps
```
Expected: postgres, rabbitmq, redis all `healthy`.

- [ ] **Step 4: Final commit**

```bash
git add .
git commit -m "chore: plan 1 complete — foundation and shared kernel ready for service plans"
```

---

## Plan Sequence

This is Plan 1 of 7. Subsequent plans build on this foundation:

| Plan | Scope | Depends on |
|---|---|---|
| **Plan 1** (this) | Foundation, SharedKernel, Docker Compose | — |
| **Plan 2** | Identity Service (Duende IdentityServer, OAuth2/OIDC) | Plan 1 |
| **Plan 3** | Card Catalog Service (ITcgProvider, Pokemon TCG API) | Plan 1 |
| **Plan 4** | Portfolio Service (holdings, grading, manual entry, P&L) | Plans 1, 3 |
| **Plan 5** | Pricing Service (scheduled polling, CardPriceUpdated events) | Plans 1, 3 |
| **Plan 6** | Marketplace Service (listings, offers, purchases) | Plans 1, 3, 4 |
| **Plan 7** | Notification Service + API Gateway (SignalR, YARP) | Plans 1–6 |
