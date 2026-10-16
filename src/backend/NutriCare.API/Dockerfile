FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/NutriCare.Domain/NutriCare.Domain.csproj ./src/backend/NutriCare.Domain/
COPY src/backend/NutriCare.Application/NutriCare.Application.csproj ./src/backend/NutriCare.Application/
COPY src/backend/NutriCare.Infrastructure/NutriCare.Infrastructure.csproj ./src/backend/NutriCare.Infrastructure/
COPY src/backend/NutriCare.Persistence/NutriCare.Persistence.csproj ./src/backend/NutriCare.Persistence/
COPY src/backend/NutriCare.API/NutriCare.API.csproj ./src/backend/NutriCare.API/

RUN dotnet restore ./src/backend/NutriCare.API/NutriCare.API.csproj

COPY src/backend ./src/backend
RUN dotnet publish ./src/backend/NutriCare.API/NutriCare.API.csproj -c Release -o /app/publish --no-restore
COPY tools/BrowserInstaller ./tools/BrowserInstaller
RUN dotnet publish ./tools/BrowserInstaller/BrowserInstaller.csproj -c Release -o /app/installer

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
EXPOSE 8080

COPY --from=build /app/publish .
COPY --from=build /app/installer /browser-installer
RUN dotnet /browser-installer/BrowserInstaller.dll install --with-deps chromium \
    && apt-get update && apt-get install -y --no-install-recommends fonts-noto-core \
    && mkdir -p /app/logs /app/.local && chown -R app:app /app/logs /app/.local
USER app
ENTRYPOINT ["dotnet", "NutriCare.API.dll"]
