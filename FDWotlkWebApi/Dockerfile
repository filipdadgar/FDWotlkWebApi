FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FDWotlkWebApi/FDWotlkWebApi.csproj", "FDWotlkWebApi/"]
RUN dotnet restore "FDWotlkWebApi/FDWotlkWebApi.csproj"
COPY . .
WORKDIR "/src/FDWotlkWebApi"
RUN dotnet build "FDWotlkWebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FDWotlkWebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables to override appsettings.json
ENV ConnectionStrings__MySql="your_mysql_connection_string_here"
ENV SoapServer__Address="your_soap_server_address"
ENV SoapServer__Username="your_soap_username"
ENV SoapServer__Password="your_soap_password"

ENTRYPOINT ["dotnet", "FDWotlkWebApi.dll"]