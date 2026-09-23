# Usar la imagen base de .NET SDK para compilar
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de proyecto y restaurar dependencias
COPY ["SchoolManager.sln", "./"]
COPY ["SchoolManager/SchoolManager.csproj", "SchoolManager/"]
COPY ["SchoolManager.Tests/SchoolManager.Tests.csproj", "SchoolManager.Tests/"]
COPY ["SchoolManager/libman.json", "SchoolManager/"]
RUN dotnet restore

# Copiar el resto del código fuente
COPY SchoolManager/ SchoolManager/

# Publicar la aplicación
RUN dotnet publish "SchoolManager/SchoolManager.csproj" -c Release -o /app/publish --no-restore

# Construir la imagen final de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Dependencias: PostgreSQL client + librerías para Chromium/PuppeteerSharp (headless).
# Sin libglib, nss, etc. Chrome descargado por Puppeteer falla en runtime en Linux.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libpq-dev \
    libglib2.0-0 \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libdbus-1-3 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    libpango-1.0-0 \
    libcairo2 \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

# Copiar la aplicación publicada
COPY --from=build /app/publish .

# Crear directorio para uploads
RUN mkdir -p /app/wwwroot/uploads && \
    chmod 755 /app/wwwroot/uploads

# Configurar las variables de entorno
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Exponer el puerto
EXPOSE 8080

# Configurar el punto de entrada
ENTRYPOINT ["dotnet", "SchoolManager.dll"] 