# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only build config and project files first so the restore layer is cached
COPY global.json Directory.Build.props L2Points.slnx ./
COPY src/L2Points.Domain/L2Points.Domain.csproj src/L2Points.Domain/
COPY src/L2Points.Infrastructure/L2Points.Infrastructure.csproj src/L2Points.Infrastructure/
COPY src/L2Points.Web/L2Points.Web.csproj src/L2Points.Web/
# Restore the web project directly (the solution also lists test projects that are not in the image build context)
RUN dotnet restore src/L2Points.Web/L2Points.Web.csproj

COPY src/ src/
RUN dotnet publish src/L2Points.Web -c Release -o /app --no-restore

# ---- Runtime stage ----
# Debian-based image: ships ICU (cs-CZ culture) and tzdata (Europe/Prague)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Built-in non-root user of the official .NET images
USER app
ENTRYPOINT ["dotnet", "L2Points.Web.dll"]
