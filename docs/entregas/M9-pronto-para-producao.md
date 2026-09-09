# M9 — Pronto para produção: backup, arquivos no bucket, deploy

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m9-pronto-para-producao.md`.

## O que este marco resolve

O marco em que o sistema deixou de depender da máquina onde roda. Até aqui ele funcionava; a
partir daqui ele **sobrevive** — a um disco que morre, a um arquivo apagado por engano, e a uma
troca de servidor.

## As regras

### Durabilidade de bucket não é backup

O banco tem dump diário para o bucket, com retenção de 30 dias no diário e um ano no mensal. A
restauração é script, e **exige confirmação** para sobrescrever a produção.

**Por que é essa:** um bucket durável protege contra o disco que falha, e contra mais nada. Ele
replica com igual fidelidade o `DROP TABLE` de ontem. Backup é uma cópia no tempo, e a
confirmação existe porque restaurar por engano é o segundo desastre.

### Apagar um arquivo cria uma versão anterior, e não um sumiço

Versionamento ligado no bucket privado.

**Por que é essa:** o M6 já garantia que o documento excluído continuava no bucket; o
versionamento cobre o caso em que o objeto é **sobrescrito ou removido** por fora do sistema.

### Produção tem compose próprio

Sem MinIO, R2 por variável, Caddy emitindo o certificado sozinho, só as portas 80 e 443 saindo
da máquina, usuários de demonstração desligados e log com rotação.

**Por que é essa:** um compose único, com `if` de ambiente, é o arquivo que ninguém lê inteiro
antes de subir. Dois arquivos são duas leituras curtas, e a diferença entre eles é a
documentação do que muda em produção.

### O que é genérico sobe para a Foundation, sem nada do Revenda Pro dentro

O `DateOnlyTypeHandler` foi o primeiro.

**Por que é essa:** o pacote compartilhado só serve enquanto for genérico. Uma regra da revenda
lá dentro faz o próximo projeto herdar uma decisão que não é dele.

## Como usar

O roteiro está em `docs/operations/deploy.md`, linha por linha, e o backup em
`docs/operations/backup.md`.

## O que mudou por baixo

- Serviço de backup no compose, com script de restauração.
- A foto do usuário saiu do disco e foi para o bucket — com ela, o último arquivo do sistema:
  volume de arquivo nenhum sobra no compose.
- `docker-compose.prod.yml`, `Caddyfile` de produção.

## O que foi conferido

Subindo a pilha **do zero**, num projeto isolado, seguindo o `deploy.md` linha por linha. Foi
esse teste que revelou um defeito de ordem: o backup rodava **antes** de a API criar as tabelas,
e o operador via ERRO num deploy correto. Hoje a primeira rodada espera o schema.

## O que ficou de fora, e por quê

- **A subida real**, que depende de VPS, domínio e conta no Cloudflare R2 — decisões do dono, e
  não do código. O sistema roda hoje num servidor da rede local, e o que o M20 mudou nele está
  em `docs/operations/celular-na-rede.md`.

## Pendente

A subida em produção continua aberta, e é o item 1.1 de `docs/PENDENCIAS.md`.
