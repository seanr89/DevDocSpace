FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DevDocSpace.sln .
COPY DevDocSpace.Api/DevDocSpace.Api.csproj DevDocSpace.Api/
COPY DevDocSpace.Data/DevDocSpace.Data.csproj DevDocSpace.Data/
COPY DevDocSpace.Api.Tests/DevDocSpace.Api.Tests.csproj DevDocSpace.Api.Tests/
RUN dotnet restore DevDocSpace.Api/DevDocSpace.Api.csproj
COPY . .
RUN dotnet publish DevDocSpace.Api/DevDocSpace.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    Content__RootPath=/content
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevDocSpace.Api.dll"]
