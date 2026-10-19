# ==========================================
# Stage 1: Build (.NET 8 LTS SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY ["backend/LMS.API/LMS.API.csproj", "backend/LMS.API/"]
RUN dotnet restore "backend/LMS.API/LMS.API.csproj"

# Copiar el código fuente y compilar en modo Release.
# .dockerignore excluye node_modules, bin/obj, logs y appsettings.Local.json.
COPY . .
WORKDIR "/src/backend/LMS.API"
RUN dotnet publish "LMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Stage 2: Runtime (.NET 8 LTS ASP.NET Core)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Ejecutar como usuario sin privilegios (principio de mínimo privilegio)
RUN adduser --disabled-password --gecos "" --home /app appuser \
    && mkdir -p /app/logs /app/App_Data/uploads \
    && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_NOLOGO=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

EXPOSE 8080
USER appuser

ENTRYPOINT ["dotnet", "LMS.API.dll"]
