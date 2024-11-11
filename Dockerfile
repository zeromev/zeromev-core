FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /app

# Copy everything
COPY . ./

# Restore dependencies
RUN dotnet restore ZeroMevApi.sln

RUN dotnet publish ZeroMevApi.sln -c Release -o /build

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build-env /build .
COPY ZeroMev/Server/appsettings.json .
# By default the SportAdvisor.Api project is started
ENTRYPOINT ["dotnet", "ZeroMev.ServerNoClient.dll"]
