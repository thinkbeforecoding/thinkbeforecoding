FROM alpine:3.21
COPY bin/Release/net9.0/linux-musl-x64/publish/proxy  /app/
EXPOSE 8080/tcp
WORKDIR /app
CMD ["./proxy"]