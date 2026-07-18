FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["manassa-ticket-backend/manassa-ticket-backend.csproj", "manassa-ticket-backend/"]
RUN dotnet restore "manassa-ticket-backend/manassa-ticket-backend.csproj"
COPY . .
WORKDIR "/src/manassa-ticket-backend"
RUN dotnet build "./manassa-ticket-backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./manassa-ticket-backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "manassa-ticket-backend.dll"]
