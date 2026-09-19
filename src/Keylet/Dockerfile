FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build

ARG VERSION=1.0.0
ARG ASSEMBLY_VERSION=1.0.0
ARG FILE_VERSION=1.0.0
ARG INFO_VERSION=1.0.0-local

WORKDIR /src

COPY ["src/Keylet/Keylet.csproj", "src/Keylet/"]
COPY ["src/Keylet.ServiceDefaults/Keylet.ServiceDefaults.csproj", "src/Keylet.ServiceDefaults/"]
RUN dotnet restore "src/Keylet/Keylet.csproj"

COPY . .
WORKDIR /src/src/Keylet
RUN dotnet publish "Keylet.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:ContinuousIntegrationBuild=true \
    /p:Version=$VERSION \
    /p:AssemblyVersion=$ASSEMBLY_VERSION \
    /p:FileVersion=$FILE_VERSION \
    /p:InformationalVersion=$INFO_VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Keylet.dll"]

