# ==========================================
# Stage 1: Build (.NET 8 LTS SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY ["LMS.API/LMS.API.csproj", "LMS.API/"]
RUN dotnet restore "LMS.API/LMS.API.csproj"

# Copiar todo el código fuente del backend y compilar en modo Release
COPY . .
WORKDIR "/src/LMS.API"
RUN dotnet publish "LMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Stage 2: Runtime (.NET 8 LTS ASP.NET Core)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN groupadd --system lms && \
    useradd --system --gid lms --home-dir /app --shell /usr/sbin/nologin lmsapp && \
    mkdir -p /app/logs /app/App_Data/uploads && \
    chown -R lmsapp:lms /app

USER lmsapp

# Exponer y configurar puerto 8080 para Render
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "LMS.API.dll"]
