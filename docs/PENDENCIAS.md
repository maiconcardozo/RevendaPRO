# Pendências

O que ficou em aberto quando o desenvolvimento parou para entregar o **MVP**, em
**4 de setembro de 2026**, com o M14 fechado e mesclado na `main`.

Escrito para ser lido meses depois: cada item diz **o que é**, **por que ficou de fora** e
**o que destrava**. Nada aqui é defeito conhecido em produção — o sistema está verde e
conferido contra banco de verdade. É o que falta, e o que foi adiado de propósito.

O histórico completo está em `docs/MARCOS.md`; o roteiro, em `docs/ROADMAP.md`.

---

## 1. O que trava a entrega de verdade

### 1.1 Subida em produção

**O que é.** O sistema roda hoje na máquina de desenvolvimento, com `docker compose up`. Falta a
subida real: um servidor, um domínio e um bucket.

**Por que ficou de fora.** Depende de três decisões de fora do código — **VPS, domínio e conta no
Cloudflare R2**. Nenhuma delas é técnica, e todas custam dinheiro de alguém.

**O que já está pronto.** Tudo o resto. O `docker-compose.prod.yml`, o Caddy emitindo certificado
sozinho, o backup diário para o bucket com retenção e restauração testada, e o roteiro linha por
linha em `docs/operations/deploy.md` — que foi percorrido do zero num projeto isolado, e é o teste
que revelou o defeito de ordem do backup.

**O que destrava.** Contratar VPS e domínio, abrir a conta no R2, preencher o `.env` de produção
e seguir `docs/operations/deploy.md`. É o V3 do M9, e é a única pendência que separa o MVP de
estar no ar.

---

## 2. Risco em aberto

### 2.1 A fonte da FIPE

**O que é.** A consulta automática de tabela usa um **espelho de terceiros**. Ele pode sumir,
mudar de formato ou passar a cobrar.

**Por que continua aberto.** Fonte oficial gratuita e estável não existe. O desenho foi resolvido
no M11 para que o fornecedor deixe de importar.

**As três saídas, já construídas.**

1. A porta `IFipeReference` no domínio: trocar de fornecedor é escrever outra implementação.
2. O interruptor de configuração: a consulta automática desliga sem tocar em código.
3. O valor digitado à mão, que sempre foi legítimo e **jamais** é sobrescrito pela rotina.

**Nenhuma operação do sistema depende da FIPE.** Com a fonte fora do ar, a consulta avisa e o
valor que estava na ficha continua lá.

---

## 3. Marcos próprios, já decididos e adiados

### 3.1 Acesso do parceiro ao próprio pátio

**O que é.** O dono da Loja do Joãozinho entrar no sistema e ver **só os carros que estão com
ele**.

**Por que ficou de fora.** É uma fronteira de segurança nova **dentro** da mesma empresa, e o
sistema hoje só tem a fronteira **entre** empresas (`IdTenant`, provada no M12). Misturar as duas
num marco de cadastro seria a forma mais provável de abrir um vazamento.

**O que ele exige.** Um perfil que enxerga por pátio, e não por tela; e cada consulta de veículo
passando a filtrar por pátio do jeito que hoje filtra por empresa. Anotado no M12, repetido no
M14, e vira marco próprio quando doer.

### 3.2 Recuperar veículo e gasto excluídos

**O que é.** A exclusão é lógica em tudo (RNF-08), mas a **tela de recuperação** só existe para
documento.

**Por que ficou de fora.** O documento tinha um motivo que os outros não têm: o arquivo continuava
**pago e parado no bucket**, inalcançável. Veículo e gasto excluídos ficam apenas invisíveis, e a
linha está lá.

**O que destrava.** Alguém precisar. Hoje se resolve no banco, e a exclusão de veículo pede
confirmação na tela.

### 3.3 Testes de interface

**O que é.** O frontend é conferido por **build** e por **captura de tela**, e não por teste
automatizado.

**Por que ficou de fora.** Com uma pessoa mexendo no frontend, a captura de tela pegou tudo o que
precisava pegar — inclusive um defeito de expressão regular que comia o nome do modelo, e que
nenhum teste de unidade teria visto.

**O que destrava.** Mais de uma pessoa mexendo no frontend.

---

## 4. Deixado de fora do M14, de propósito

Estes vieram da conversa sobre pátios e foram decididos **contra**, com motivo escrito em
`docs/plans/m14-patios.md`.

| Item | Decisão |
|---|---|
| **Carro de terceiro** | O stakeholder foi claro: *"o carro ainda é dele, só está em pátio diferente"*. Custo e lucro seguem como estão. |
| **Endereço e mapa do pátio** | Contato basta para o que a operação faz hoje. |
| **Transferência em lote** | Mover dez carros de uma vez entra quando alguém precisar mover dez carros de uma vez. |
| **Banco separado por cliente** | Resolvido de outro jeito na **ADR-0006**: cliente diferente ganha pilha própria — mesmo código, outro `docker compose -p`, outro banco —, e o `IdTenant` continua por baixo. Revisar por volta de dez clientes. |

---

## 5. Dívidas pequenas de código

Nenhuma delas afeta o que o sistema faz. Ficam anotadas para não virarem folclore.

| Item | Onde | O que é |
|---|---|---|
| **`npm run build` fora do contêiner** | `frontend/` | O `node_modules` local está incompleto nesta máquina, e o build direto falha com `Cannot find module next/dist/bin/next`. **Dentro do Docker passa**, e é esse o caminho do projeto. Um `npm ci` no `frontend/` resolve. |
| **Aviso `CS8602`** | `FipeQuoteReader.cs:204` | O compilador vê `years.Value` como possivelmente nulo na interpolação, embora a linha 195 já tenha usado `years.Value!`. É o `!` de uma linha que não convence o analisador na outra. Cosmético. |
| **Aviso `CS0618`** | `tests/.../ApiFixture.cs:41` | O construtor sem parâmetros do `MariaDbBuilder` foi marcado como obsoleto pelo Testcontainers. Passar a imagem explicitamente tira o aviso e fixa a versão do MariaDB do teste. |
| **Aviso `NU1901`** | `AWSSDK.Core 4.0.1.3` | Vulnerabilidade de **baixa** gravidade conhecida no pacote. Subir a versão quando houver uma corrigida. |

---

## 6. O que **não** está pendente

Para quem chegar aqui achando que falta, e não falta:

- **Isolamento entre empresas.** Provado no M12 com duas revendas montadas pelo próprio sistema:
  leitura e escrita cruzadas respondem 404. Oito vazamentos reais foram encontrados e consertados
  na raiz — ler por código passou a **exigir** a empresa, no contrato do repositório.
- **Matriz perfil × endpoint.** Os 63 endpoints, os cinco perfis e o anônimo, com a API no ar.
- **Backup.** Dump diário para o bucket, com retenção e **restauração testada**.
- **Arquivos.** Fora do banco e fora do disco, com endereço assinado de vida curta; o documento
  excluído continua guardado e recuperável.
- **Custo.** Somado a cada leitura, e jamais guardado.

---

## A suíte, no dia em que isto foi escrito

**503 testes, todos verdes** — 304 de unidade e 199 que sobem a API de verdade contra um banco
descartável em contêiner. As três portas do projeto passam: `dotnet test`, `npm run build` (dentro
da imagem) e `docker compose up --build`.
