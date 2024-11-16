#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["gtsCore/gtsCore.csproj", "gtsCore/"]
COPY ["GamestatsBase/GamestatBaseCore/GamestatsBaseCore.csproj", "GamestatsBase/GamestatBaseCore/"]
COPY ["library/Library.csproj", "library/"]
COPY ["pkmn-classic-framework.ServiceDefaults/pkmn-classic-framework.ServiceDefaults.csproj", "pkmn-classic-framework.ServiceDefaults/"]
RUN dotnet restore "./gtsCore/gtsCore.csproj"
COPY . .
WORKDIR "/src/gtsCore"
RUN dotnet build "./gtsCore.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./gtsCore.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "gtsCore.dll"]