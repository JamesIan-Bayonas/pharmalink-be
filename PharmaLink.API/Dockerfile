# 1. Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["PharmaLink.API/PharmaLink.API.csproj", "PharmaLink.API/"]
RUN dotnet restore "PharmaLink.API/PharmaLink.API.csproj"

# Copy all source files and publish
COPY . .
WORKDIR "/src/PharmaLink.API"
RUN dotnet publish "PharmaLink.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose Railway container port
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PharmaLink.API.dll"]