FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/Notification/TCGTrading.Notification.Api.csproj src/Notification/

RUN dotnet restore src/Notification/TCGTrading.Notification.Api.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/Notification/ src/Notification/

RUN dotnet publish src/Notification/TCGTrading.Notification.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.Notification.Api.dll"]
