FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/Alerts/TCGTrading.Alerts.Api.csproj src/Alerts/

RUN dotnet restore src/Alerts/TCGTrading.Alerts.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/Alerts/ src/Alerts/

RUN dotnet publish src/Alerts/TCGTrading.Alerts.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.Alerts.Api.dll"]
