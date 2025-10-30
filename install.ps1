pwsh ./pack.ps1

dotnet tool uninstall -g abpdevtools
dotnet tool update -g AbpDevTools --add-source ./nupkg --prerelease