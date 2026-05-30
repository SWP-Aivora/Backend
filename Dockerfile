# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Aivora.api/Aivora.api.csproj", "Aivora.api/"]
COPY ["Aivora.Repositories/Aivora.Repositories.csproj", "Aivora.Repositories/"]
COPY ["Aivora.Services/Aivora.Services.csproj", "Aivora.Services/"]
RUN dotnet restore "Aivora.api/Aivora.api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Aivora.api"
RUN dotnet build "Aivora.api.csproj" -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish "Aivora.api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Aivora.api.dll"]
