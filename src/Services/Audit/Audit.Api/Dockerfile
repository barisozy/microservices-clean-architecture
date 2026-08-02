FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Services/Audit/Audit.Api/Audit.Api.csproj", "src/Services/Audit/Audit.Api/"]
COPY ["src/Services/Audit/Audit.Application/Audit.Application.csproj", "src/Services/Audit/Audit.Application/"]
COPY ["src/Services/Audit/Audit.Domain/Audit.Domain.csproj", "src/Services/Audit/Audit.Domain/"]
COPY ["src/Services/Audit/Audit.Infrastructure/Audit.Infrastructure.csproj", "src/Services/Audit/Audit.Infrastructure/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
RUN dotnet restore "./src/Services/Audit/Audit.Api/Audit.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Audit/Audit.Api"
RUN dotnet build "./Audit.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Audit.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Audit.Api.dll"]

