FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["School.API/School.API.csproj", "School.API/"]
COPY ["School.Application/School.Application.csproj", "School.Application/"]
COPY ["School.Infrastructure/School.Infrastructure.csproj", "School.Infrastructure/"]
COPY ["School.Domain/School.Domain.csproj", "School.Domain/"]

RUN dotnet restore "School.API/School.API.csproj"

COPY . .
WORKDIR /src/School.API
RUN dotnet publish "School.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "School.API.dll"]
