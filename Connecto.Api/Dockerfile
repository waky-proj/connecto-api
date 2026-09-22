FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Connecto.Api.csproj", "."]
RUN dotnet restore "./Connecto.Api.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./Connecto.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./Connecto.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Connecto.Api.dll"]