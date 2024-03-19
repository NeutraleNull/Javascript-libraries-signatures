FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PackageAnalyzer/PackageAnalyzer.csproj", "PackageAnalyzer/"]
RUN dotnet restore "PackageAnalyzer/PackageAnalyzer.csproj"
COPY . .
WORKDIR "/src/PackageAnalyzer"
RUN dotnet build "PackageAnalyzer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PackageAnalyzer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PackageAnalyzer.dll"]
