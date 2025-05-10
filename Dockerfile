FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN sed -i 's/^Components: main$/& contrib/' /etc/apt/sources.list.d/debian.sources
RUN apt-get update && ln -s /usr/lib/libgdiplus.so /lib/x86_64-linux-gnu/libgdiplus.so
RUN apt-get install -y --no-install-recommends wget
RUN wget http://archive.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2_amd64.deb
RUN dpkg -i libssl1.1_1.1.1f-1ubuntu2_amd64.deb

WORKDIR /disco
ENTRYPOINT ["dotnet", "Disco.Server.dll"]

# Copy the Backend Source
COPY ./bin/ ./
