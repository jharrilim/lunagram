FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 2436
EXPOSE 44393

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ["Lunagram/Lunagram.csproj", "Lunagram/"]
RUN dotnet restore "Lunagram/Lunagram.csproj"
COPY . .
WORKDIR "/src/Lunagram"
RUN dotnet build "Lunagram.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "Lunagram.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Lunagram.dll"]