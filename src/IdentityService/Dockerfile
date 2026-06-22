FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Layer 1: Central build config — changes rarely, cached longest
COPY Directory.Build.props Directory.Packages.props ./

# Layer 2: Project files only — restore cached until deps change
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/IdentityService/TCGTrading.IdentityService.csproj src/IdentityService/

RUN dotnet restore src/IdentityService/TCGTrading.IdentityService.csproj

# Layer 3: Full source — invalidates restore cache only on source change
COPY src/SharedKernel/ src/SharedKernel/
COPY src/IdentityService/ src/IdentityService/

RUN dotnet publish src/IdentityService/TCGTrading.IdentityService.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# curl required for Docker healthcheck in docker-compose.yml
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.IdentityService.dll"]
