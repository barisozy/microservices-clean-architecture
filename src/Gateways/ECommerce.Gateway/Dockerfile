FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Gateways/ECommerce.Gateway/ECommerce.Gateway.csproj", "src/Gateways/ECommerce.Gateway/"]

RUN dotnet restore "src/Gateways/ECommerce.Gateway/ECommerce.Gateway.csproj"

COPY . .
WORKDIR "/src/src/Gateways/ECommerce.Gateway"
RUN dotnet build "ECommerce.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ECommerce.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Gateway.dll"]
