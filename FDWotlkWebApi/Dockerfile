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

# Set environment variables to override appsettings.json and make Kestrel listen on container port 8080
# Note: update the connection string and soap credentials with secure values when deploying
ENV ASPNETCORE_URLS="http://+:8080"
ENV ConnectionStrings__Mangos="Server=dbname;Port=3306;Database=wotlkrealmd;User Id=dbuser;Password=dbpassword;"
ENV SoapServer__Host="servername"
ENV SoapServer__Port="7878"
ENV SoapServer__Username="USERNAME"
ENV SoapServer__Password="PASSWORD"

ENTRYPOINT ["dotnet", "FDWotlkWebApi.dll"]