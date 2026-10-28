# Monthwise release image: the API serving the built front-end on port 8080 (ADR 0006).
# Build from the repository root:
#   docker build --build-arg VERSION=1.2.0 --build-arg COMMIT=$(git rev-parse --short HEAD) -t monthwise .

FROM --platform=$BUILDPLATFORM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
COPY backend/openapi/ /src/backend/openapi/
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
ARG TARGETARCH
ARG VERSION=0.0.0-dev
ARG COMMIT=
WORKDIR /src/backend
# The analyzer settings (e.g. migrations are generated code) live in the root .editorconfig.
COPY .editorconfig /src/
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Monthwise.sln ./
COPY backend/src/Monthwise.Domain/Monthwise.Domain.csproj src/Monthwise.Domain/
COPY backend/src/Monthwise.Application/Monthwise.Application.csproj src/Monthwise.Application/
COPY backend/src/Monthwise.Infrastructure/Monthwise.Infrastructure.csproj src/Monthwise.Infrastructure/
COPY backend/src/Monthwise.Api/Monthwise.Api.csproj src/Monthwise.Api/
RUN dotnet restore src/Monthwise.Api/Monthwise.Api.csproj -a $TARGETARCH
COPY backend/src/ src/
COPY --from=frontend /src/frontend/dist /src/frontend/dist
RUN dotnet publish src/Monthwise.Api/Monthwise.Api.csproj --no-restore -c Release -a $TARGETARCH \
      --self-contained false -o /app \
      -p:Version=$VERSION -p:SourceRevisionId=$COMMIT \
      -p:FrontendDist=/src/frontend/dist -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    MONTHWISE_CONFIG_DIR=/config \
    Database__MigrateOnStartup=true \
    Database__StartupWaitSeconds=60
EXPOSE 8080
USER $APP_UID
# No curl in the image: ask /api/health over bash's /dev/tcp.
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /api/health HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "Monthwise.Api.dll"]
