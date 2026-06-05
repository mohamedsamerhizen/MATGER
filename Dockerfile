FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MATGER.sln ./
COPY MATGER.Api/MATGER.Api.csproj MATGER.Api/
COPY MATGER.Tests/MATGER.Tests.csproj MATGER.Tests/

RUN dotnet restore MATGER.sln

COPY . .
RUN dotnet publish MATGER.Api/MATGER.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MATGER.Api.dll"]
