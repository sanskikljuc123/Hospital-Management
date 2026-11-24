# Build context is the backend/ folder:  docker build -f api/Dockerfile .
# Base images pinned to a digest (R-040); Dependabot updates tag and digest together.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY Domain/Domain.csproj Domain/
COPY Data/Data.csproj Data/
COPY Services/Services.csproj Services/
COPY HospitalManagement.ServiceDefaults/HospitalManagement.ServiceDefaults.csproj HospitalManagement.ServiceDefaults/
COPY api/api.csproj api/
RUN dotnet restore api/api.csproj

COPY Domain/ Domain/
COPY Data/ Data/
COPY Services/ Services/
COPY HospitalManagement.ServiceDefaults/ HospitalManagement.ServiceDefaults/
COPY api/ api/
RUN dotnet publish api/api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Non-root user provided by the base image.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "api.dll"]
