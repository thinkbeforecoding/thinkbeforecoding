@REM dotnet publish --os linux --arch x64 -c Release -p:PublishProfile=DefaultContainer src/proxy
@REM docker tag proxy:1.0.0 thinkbeforecoding/proxy:latest
docker run --rm -v D:\dev\thinkbeforecoding\:/app -w /app mcr.microsoft.com/dotnet/nightly/sdk:8.0-alpine3.19-aot sh -c "dotnet tool restore; dotnet publish -c Release ./src/proxy -o ./src/proxy/publish"
docker build .\src\proxy\ -t thinkbeforecoding/proxy:latest
docker build src/blog -t thinkbeforecoding/blog:latest