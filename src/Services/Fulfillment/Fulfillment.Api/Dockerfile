FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Services/Fulfillment/Fulfillment.Domain/Fulfillment.Domain.csproj", "src/Services/Fulfillment/Fulfillment.Domain/"]
COPY ["src/Services/Fulfillment/Fulfillment.Application/Fulfillment.Application.csproj", "src/Services/Fulfillment/Fulfillment.Application/"]
COPY ["src/Services/Fulfillment/Fulfillment.Infrastructure/Fulfillment.Infrastructure.csproj", "src/Services/Fulfillment/Fulfillment.Infrastructure/"]
COPY ["src/Services/Fulfillment/Fulfillment.Api/Fulfillment.Api.csproj", "src/Services/Fulfillment/Fulfillment.Api/"]

RUN dotnet restore "src/Services/Fulfillment/Fulfillment.Api/Fulfillment.Api.csproj"

COPY . .
WORKDIR "/src/src/Services/Fulfillment/Fulfillment.Api"
RUN dotnet build "Fulfillment.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Fulfillment.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fulfillment.Api.dll"]
