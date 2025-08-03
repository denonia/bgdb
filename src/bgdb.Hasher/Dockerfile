FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["bgdb.Hasher/bgdb.Hasher.csproj", "bgdb.Hasher/"]
RUN dotnet restore "bgdb.Hasher/bgdb.Hasher.csproj"
COPY . .
WORKDIR "/src/bgdb.Hasher"
RUN dotnet build "./bgdb.Hasher.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./bgdb.Hasher.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "bgdb.Hasher.dll"]
