dotnet publish ./src/Disco.Server/Disco.Server.csproj -c Release -r linux-x64 -o ./bin

echo "Pushing tag 'latest'"
docker build -t ewu.disco.server . --no-cache
docker tag ewu.disco.server:latest docker.ewu-it.de/ewu.disco.server:latest
docker image push docker.ewu-it.de/ewu.disco.server:latest

if($env:BUILD_ARTIFACTSTAGINGDIRECTORY)
{
    echo "latest" > "$($env:BUILD_ARTIFACTSTAGINGDIRECTORY)/disco_server_tag.txt"
}

if($env:BUILD_SOURCEBRANCHNAME)
{    
    if($env:BUILD_SOURCEBRANCHNAME -eq "master")
    {
        echo "Pushing tag 'dev'"
        docker tag ewu.disco.server:latest docker.ewu-it.de/ewu.disco.server:dev
        docker image push docker.ewu-it.de/ewu.disco.server:dev
    }
    else 
    {
        echo "Pushing tag '$env:BUILD_SOURCEBRANCHNAME'"
        docker tag ewu.disco.server:latest docker.ewu-it.de/ewu.disco.server:$($env:BUILD_SOURCEBRANCHNAME)
        docker image push docker.ewu-it.de/ewu.disco.server:$($env:BUILD_SOURCEBRANCHNAME)    
    }
}

if($env:BUILD_BUILDNUMBER)
{
    echo "Pushing tag '$env:BUILD_BUILDNUMBER'"
    docker tag ewu.disco.server:latest docker.ewu-it.de/ewu.disco.server:$($env:BUILD_BUILDNUMBER)
    docker image push docker.ewu-it.de/ewu.disco.server:$($env:BUILD_BUILDNUMBER)
    $env:BUILD_BUILDNUMBER > "$($env:BUILD_ARTIFACTSTAGINGDIRECTORY)/disco_server_tag.txt"
}

echo "Build Completed."
if($env:BUILD_ARTIFACTSTAGINGDIRECTORY)
{
    echo "Tag: $(get-content "$($env:BUILD_ARTIFACTSTAGINGDIRECTORY)/disco_server_tag.txt")"
}
