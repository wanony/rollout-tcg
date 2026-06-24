FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/Pricing/TCGTrading.Pricing.Api.csproj src/Pricing/

RUN dotnet restore src/Pricing/TCGTrading.Pricing.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/Pricing/ src/Pricing/

RUN dotnet publish src/Pricing/TCGTrading.Pricing.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.Pricing.Api.dll"]
