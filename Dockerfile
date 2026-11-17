# Una sola imagen: la API, el servidor MCP y el front (Blazor WebAssembly), en la misma URL.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero los proyectos, para que la restauración se cachee mientras no cambien las dependencias.
COPY Directory.Build.props .editorconfig .globalconfig spectrace.json Reservas.slnx ./
COPY specs/ specs/
COPY src/Reservas.Domain/Reservas.Domain.csproj src/Reservas.Domain/
COPY src/Reservas.Contracts/Reservas.Contracts.csproj src/Reservas.Contracts/
COPY src/Reservas.Infrastructure/Reservas.Infrastructure.csproj src/Reservas.Infrastructure/
COPY src/Reservas.Web/Reservas.Web.csproj src/Reservas.Web/
COPY src/Reservas.Api/Reservas.Api.csproj src/Reservas.Api/
RUN dotnet restore src/Reservas.Api/Reservas.Api.csproj

COPY src/ src/
RUN dotnet publish src/Reservas.Api/Reservas.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Las zonas horarias (Europe/Madrid), para mostrar las horas locales.
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    PORT=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Reservas.Api.dll"]
