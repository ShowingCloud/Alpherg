all: publish push

VERSION=1.0.5

clean:
	dotnet clean

build: radarUdpF/bin/Release/netstandard2.0/radarUdpF.dll radarUdpF/bin/Debug/netstandard2.0/radarUdpF.dll

publish: radarUdpF/bin/Release/netstandard2.0/publish/radarUdpF.deps.json radarUdpF/bin/Debug/netstandard2.0/publish/radarUdpF.deps.json

pack: radarUdpF/bin/Release/radarUdpF.${VERSION}.nupkg radarUdpF/bin/Debug/radarUdpF.${VERSION}.nupkg

push: .nuget.pushed
   
radarUdpF/bin/Release/netstandard2.0/radarUdpF.dll: radarUdpF/Library.fs
	dotnet build -c Release

radarUdpF/bin/Debug/netstandard2.0/radarUdpF.dll: radarUdpF/Library.fs
	dotnet build -c Debug

radarUdpF/bin/Release/netstandard2.0/publish/radarUdpF.deps.json: radarUdpF/bin/Release/netstandard2.0/radarUdpF.dll
	dotnet publish -c Release

radarUdpF/bin/Debug/netstandard2.0/publish/radarUdpF.deps.json: radarUdpF/bin/Debug/netstandard2.0/radarUdpF.dll
	dotnet publish -c Debug

radarUdpF/bin/Release/radarUdpF.${VERSION}.nupkg: radarUdpF/bin/Release/netstandard2.0/radarUdpF.dll
	dotnet pack -c Release --no-build -p:Version=${VERSION}

radarUdpF/bin/Debug/radarUdpF.${VERSION}.nupkg: radarUdpF/bin/Debug/netstandard2.0/radarUdpF.dll
	dotnet pack -c Debug --no-build -p:Version=${VERSION}

.nuget.pushed: radarUdpF/bin/Debug/radarUdpF.${VERSION}.nupkg
	dotnet nuget push $< -s GitHub --skip-duplicate
	nuget push $< -Source nuget.org -SkipDuplicate
	touch .nuget.pushed
