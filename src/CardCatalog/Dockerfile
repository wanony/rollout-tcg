FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/CardCatalog/TCGTrading.CardCatalog.Api.csproj src/CardCatalog/

RUN dotnet restore src/CardCatalog/TCGTrading.CardCatalog.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/CardCatalog/ src/CardCatalog/

RUN dotnet publish src/CardCatalog/TCGTrading.CardCatalog.Api.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.CardCatalog.Api.dll"]
