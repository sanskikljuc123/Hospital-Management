# Build context is the backend/ folder:  docker build -f api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Non-root user provided by the base image.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "api.dll"]
