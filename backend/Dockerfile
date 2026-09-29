# ==========================================
# Etapa 1: Compilación con .NET 10 SDK
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Optimización de caché: copiar csproj y restaurar dependencias NuGet
COPY ["LMS.API/LMS.API.csproj", "LMS.API/"]
RUN dotnet restore "LMS.API/LMS.API.csproj"

# Copiar el resto del código del backend
COPY . .

# Compilar y publicar la solución en modo Release
WORKDIR "/src/LMS.API"
RUN dotnet publish "LMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Etapa 2: Imagen de ejecución (Runtime ASP.NET 10)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Configuración de puerto 8080 para Render
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Copiar binarios compilados
COPY --from=build /app/publish .

# Iniciar la aplicación
ENTRYPOINT ["dotnet", "LMS.API.dll"]
