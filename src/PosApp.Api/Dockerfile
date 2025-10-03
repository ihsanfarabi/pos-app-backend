# syntax=docker/dockerfile:1

# -------- Build stage --------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY pos-app.sln ./
COPY src/PosApp.Api/PosApi.csproj src/PosApp.Api/
COPY src/PosApp.Application/PosApp.Application.csproj src/PosApp.Application/
COPY src/PosApp.Domain/PosApp.Domain.csproj src/PosApp.Domain/
COPY src/PosApp.Infrastructure/PosApp.Infrastructure.csproj src/PosApp.Infrastructure/

RUN dotnet restore src/PosApp.Api/PosApi.csproj

# Copy the remaining source and publish
COPY src/ ./src/
RUN dotnet publish src/PosApp.Api/PosApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------- Runtime stage --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "PosApi.dll"]
