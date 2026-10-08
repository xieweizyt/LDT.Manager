FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY NuGet.Config ./
COPY BaseService ./BaseService
COPY Ldt.Host ./Ldt.Host
RUN dotnet restore Ldt.Host/Ldt.Host.csproj --configfile NuGet.Config
RUN dotnet publish Ldt.Host/Ldt.Host.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app .
EXPOSE 5000
ENTRYPOINT ["dotnet", "Ldt.Host.dll"]
