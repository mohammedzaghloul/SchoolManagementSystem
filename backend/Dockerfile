# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all project files
COPY ["School.API/School.API.csproj", "School.API/"]
COPY ["School.Infrastructure/School.Infrastructure.csproj", "School.Infrastructure/"]
COPY ["School.Application/School.Application.csproj", "School.Application/"]
COPY ["School.Domain/School.Domain.csproj", "School.Domain/"]

# Restore dependencies
RUN dotnet restore "School.API/School.API.csproj"

# Copy everything else
COPY . .

# Build and Publish
WORKDIR "/src/School.API"
RUN dotnet publish "School.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects PORT at runtime. Program.cs binds to 0.0.0.0:${PORT}.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "School.API.dll"]
