dotnet tool uninstall -g fsln.fernglas
dotnet pack
dotnet tool install -g --add-source bin/Release FSLN.Fernglas
