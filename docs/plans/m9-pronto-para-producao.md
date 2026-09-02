# Plano — M9: Pronto para produção

Fontes: `docs/ROADMAP.md` (riscos abertos), `docs/architecture/decisions/ADR-0004-armazenamento-de-arquivos.md`
(pendência de backup), e as lacunas anotadas ao fechar o M6 e o M8.

Os RF-01 a RF-24 estão implementados. O que separa o sistema de uma revenda de verdade usando
não é funcionalidade: é o que acontece quando algo dá errado (backup) e onde ele roda (deploy).
Este marco existe para isso, e leva junto três lapidações que ficaram para trás.

## O que a entrega precisa provar

> Um `DELETE` errado às 15h é desfeito às 15h20, com os arquivos junto. E o sistema está num
> endereço que o stakeholder abre no celular, com HTTPS, sem ninguém rodar `docker compose`.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | **concluído** — hospedagem fica para depois; tudo pronto para o R2 por configuração | — |
| **V1** | Backup do banco | Dump diário do MariaDB para o bucket, com retenção; script de restauração | **concluído** — dump restaurado num banco à parte com as 17 tabelas e as mesmas contagens | — |
| **V2** | Backup dos arquivos | Versionamento ligado no bucket (MinIO e R2 suportam); o `DELETE` vira versão, e não sumiço | **concluído** — foto apagada pela API recuperada pela versão anterior, byte a byte | V1 |
| **V3** | Foto do usuário no bucket | Avatar passa a usar `IFileStorage`; `DiskPhotoStorageService` e o volume `revendapro_files` saem; migração dos arquivos existentes | **concluído** — avatar entra como WebP de 320 px em `{tenant}/users/{código}/`; o volume `revendapro_files` saiu; havia zero fotos para migrar | — |
| **V4** | Foundation | `DateOnlyTypeHandler` sobe para o pacote; RevendaPro consome a release | **concluído** — rc.5 empacotada com o binário conferido; RevendaPro passou a consumi-la e o handler local saiu | — |
| **V5** | Deploy | Compose de produção (sem MinIO, R2 por variável), proxy com HTTPS, variáveis documentadas, `/health` monitorado, log em arquivo com rotação | O stakeholder abre o endereço no celular e entra | V1, V2, V3 |
| **V6** | Checklist | Roteiro de subida e de restauração, testado do zero numa máquina limpa | Alguém sem o histórico da conversa sobe o sistema seguindo o documento | V5 |

## Decisões que precisam ser tomadas (V0)

Cada uma muda o V5, e nenhuma se resolve pelo código:

**1. Onde hospedar.** Uso 100% interno, poucos usuários, um banco pequeno. A opção mais barata
e mais simples de operar é **uma VPS** (Hetzner, DigitalOcean, Contabo — na faixa de US$ 5–12
por mês) rodando o mesmo `docker compose`, com **Caddy** na frente fazendo HTTPS sozinho pelo
Let's Encrypt. Alternativas gerenciadas (Fly.io, Railway) tiram o trabalho de manter a máquina
e cobram por isso; o banco gerenciado deles é o item mais caro.

Recomendação: **VPS + compose + Caddy**, com o MariaDB no próprio compose e o backup indo para o
R2. É o que uma pessoa mantém sozinha.

**2. Domínio.** Precisa de um, para o HTTPS e para o endereço que o stakeholder decora. Pode ser
subdomínio de um que você já tenha.

**3. Conta no Cloudflare R2.** Dois buckets (`revendapro-public`, `revendapro-private`), um
token com permissão de leitura e escrita, e o domínio do CDN para o `PublicUrl`. O código já
espera exatamente isso — ver `StorageSettings`.

**4. Retenção do backup.** Proposta: dump diário, guardar 30 dias; um dump mensal guardado por
um ano. O bucket versionado guarda toda versão de arquivo por 90 dias.

## O que fica de fora deste marco

Os itens seguintes viram marcos próprios, na ordem que o roteiro sugere:

- **M10 — Linha do tempo e filtros.** RF-26 pede um histórico único da operação (compra, gastos,
  anexos, propostas, status, venda); hoje cada um vive na sua aba. RF-25 pede filtro por período
  na listagem. Entra aqui também a rotina administrativa do documento excluído que fica no bucket.
- **M11 — FIPE.** Consulta automática pelo `FipeCode`, quando houver fonte estável ou paga.
- **Testes de front.** Hoje o front é conferido pelo build e por capturas. Um marco de testes de
  interface faz sentido quando houver mais de uma pessoa mexendo nele.
