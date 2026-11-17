# Build context: ./backend  (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached while only source files change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BookStore.Domain/BookStore.Domain.csproj src/BookStore.Domain/
COPY src/BookStore.Application/BookStore.Application.csproj src/BookStore.Application/
COPY src/BookStore.Infrastructure/BookStore.Infrastructure.csproj src/BookStore.Infrastructure/
COPY src/BookStore.Api/BookStore.Api.csproj src/BookStore.Api/
RUN dotnet restore src/BookStore.Api/BookStore.Api.csproj

COPY src/ src/
RUN dotnet publish src/BookStore.Api/BookStore.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
# Local cover storage (Storage:Provider=Local) writes here as the non-root app user; mount a volume
# on /app/wwwroot/uploads to keep the files. Production uses Supabase Storage instead.
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID /app/wwwroot
USER $APP_UID
ENTRYPOINT ["dotnet", "BookStore.Api.dll"]
