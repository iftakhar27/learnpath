# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/LearnPath.Api/LearnPath.Api.csproj src/LearnPath.Api/
RUN dotnet restore src/LearnPath.Api/LearnPath.Api.csproj
COPY src/ src/
RUN dotnet publish src/LearnPath.Api/LearnPath.Api.csproj -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Kolkata
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "LearnPath.Api.dll"]
