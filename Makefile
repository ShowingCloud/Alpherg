VERSION=1.0.28
SOURCE=radarUdpF/Library.fs


all: publish pack push

clean:
	dotnet clean -c Debug -f netcoreapp3.1
	dotnet clean -c Release -f netcoreapp3.1
	dotnet clean -c Library -f netcoreapp3.1
	dotnet clean -c Debug -f netstandard2.0
	dotnet clean -c Release -f netstandard2.0
	dotnet clean -c Library -f netstandard2.0


build: upgrade build-debug build-release build-library

build-debug: radarUdpF/bin/Debug/netcoreapp3.1/radarUdpF.dll
 
build-release: radarUdpF/bin/Release/netcoreapp3.1/radarUdpF.dll

build-library: radarUdpF/bin/Library/netstandard2.0/radarUdpF.dll


publish: publish-debug publish-release publish-library

publish-debug: radarUdpF/bin/Debug/netcoreapp3.1/publish/radarUdpF.deps.json

publish-release: radarUdpF/bin/Release/netcoreapp3.1/publish/radarUdpF.deps.json

publish-library: radarUdpF/bin/Library/netstandard2.0/publish/radarUdpF.deps.json


pack: pack-debug pack-release pack-library

pack-debug: radarUdpF/bin/Debug/radarUdpF.${VERSION}.nupkg

pack-release: radarUdpF/bin/Release/radarUdpF.${VERSION}.nupkg

pack-library: radarUdpF/bin/Library/radarUdpF.${VERSION}.nupkg


push: .nuget.pushed


upgrade:
	dotnet outdated --upgrade

radarUdpF/bin/Release/netcoreapp3.1/radarUdpF.dll: ${SOURCE}
	dotnet build -c Release -f netcoreapp3.1
	dotnet build -c Release -f netstandard2.0

radarUdpF/bin/Debug/netcoreapp3.1/radarUdpF.dll: ${SOURCE}
	dotnet build -c Debug -f netcoreapp3.1
	dotnet build -c Debug -f netstandard2.0

radarUdpF/bin/Library/netstandard2.0/radarUdpF.dll: ${SOURCE}
	dotnet build -c Library -f netcoreapp3.1
	dotnet build -c Library -f netstandard2.0


radarUdpF/bin/Release/netcoreapp3.1/publish/radarUdpF.deps.json: radarUdpF/bin/Release/netcoreapp3.1/radarUdpF.dll
	dotnet publish -c Release -f netcoreapp3.1
	dotnet publish -c Release -f netstandard2.0

radarUdpF/bin/Debug/netcoreapp3.1/publish/radarUdpF.deps.json: radarUdpF/bin/Debug/netcoreapp3.1/radarUdpF.dll
	dotnet publish -c Debug -f netcoreapp3.1
	dotnet publish -c Debug -f netstandard2.0

radarUdpF/bin/Library/netstandard2.0/publish/radarUdpF.deps.json: radarUdpF/bin/Library/netstandard2.0/radarUdpF.dll
	dotnet publish -c Library -f netcoreapp3.1
	dotnet publish -c Library -f netstandard2.0


radarUdpF/bin/Release/radarUdpF.${VERSION}.nupkg: radarUdpF/bin/Release/netcoreapp3.1/radarUdpF.dll
	dotnet pack -c Release --no-build -p:Version=${VERSION}

radarUdpF/bin/Debug/radarUdpF.${VERSION}.nupkg: radarUdpF/bin/Debug/netcoreapp3.1/radarUdpF.dll
	dotnet pack -c Debug --no-build -p:Version=${VERSION}

radarUdpF/bin/Library/radarUdpF.${VERSION}.nupkg: radarUdpF/bin/Library/netstandard2.0/radarUdpF.dll
	dotnet pack -c Library --no-build -p:Version=${VERSION}


.nuget.pushed: radarUdpF/bin/Library/radarUdpF.${VERSION}.nupkg
	dotnet nuget push $< -s GitHub --skip-duplicate
	nuget push $< -Source nuget.org -SkipDuplicate
	touch .nuget.pushed
