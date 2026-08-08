# syntax=docker/dockerfile:1.7
# Build context is the repo root, not src/AppointMe.Api.

# Stage 1: frontend bundle
FROM node:22-alpine AS frontend
# Git short SHA of the build; shown in the app footer. CI passes it explicitly.
ARG APP_VERSION=local
ENV VITE_APP_VERSION=$APP_VERSION
WORKDIR /src
COPY src/AppointMe.Frontend/package.json src/AppointMe.Frontend/package-lock.json ./
RUN npm ci
COPY src/AppointMe.Frontend/ ./
RUN npm run build

# Stage 2: API publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore AppointMe.sln
RUN ASPNETCORE_ENVIRONMENT=Codegen \
    dotnet run --project src/AppointMe.Api/AppointMe.Api.csproj \
    -c $BUILD_CONFIGURATION --no-restore --no-launch-profile -- codegen write
RUN dotnet publish src/AppointMe.Api/AppointMe.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 3: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=frontend /src/dist ./wwwroot
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AppointMe.Api.dll"]
