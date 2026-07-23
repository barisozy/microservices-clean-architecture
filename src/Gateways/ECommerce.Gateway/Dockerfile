FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install prerequisites for Native AOT
RUN apt-get update && apt-get install -y clang zlib1g-dev

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
RUN dotnet publish "ECommerce.Gateway.csproj" -c Release -r linux-x64 --self-contained -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Use non-root user for security
RUN useradd -d /app -M -s /bin/false appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["./ECommerce.Gateway"]
