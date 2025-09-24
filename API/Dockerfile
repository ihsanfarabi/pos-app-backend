# syntax=docker/dockerfile:1

# -------- Build stage --------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY PosApi.csproj ./
RUN dotnet restore "PosApi.csproj"

# Copy the remaining source and publish
COPY . .
RUN dotnet publish "PosApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -------- Runtime stage --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Configure ASP.NET Core to listen on port 8080 by default
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Copy published output
COPY --from=build /app/publish ./

# Expose the port for clarity (optional with ASPNETCORE_URLS)
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "PosApi.dll"]


