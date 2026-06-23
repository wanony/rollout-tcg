FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/Portfolio/TCGTrading.Portfolio.Api.csproj src/Portfolio/

RUN dotnet restore src/Portfolio/TCGTrading.Portfolio.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/Portfolio/ src/Portfolio/

RUN dotnet publish src/Portfolio/TCGTrading.Portfolio.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.Portfolio.Api.dll"]
