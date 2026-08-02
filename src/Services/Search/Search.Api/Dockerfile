FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Services/Search/Search.Domain/Search.Domain.csproj", "src/Services/Search/Search.Domain/"]
COPY ["src/Services/Search/Search.Application/Search.Application.csproj", "src/Services/Search/Search.Application/"]
COPY ["src/Services/Search/Search.Infrastructure/Search.Infrastructure.csproj", "src/Services/Search/Search.Infrastructure/"]
COPY ["src/Services/Search/Search.Api/Search.Api.csproj", "src/Services/Search/Search.Api/"]
RUN dotnet restore "src/Services/Search/Search.Api/Search.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Search/Search.Api"
RUN dotnet publish "Search.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Search.Api.dll"]
