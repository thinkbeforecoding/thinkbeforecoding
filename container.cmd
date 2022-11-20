dotnet publish --os linux --arch x64 -c Release -p:PublishProfile=DefaultContainer src/proxyo
docker tag proxy:1.0.0 thinkbeforecoding/proxy:latest
docker build src/blog -t thinkbeforecoding/blog:latest