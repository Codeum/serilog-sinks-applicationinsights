echo "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path .\artifacts) {
	echo "build: Cleaning .\artifacts"
	Remove-Item .\artifacts -Force -Recurse
}

& dotnet restore --no-cache

foreach ($src in ls .\src\*) {
    Push-Location $src

	echo "build: Packaging project in $src"

    & dotnet pack -c Release -o ..\..\artifacts
    if($LASTEXITCODE -ne 0) { exit 1 }    

    Pop-Location
}

foreach ($nupkg in ls .\artifacts\*.nupkg) {
	echo "##teamcity[publishArtifacts '$nupkg']"
}

Pop-Location