FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY SWD63AMovieUploader/SWD63AMovieUploader.csproj SWD63AMovieUploader/
RUN dotnet restore SWD63AMovieUploader/SWD63AMovieUploader.csproj

COPY . ./
RUN dotnet publish SWD63AMovieUploader -c Release -o out
 
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
EXPOSE 80
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SWD63AMovieUploader.dll"]