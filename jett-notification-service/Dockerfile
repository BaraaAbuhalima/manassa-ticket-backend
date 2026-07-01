FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["jett-notification-service/jett-notification-service.csproj", "jett-notification-service/"]
RUN dotnet restore "jett-notification-service/jett-notification-service.csproj"
COPY . .
WORKDIR "/src/jett-notification-service"
RUN dotnet build "./jett-notification-service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./jett-notification-service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "jett-notification-service.dll"]
