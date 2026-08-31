# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["DriveAndGo_API.csproj", "./"]
RUN dotnet restore "DriveAndGo_API.csproj"

# Copy remaining source code and build
COPY . .
RUN dotnet build "DriveAndGo_API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DriveAndGo_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV PORT=8080
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DriveAndGo_API.dll"]
