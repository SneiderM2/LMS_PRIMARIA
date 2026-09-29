# ==========================================
# Stage 1: Build (.NET 8 LTS SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY ["backend/LMS.API/LMS.API.csproj", "backend/LMS.API/"]
RUN dotnet restore "backend/LMS.API/LMS.API.csproj"

# Copiar todo el código fuente y compilar en modo Release
COPY . .
WORKDIR "/src/backend/LMS.API"
RUN dotnet publish "LMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Stage 2: Runtime (.NET 8 LTS ASP.NET Core)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Exponer y configurar puerto 8080 para Render
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "LMS.API.dll"]
