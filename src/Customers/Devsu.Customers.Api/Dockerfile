FROM mcr.microsoft.com/dotnet/sdk:8.0.416-bookworm-slim AS build
WORKDIR /source

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/Customers/Devsu.Customers.Domain/Devsu.Customers.Domain.csproj", "src/Customers/Devsu.Customers.Domain/"]
COPY ["src/Customers/Devsu.Customers.Domain/packages.lock.json", "src/Customers/Devsu.Customers.Domain/"]
COPY ["src/Customers/Devsu.Customers.Application/Devsu.Customers.Application.csproj", "src/Customers/Devsu.Customers.Application/"]
COPY ["src/Customers/Devsu.Customers.Application/packages.lock.json", "src/Customers/Devsu.Customers.Application/"]
COPY ["src/Customers/Devsu.Customers.Infrastructure/Devsu.Customers.Infrastructure.csproj", "src/Customers/Devsu.Customers.Infrastructure/"]
COPY ["src/Customers/Devsu.Customers.Infrastructure/packages.lock.json", "src/Customers/Devsu.Customers.Infrastructure/"]
COPY ["src/Customers/Devsu.Customers.Api/Devsu.Customers.Api.csproj", "src/Customers/Devsu.Customers.Api/"]
COPY ["src/Customers/Devsu.Customers.Api/packages.lock.json", "src/Customers/Devsu.Customers.Api/"]
RUN dotnet restore "src/Customers/Devsu.Customers.Api/Devsu.Customers.Api.csproj" --locked-mode

COPY src/Customers/ src/Customers/
RUN dotnet publish "src/Customers/Devsu.Customers.Api/Devsu.Customers.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0.22-bookworm-slim AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "Devsu.Customers.Api.dll"]
