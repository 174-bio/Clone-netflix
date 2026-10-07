FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY netflix-clone/NetflixClone.csproj netflix-clone/
RUN dotnet restore netflix-clone/NetflixClone.csproj
COPY netflix-clone/ netflix-clone/
WORKDIR /src/netflix-clone
RUN dotnet publish NetflixClone.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "NetflixClone.dll"]