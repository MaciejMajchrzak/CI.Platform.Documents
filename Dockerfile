ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

ARG API_PROJECT=src/CI.Platform.Documents.API/CI.Platform.Documents.API.csproj

COPY nuget.config .
COPY ["src/CI.Platform.Documents.Domain/CI.Platform.Documents.Domain.csproj",                 "src/CI.Platform.Documents.Domain/"]
COPY ["src/CI.Platform.Documents.Core/CI.Platform.Documents.Core.csproj",                     "src/CI.Platform.Documents.Core/"]
COPY ["src/CI.Platform.Documents.Infrastructure/CI.Platform.Documents.Infrastructure.csproj", "src/CI.Platform.Documents.Infrastructure/"]
COPY ["src/CI.Platform.Documents.API/CI.Platform.Documents.API.csproj",                       "src/CI.Platform.Documents.API/"]
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username ci \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text && \
    dotnet restore ${API_PROJECT}

COPY . .
RUN dotnet publish ${API_PROJECT} -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CI.Platform.Documents.API.dll"]
