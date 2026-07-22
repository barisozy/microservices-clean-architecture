FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Services/Payments/Payments.Domain/Payments.Domain.csproj", "src/Services/Payments/Payments.Domain/"]
COPY ["src/Services/Payments/Payments.Application/Payments.Application.csproj", "src/Services/Payments/Payments.Application/"]
COPY ["src/Services/Payments/Payments.Infrastructure/Payments.Infrastructure.csproj", "src/Services/Payments/Payments.Infrastructure/"]
COPY ["src/Services/Payments/Payments.Api/Payments.Api.csproj", "src/Services/Payments/Payments.Api/"]

RUN dotnet restore "src/Services/Payments/Payments.Api/Payments.Api.csproj"

COPY . .
WORKDIR "/src/src/Services/Payments/Payments.Api"
RUN dotnet build "Payments.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Payments.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Payments.Api.dll"]
