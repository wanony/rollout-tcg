FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (layer-caching)
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/IdentityService/TCGTrading.IdentityService.csproj src/IdentityService/

RUN dotnet restore src/IdentityService/TCGTrading.IdentityService.csproj

# Copy full source and publish
COPY src/SharedKernel/ src/SharedKernel/
COPY src/IdentityService/ src/IdentityService/

RUN dotnet publish src/IdentityService/TCGTrading.IdentityService.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# curl needed for Docker healthcheck — not in base image by default
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.IdentityService.dll"]
