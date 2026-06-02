FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Skemex.Web/Skemex.Web.csproj", "src/Skemex.Web/"]
COPY ["src/Skemex.Infrastructure/Skemex.Infrastructure.csproj", "src/Skemex.Infrastructure/"]
COPY ["src/Skemex.Application/Skemex.Application.csproj", "src/Skemex.Application/"]
COPY ["src/Skemex.Domain/Skemex.Domain.csproj", "src/Skemex.Domain/"]
RUN dotnet restore "src/Skemex.Web/Skemex.Web.csproj"
COPY . .
WORKDIR "/src/src/Skemex.Web"
RUN dotnet build "./Skemex.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Skemex.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Skemex.Web.dll"]
