# Build API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY XCloud.sln ./
COPY ./XCloud.Api/XCloud.Api.csproj XCloud.Api/
COPY ./XCloud.Clipper/XCloud.Clipper.csproj XCloud.Clipper/
COPY ./XCloud.Core/XCloud.Core.csproj XCloud.Core/
COPY ./XCloud.Sharing/XCloud.Sharing.csproj XCloud.Sharing/
COPY ./XCloud.Storage/XCloud.Storage.csproj XCloud.Storage/
COPY ./XCloud.Helpers/XCloud.Helpers.csproj XCloud.Helpers/
COPY ./XCloud.ReadEra/XCloud.ReadEra.csproj XCloud.ReadEra/
COPY ./XCloud.Common/XCloud.Common.csproj XCloud.Common/
COPY ./XCloud.Automations/XCloud.Automations.csproj XCloud.Automations/
COPY ./XCloud.Ext/XCloud.Ext.csproj XCloud.Ext/
RUN dotnet restore XCloud.Api/XCloud.Api.csproj

COPY . .
WORKDIR /src/XCloud.Api
RUN dotnet publish --no-restore -c Release -o /app

# The final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
COPY --from=build /app .

CMD dotnet XCloud.Api.dll
