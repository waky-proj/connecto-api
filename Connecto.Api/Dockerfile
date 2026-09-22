FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all project files while preserving the repository structure.
COPY Connecto.Api/Connecto.Api.csproj Connecto.Api/
COPY Connecto.Application/Connecto.Application.csproj Connecto.Application/
COPY Connecto.Domain/Connecto.Domain.csproj Connecto.Domain/
COPY Connecto.Infrastructure/Connecto.Infrastructure.csproj Connecto.Infrastructure/
COPY Connecto.Tests.Unit/Connecto.Tests.Unit.csproj Connecto.Tests.Unit/

# Restore after all project references are present.
RUN dotnet restore Connecto.Api/Connecto.Api.csproj --force

# Copy the source code.
COPY Connecto.Api/ Connecto.Api/
COPY Connecto.Application/ Connecto.Application/
COPY Connecto.Domain/ Connecto.Domain/
COPY Connecto.Infrastructure/ Connecto.Infrastructure/
COPY Connecto.Tests.Unit/ Connecto.Tests.Unit/

WORKDIR /src/Connecto.Api

# Restore again in the final source layout, then build.
RUN dotnet restore Connecto.Api.csproj --force
RUN dotnet build Connecto.Api.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-restore \
    --output /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/Connecto.Api

RUN dotnet publish Connecto.Api.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Connecto.Api.dll"]