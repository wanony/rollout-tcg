FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/Marketplace/TCGTrading.Marketplace.Api.csproj src/Marketplace/

RUN dotnet restore src/Marketplace/TCGTrading.Marketplace.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/Marketplace/ src/Marketplace/

RUN dotnet publish src/Marketplace/TCGTrading.Marketplace.Api.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.Marketplace.Api.dll"]
