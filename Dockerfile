# Stage 1: Restore dependencies
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY ["Aivora.api/Aivora.api.csproj", "Aivora.api/"]
COPY ["Aivora.Repositories/Aivora.Repositories.csproj", "Aivora.Repositories/"]
COPY ["Aivora.Services/Aivora.Services.csproj", "Aivora.Services/"]
RUN dotnet restore "Aivora.api/Aivora.api.csproj"

# Stage 2: Build
FROM restore AS build
COPY . .
WORKDIR "/src/Aivora.api"
RUN dotnet build "Aivora.api.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "Aivora.api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user
RUN addgroup -g 1001 -S appgroup && adduser -S appuser -u 1001 -G appgroup
USER appuser

COPY --from=publish /app/publish .

ENV DOTNET_USE_FILE_WATCHER=false
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Aivora.api.dll"]
