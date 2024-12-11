docker build -t limited-sftp .
docker run -p 2222:22 -d limited-sftp user:pass:::uploads #--cpus=".1" --memory="64m"
#docker run -p 2222:22 --cpus=".1" --memory="64m" -d atmoz/sftp user:pass:::uploads