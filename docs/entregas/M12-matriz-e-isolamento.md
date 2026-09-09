# M12 — A fechadura, provada trancando

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m12-matriz-perfil-endpoint.md`.

## O que este marco resolve

O marco de acesso deixou uma dívida escrita: a **matriz perfil × endpoint**, que o próprio plano
chamava de *"o teste que impede regressão de segurança"*. Até aqui a guarda era **estática** — um
teste exigia que todo endpoint declarasse a tela que o protege. Isso prova que a fechadura está
**instalada**; jamais provou que ela **tranca**.

## As regras

### A API sobe de verdade no teste, contra um banco real descartável

MariaDB em contêiner, e `dotnet test` continua sendo **um comando**.

**Por que é essa:** o acesso a dado é Dapper com SQL escrito à mão. Um banco em memória
responderia a um SQL que **não é o nosso**, e o teste passaria a medir o SQLite.

### A expectativa da matriz é derivada, e jamais escrita à mão

Os 63 endpoints nos cinco perfis, com o esperado calculado das telas que o próprio sistema diz
que cada perfil alcança.

**Por que é essa:** uma lista escrita à mão envelhece no primeiro endpoint novo — e envelhece em
**silêncio**, ficando verde justamente sobre o que ninguém conferiu.

### Uma segunda lista, curta e em português, diz o que cada perfil jamais alcança

**Por que é essa:** a matriz derivada tem um limite conhecido: trocar a *etiqueta* da tela
deixaria tudo verde e abriria o Mercado para o Vendedor. A lista curta é o contrapeso, e foi
conferida por mutação — quebrando a regra de propósito, para ver o teste ficar vermelho.

### Ler por código público **pede** a empresa

A assinatura passou a exigir o tenant. A regra saiu da disciplina de cada handler e foi para o
contrato.

**Por que é essa:** é onde o marco encontrou defeito de verdade, e uma regra que depende de cada
autor lembrar dela já falhou em algum lugar — só ninguém sabe ainda em qual.

### O isolamento entre empresas tem teste próprio

Duas revendas montadas pelas próprias entidades do sistema.

**Por que é essa:** dado semeado direto no banco provaria o teste, e nada sobre o caminho que a
aplicação percorre.

## Como usar

`dotnet test`. A matriz e o teste de isolamento fazem parte da suíte, e rodam a cada mudança.

## O que mudou por baixo

- Testes de integração com Testcontainers; a matriz derivada; a lista curta em português; o
  teste de isolamento; e a assinatura de leitura por código, agora exigindo a empresa.

## O que foi conferido

**Ele encontrou defeito de verdade.** Oito handlers liam pelo código público sem filtrar a
empresa. O pior respondeu **204**: o administrador de uma revenda excluiu o usuário de outra. Os
demais deixavam editar, bloquear, restaurar e trocar a foto de gente de outra revenda, e editar e
excluir o **perfil** dela — e perfil concede tela.

Curiosidade que explica o defeito: um dos handlers **já conferia** a empresa. Alguém viu o risco
naquele caminho e corrigiu só ali — que é o que acontece quando a regra vive na disciplina, e não
no contrato. Por isso o conserto foi na raiz.

## O que ficou de fora, e por quê

- **Teste de carga e de negação de serviço**: outra classe de risco, sem pedido do negócio, e o
  sistema roda hoje numa rede local fechada.
