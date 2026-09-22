FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Connecto.Api/Connecto.Api.csproj Connecto.Api/
COPY Connecto.Application/Connecto.Application.csproj Connecto.Application/
COPY Connecto.Domain/Connecto.Domain.csproj Connecto.Domain/
COPY Connecto.Infrastructure/Connecto.Infrastructure.csproj Connecto.Infrastructure/

RUN dotnet restore Connecto.Api/Connecto.Api.csproj

COPY Connecto.Api/ Connecto.Api/
COPY Connecto.Application/ Connecto.Application/
COPY Connecto.Domain/ Connecto.Domain/
COPY Connecto.Infrastructure/ Connecto.Infrastructure/

WORKDIR /src/Connecto.Api
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