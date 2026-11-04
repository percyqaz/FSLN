dotnet tool uninstall -g fsln.solution
dotnet pack
dotnet tool install -g --add-source bin/Release FSLN.Solution