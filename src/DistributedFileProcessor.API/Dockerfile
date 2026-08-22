# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DistributedFileProcessor.API/DistributedFileProcessor.API.csproj", "src/DistributedFileProcessor.API/"]
COPY ["src/DistributedFileProcessor.Application/DistributedFileProcessor.Application.csproj", "src/DistributedFileProcessor.Application/"]
COPY ["src/DistributedFileProcessor.Domain/DistributedFileProcessor.Domain.csproj", "src/DistributedFileProcessor.Domain/"]
COPY ["src/DistributedFileProcessor.Infrastructure/DistributedFileProcessor.Infrastructure.csproj", "src/DistributedFileProcessor.Infrastructure/"]
RUN dotnet restore "./src/DistributedFileProcessor.API/DistributedFileProcessor.API.csproj"
COPY . .
WORKDIR "/src/src/DistributedFileProcessor.API"
RUN dotnet publish "./DistributedFileProcessor.API.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DistributedFileProcessor.API.dll"]