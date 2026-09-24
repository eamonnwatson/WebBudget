param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

docker buildx build --platform linux/arm64 -f ./Dockerfile -t predictivebudget-web:$Version --build-arg VERSION=$Version .
docker save "predictivebudget-web:$Version" | gzip > predictivebudget-web_$Version.tar.gz

