dotnet tool uninstall -g fsln.controller
dotnet pack
dotnet tool install -g --add-source bin/Release FSLN.Controller