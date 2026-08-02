FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Services/Customer/Customer.Domain/Customer.Domain.csproj", "src/Services/Customer/Customer.Domain/"]
COPY ["src/Services/Customer/Customer.Application/Customer.Application.csproj", "src/Services/Customer/Customer.Application/"]
COPY ["src/Services/Customer/Customer.Infrastructure/Customer.Infrastructure.csproj", "src/Services/Customer/Customer.Infrastructure/"]
COPY ["src/Services/Customer/Customer.Api/Customer.Api.csproj", "src/Services/Customer/Customer.Api/"]
RUN dotnet restore "src/Services/Customer/Customer.Api/Customer.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/Customer/Customer.Api"
RUN dotnet publish "Customer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Customer.Api.dll"]
