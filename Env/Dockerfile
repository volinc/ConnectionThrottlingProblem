FROM atmoz/sftp

COPY sshd_config /etc/ssh/sshd_config
CMD ["sftp-server", "user:password:1001"]

#RUN apt-get update && apt-get install -y openssh-server stress && stress -c 4