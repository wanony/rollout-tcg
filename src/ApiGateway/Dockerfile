FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/SharedKernel/TCGTrading.SharedKernel.csproj src/SharedKernel/
COPY src/ApiGateway/TCGTrading.ApiGateway.csproj src/ApiGateway/

RUN dotnet restore src/ApiGateway/TCGTrading.ApiGateway.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/ApiGateway/ src/ApiGateway/

RUN dotnet publish src/ApiGateway/TCGTrading.ApiGateway.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TCGTrading.ApiGateway.dll"]
