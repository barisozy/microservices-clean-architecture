FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/BuildingBlocks/ECommerce.Contracts/ECommerce.Contracts.csproj", "src/BuildingBlocks/ECommerce.Contracts/"]
COPY ["src/BuildingBlocks/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj", "src/BuildingBlocks/ECommerce.ServiceDefaults/"]
COPY ["src/Services/IAM/IAM.Domain/IAM.Domain.csproj", "src/Services/IAM/IAM.Domain/"]
COPY ["src/Services/IAM/IAM.Application/IAM.Application.csproj", "src/Services/IAM/IAM.Application/"]
COPY ["src/Services/IAM/IAM.Infrastructure/IAM.Infrastructure.csproj", "src/Services/IAM/IAM.Infrastructure/"]
COPY ["src/Services/IAM/IAM.Api/IAM.Api.csproj", "src/Services/IAM/IAM.Api/"]
RUN dotnet restore "src/Services/IAM/IAM.Api/IAM.Api.csproj"
COPY . .
WORKDIR "/src/src/Services/IAM/IAM.Api"
RUN dotnet publish "IAM.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "IAM.Api.dll"]
