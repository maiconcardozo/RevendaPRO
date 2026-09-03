# Backup do banco para o bucket.
#
# Parte da imagem do MariaDB porque ela ja traz o mariadb-dump da versao exata do servidor -
# um dump feito por um cliente de outra versao e a forma classica de descobrir, na hora de
# restaurar, que o formato mudou. O mc (cliente do MinIO) vem copiado da imagem oficial, e
# fala S3 com qualquer fornecedor: MinIO local hoje, Cloudflare R2 quando publicar.
#
# Sem apt-get de proposito: a imagem do MariaDB ja tem gzip e os certificados, e uma rede
# que bloqueia o espelho do Ubuntu na porta 80 deixaria o build sem backup nenhum.
FROM minio/mc:RELEASE.2025-04-16T18-13-26Z AS mc

FROM mariadb:11.8

COPY --from=mc /usr/bin/mc /usr/local/bin/mc
COPY backup.sh restore.sh run.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/mc /usr/local/bin/backup.sh /usr/local/bin/restore.sh /usr/local/bin/run.sh

ENTRYPOINT ["/usr/local/bin/run.sh"]
