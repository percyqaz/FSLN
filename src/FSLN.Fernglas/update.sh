dotnet tool uninstall -g fernglas
dotnet pack
dotnet tool install -g --add-source bin/Release fernglas
