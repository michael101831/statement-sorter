FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/StatementSorter.Core/StatementSorter.Core.csproj src/StatementSorter.Core/
COPY src/StatementSorter.Web/StatementSorter.Web.csproj src/StatementSorter.Web/
RUN dotnet restore src/StatementSorter.Web/StatementSorter.Web.csproj
COPY src/ src/
RUN dotnet publish src/StatementSorter.Web/StatementSorter.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "StatementSorter.Web.dll"]
