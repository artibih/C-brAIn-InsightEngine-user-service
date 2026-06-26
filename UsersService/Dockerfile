
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./

# Work around SDK glob/default-items issues by explicitly including files
# (If you later confirm you don't need this, you can delete this whole block.)
RUN echo '<Project>' > Directory.Build.targets && \
    echo '  <PropertyGroup>' >> Directory.Build.targets && \
    echo '    <EnableDefaultItems>false</EnableDefaultItems>' >> Directory.Build.targets && \
    echo '  </PropertyGroup>' >> Directory.Build.targets && \
    echo '  <ItemGroup>' >> Directory.Build.targets && \
    find . -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | while read f; do \
        echo "    <Compile Include=\"$f\" />" >> Directory.Build.targets; \
    done && \
    echo '    <Content Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />' >> Directory.Build.targets && \
    echo '    <Content Include="appsettings.Development.json" CopyToOutputDirectory="PreserveNewest" />' >> Directory.Build.targets && \
    echo '    <None Include="Templates/**" CopyToOutputDirectory="PreserveNewest" />' >> Directory.Build.targets && \
    echo '    <None Include="keys/**" CopyToOutputDirectory="PreserveNewest" />' >> Directory.Build.targets && \
    echo '  </ItemGroup>' >> Directory.Build.targets && \
    echo '</Project>' >> Directory.Build.targets

# Restore + publish
RUN dotnet restore ./UsersService.csproj

# Publish (Release by default; set BUILD_ENV=development if you want Debug)
ARG BUILD_ENV=production
RUN if [ "$BUILD_ENV" = "development" ] ; then \
        dotnet publish ./UsersService.csproj -c Debug -o /out /p:UseAppHost=false \
          -p:TreatWarningsAsErrors=false -p:WarningsAsErrors= ; \
    else \
        dotnet publish ./UsersService.csproj -c Release -o /out /p:UseAppHost=false \
          -p:TreatWarningsAsErrors=false -p:WarningsAsErrors= ; \
    fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /out ./

# Azure Container Apps default ingress target port is commonly 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UsersService.dll"]
