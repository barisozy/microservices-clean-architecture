FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Services/Auditing/Auditing.Api/Auditing.Api.csproj", "src/Services/Auditing/Auditing.Api/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
RUN dotnet restore "./src/Services/Auditing/Auditing.Api/Auditing.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Auditing/Auditing.Api"
RUN dotnet build "./Auditing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Auditing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Auditing.Api.dll"]
