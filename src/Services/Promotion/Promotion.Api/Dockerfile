FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Services/Promotion/Promotion.Domain/Promotion.Domain.csproj", "src/Services/Promotion/Promotion.Domain/"]
COPY ["src/Services/Promotion/Promotion.Application/Promotion.Application.csproj", "src/Services/Promotion/Promotion.Application/"]
COPY ["src/Services/Promotion/Promotion.Infrastructure/Promotion.Infrastructure.csproj", "src/Services/Promotion/Promotion.Infrastructure/"]
COPY ["src/Services/Promotion/Promotion.Api/Promotion.Api.csproj", "src/Services/Promotion/Promotion.Api/"]
RUN dotnet restore "src/Services/Promotion/Promotion.Api/Promotion.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Promotion/Promotion.Api"
RUN dotnet publish "Promotion.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Promotion.Api.dll"]
