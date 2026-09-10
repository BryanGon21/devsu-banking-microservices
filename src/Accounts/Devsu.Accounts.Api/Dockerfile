FROM mcr.microsoft.com/dotnet/sdk:8.0.416-bookworm-slim AS build
WORKDIR /source

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/Accounts/Devsu.Accounts.Domain/Devsu.Accounts.Domain.csproj", "src/Accounts/Devsu.Accounts.Domain/"]
COPY ["src/Accounts/Devsu.Accounts.Domain/packages.lock.json", "src/Accounts/Devsu.Accounts.Domain/"]
COPY ["src/Accounts/Devsu.Accounts.Application/Devsu.Accounts.Application.csproj", "src/Accounts/Devsu.Accounts.Application/"]
COPY ["src/Accounts/Devsu.Accounts.Application/packages.lock.json", "src/Accounts/Devsu.Accounts.Application/"]
COPY ["src/Accounts/Devsu.Accounts.Infrastructure/Devsu.Accounts.Infrastructure.csproj", "src/Accounts/Devsu.Accounts.Infrastructure/"]
COPY ["src/Accounts/Devsu.Accounts.Infrastructure/packages.lock.json", "src/Accounts/Devsu.Accounts.Infrastructure/"]
COPY ["src/Accounts/Devsu.Accounts.Api/Devsu.Accounts.Api.csproj", "src/Accounts/Devsu.Accounts.Api/"]
COPY ["src/Accounts/Devsu.Accounts.Api/packages.lock.json", "src/Accounts/Devsu.Accounts.Api/"]
RUN dotnet restore "src/Accounts/Devsu.Accounts.Api/Devsu.Accounts.Api.csproj" --locked-mode

COPY src/Accounts/ src/Accounts/
RUN dotnet publish "src/Accounts/Devsu.Accounts.Api/Devsu.Accounts.Api.csproj" \
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

ENTRYPOINT ["dotnet", "Devsu.Accounts.Api.dll"]
