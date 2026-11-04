dotnet tool uninstall -g fsln.files
dotnet pack
dotnet tool install -g --add-source bin/Release FSLN.Files
