# MediaViewer.Server — build multi-stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaura primeiro para aproveitar cache de camadas
COPY src/MediaViewer.Shared/MediaViewer.Shared.csproj src/MediaViewer.Shared/
COPY src/MediaViewer.Server/MediaViewer.Server.csproj src/MediaViewer.Server/
RUN dotnet restore src/MediaViewer.Server/MediaViewer.Server.csproj

COPY src/ src/
# Sem --no-restore: o publish precisa resolver o runtime pack do ASP.NET
# (de onde sai o _framework/blazor.web.js); com --no-restore ele podia ser
# pulado e o Blazor ficava sem o script -> circuito nunca conectava.
RUN dotnet publish src/MediaViewer.Server/MediaViewer.Server.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# SQLite fica em /data (montar volume para persistir)
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/mediaviewer.db"
EXPOSE 8080
VOLUME /data

ENTRYPOINT ["dotnet", "MediaViewer.Server.dll"]
