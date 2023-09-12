dotnet pack ./src/AbpDevTools/AbpDevTools.csproj -c Release
dotnet tool update -g AbpDevTools --add-source ./nupkg --prerelease