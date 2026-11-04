cd src/FSLN.Browser
dotnet tool uninstall -g fsln.browser
dotnet pack
dotnet tool install -g --add-source bin/Release FSLN.Browser
