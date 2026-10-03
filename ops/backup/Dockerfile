# Backup do banco para o bucket.
#
# Parte da imagem do MariaDB porque ela ja traz o mariadb-dump da versao exata do servidor -
# um dump feito por um cliente de outra versao e a forma classica de descobrir, na hora de
# restaurar, que o formato mudou. O rclone vem copiado da imagem oficial, e fala S3 com
# qualquer fornecedor: MinIO local hoje, Cloudflare R2 quando publicar.
#
# Era o mc, o cliente do MinIO, ate outubro de 2026: a MinIO arquivou o projeto aberto e tirou
# as imagens e os binarios do ar, e o build passou a falhar com "pull access denied". O rclone
# e um binario estatico, mantido, e independe de fornecedor de armazenamento.
#
# Sem apt-get de proposito: a imagem do MariaDB ja tem gzip e os certificados, e uma rede
# que bloqueia o espelho do Ubuntu na porta 80 deixaria o build sem backup nenhum.
FROM rclone/rclone:1.71.1 AS rclone

FROM mariadb:11.8

COPY --from=rclone /usr/local/bin/rclone /usr/local/bin/rclone
COPY storage.sh backup.sh restore.sh run.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/rclone /usr/local/bin/backup.sh /usr/local/bin/restore.sh /usr/local/bin/run.sh

ENTRYPOINT ["/usr/local/bin/run.sh"]
