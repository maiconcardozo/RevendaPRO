# Os marcos do Revenda Pro

O que foi construído, em que ordem, por qual motivo, e o que ficou aberto. Escrito para quem
chega agora: cada marco diz o que entregou, qual decisão o moldou e como ele foi conferido.

O roteiro original está em `docs/ROADMAP.md`; os planos detalhados, em `docs/plans/`. Este
documento é a leitura de cima, do começo ao estado de hoje — **4 de setembro de 2026**.

> Versão em página, para ler e compartilhar: https://claude.ai/code/artifact/f885a6b8-5dce-45ab-aa3e-4eb99e650408

---

## O sistema, em um parágrafo

O Revenda Pro é o sistema de uma revenda que **compra, recupera e vende** veículos, boa parte
vinda de leilão. Ele responde três perguntas que hoje moram numa planilha e na cabeça de quem
toca a operação: *quanto este carro já custou de verdade*, *por quanto ele precisa sair* e *o
que aconteceu com ele desde que entrou no pátio*. A primeira fase é de uso interno.

## Como o trabalho foi organizado

Uma **branch por marco** (`M6`, `M8`, `M9`, `M10`) e um **commit por versão** dentro dele
(`V0`, `V1`, …). O `V0` de cada marco é o plano escrito antes de qualquer código, com as
decisões que precisavam ser tomadas. O último `V` fecha a suíte e a documentação. Nada é dado
por pronto sem `dotnet test`, `npm run build` e `docker compose up --build` passando.

---

## Panorama

| Marco | Entrega | Estado |
|---|---|---|
| **M0** | Higienização da base e as decisões de arquitetura | concluído |
| **A0–A5** | Acesso: empresa, usuário, perfil, permissão por tela, login, menu, telas de administração | concluído |
| **A6** | Testes do acesso | concluído no M12 |
| **M6** | Veículo, custo, gastos, fotos e documentos | concluído |
| **M8** | Proposta, venda, troca e painel | concluído |
| **M9** | Pronto para produção: backup, arquivos no bucket, deploy | concluído, faltando a subida real |
| **M10** | Linha do tempo, filtro por período e documentos excluídos | concluído |
| **M11** | Consulta automática da FIPE e a tela Mercado | concluído |
| **M12** | A matriz perfil × endpoint e o isolamento entre empresas, com a API no ar | concluído |
| **M13** | Faxina: configuração que engana, dependência morta e o número dos dias | concluído |
| **M14** | Pátios: onde cada carro está, a passagem registrada e o relatório de cada lugar | concluído |

O M7 deixou de existir: custo era um módulo à parte no roteiro antigo, e o M6 mostrou que
custo é leitura do veículo. Quem cadastra o carro é quem lança o gasto.

---

## M0 — A base, e as decisões que sustentam tudo

Antes de qualquer funcionalidade, quatro decisões foram escritas como ADR, e elas explicam
quase todo o código que veio depois:

- **ADR-0002 — permissão é tela.** O sistema descartou a ideia de chaves de permissão em texto
  livre. Cada tela é uma permissão, e a chave da tela é a permissão. Declarar uma linha no
  `ScreenCatalog` cria a permissão, concede ao Administrador e coloca o item no menu — sem
  migration e sem SQL na mão. O menu de cada pessoa é o que ela pode abrir.
- **ADR-0003 — o padrão Global.** Código e comentário em inglês, texto de tela em português.
  Chave primária `Id` inteira com um `Code` (UUID v7) público. Entity Framework **só** para
  schema e mapeamento; leitura e escrita com Dapper. Envelope `SuccessDetails<T>` nas
  respostas.
- **ADR-0004 — armazenamento de arquivos.** Nenhum arquivo no banco e nenhum arquivo em disco:
  tudo em bucket S3, com endereço assinado de vida curta. MinIO no desenvolvimento, Cloudflare
  R2 na produção — a diferença é configuração.
- **ADR-0001** ficou substituída pela ADR-0002.

A escolha do Dapper trouxe um risco junto: o Entity Framework esconde a linha excluída
sozinho, e o Dapper não. Por isso existe um teste que **inspeciona cada SELECT escrito à mão** e
exige o filtro de exclusão lógica. Hoje só quatro consultas leem linha excluída de propósito, e
cada uma tem o motivo escrito no próprio teste.

## A0–A5 — Acesso

Empresa, usuário, perfil, permissão por tela, auditoria. Senha com hash forte, JWT com chave e
expiração por variável de ambiente, refresh token com rotação e revogação. No frontend, a
sessão saiu do `localStorage` e virou cookie httpOnly; o menu passou a ser montado pelo
servidor a partir das telas que a pessoa tem; rota do painel nenhuma abre sem login.

Cinco perfis nascem com o sistema: **Administrador** (todas as telas, inclusive as que
surgirem), **Gestor**, **Financeiro**, **Vendedor** e **Oficina**. Perfil de sistema é
permanente: ele pode ganhar e perder telas, e jamais ser excluído.

## M6 — Veículo, custo e arquivos

O coração da operação.

- **Veículo** com placa e chassi únicos por empresa, esteira de situação validada no domínio, e
  origem (leilão, particular, loja, troca).
- **Gasto** lançado por quem cuida do carro, com tipo, data e a marca de *pago* ou *previsto*.
  O tipo de gasto é **tabela mantida pela revenda**, com palavras-chave que sugerem o tipo a
  partir do que a pessoa digitou — quem digita "balanceamento" cai em Alinhamento sem nunca ter
  cadastrado a palavra.
- **Custo somado a cada leitura, jamais guardado.** Essa foi a decisão mais importante do
  marco, e ela veio de um defeito na planilha real: o total tinha sido digitado uma vez, três
  gastos entraram embaixo dele depois, e o documento seguia mostrando **R$ 350 a menos** do que
  o carro tinha custado. Um total guardado está certo até o próximo gasto, e errado a partir
  dali, em silêncio.
- **Teto de orçamento** por carro, com quanto ainda cabe e aviso de estouro previsto **antes**
  de a despesa ser paga.
- **Fotos e documentos** fora do banco: foto vira WebP em três tamanhos, o tipo é julgado pelos
  primeiros bytes do arquivo (e nunca pela extensão), e o limite de tamanho é configurável —
  12 MB por padrão.
- **Documento excluído continua no bucket**, por requisito: uma revenda responde pelo que
  vendeu anos depois. Foto excluída sai de verdade.

**Verificado** contra o `GASTOS.docx` real do stakeholder: o Cruze com os 21 gastos fecha em
R$ 37.994.

## M8 — Proposta, venda, troca e painel

- **Proposta** com quem ofereceu, quanto, como paga e por qual canal — e **quanto sobra se ela
  for aceita**, calculado na hora, antes de qualquer coisa ser gravada.
- **Venda** com preço fechado, comprador, canal, repasse da loja parceira, comissão e troca.
  O repasse entra **por cima** do que o vendedor quer receber, que foi exatamente como o
  stakeholder descreveu: *"eu quero 58 para mim, a loja põe a dela em cima"*.
- **Troca** cria um veículo novo no estoque, com origem *Troca* e o valor acordado como compra.
- **"Vendido" tem uma porta só**: registrar a venda. A mudança de situação recusa esse destino,
  o que impede um carro marcado como vendido sem venda por trás.
- **Painel** com capital parado, contagem por situação, lucro projetado e realizado, e os cinco
  carros de maior investimento, maior sobra prometida e mais tempo parado.
- **FIPE segue manual**, por decisão: o único acesso gratuito é um espelho comunitário sem
  contrato. O código FIPE já é guardado desde o M6, e é ele que vai deixar a integração barata.

**Verificado** ponta a ponta: o Cruze que custou R$ 37.994, vendido por 55 com 20 em carro,
deixa os mesmos R$ 17.006 que a proposta prometia; o carro da troca nasce no pátio a 20 mil.

## M9 — Pronto para produção

O marco em que o sistema deixou de depender da máquina onde roda.

- **Backup do banco**: dump diário para o bucket, retenção de 30 dias no diário e um ano no
  mensal, e um script de restauração que exige confirmação para sobrescrever a produção.
- **Backup dos arquivos**: versionamento ligado no bucket privado. Apagar um arquivo passa a
  criar uma versão anterior, e não um sumiço.
- **A foto do usuário saiu do disco** e foi para o bucket. Com ela, o último arquivo do sistema
  — nenhum volume de arquivo sobra no compose.
- **Produção tem compose próprio**: sem MinIO, R2 por variável, Caddy emitindo o certificado
  sozinho, só as portas 80 e 443 saindo da máquina, usuários de demonstração desligados e log
  com rotação.
- Um utilitário genérico (`DateOnlyTypeHandler`) subiu para o pacote **Foundation.Base**, sem
  nada do Revenda Pro dentro.

**Verificado** subindo a pilha do zero, num projeto isolado, seguindo o `deploy.md` linha por
linha. Foi esse teste que revelou um defeito de ordem: o backup rodava antes de a API criar as
tabelas, e o operador via ERRO num deploy correto. Hoje a primeira rodada espera o schema.

**Falta a subida real**, que depende de VPS, domínio e conta no Cloudflare R2.

## M10 — Linha do tempo, período e a porta de volta

- **Linha do tempo do veículo**: compra, gastos, anexos, propostas, mudanças de situação e
  venda, numa aba só e em ordem. Lida das tabelas da operação, e jamais da auditoria — a
  auditoria existe para perícia e guarda JSON, e a ficha precisa de significado. Fotos e
  documentos enviados pela mesma pessoa no mesmo dia entram contados num evento só.
- **Filtro por período** na listagem de veículos, pela data de compra. Quem quer o que saiu tem
  a tela de Vendas, que filtra pela data da venda.
- **Documentos excluídos**: tela administrativa que lista, abre e devolve à ficha. Não existe
  apagar de vez, e a ausência é o desenho — guardar documento para sempre foi requisito, e o
  arquivo nunca saiu do bucket. Na primeira vez que a tela rodou, ela desenterrou 13 arquivos
  que estavam pagos e inalcançáveis desde o M6.

---

## M11 — A tabela consultada sozinha, e a negociação medida contra ela

Desde o M6 o veículo guardava **valor**, **mês** e **código FIPE**, os três digitados à mão.
O código foi guardado justamente para este marco: com ele, o preço vem em uma chamada.

**A FIPE não publica API.** O acesso oficial é o site, um modelo por vez; o que existe são
espelhos de terceiros, e qualquer um deles pode sumir, mudar de forma ou passar a cobrar. Por
isso a consulta entra **atrás de uma porta no domínio**, com o adaptador na infraestrutura —
a mesma forma do armazenamento de arquivos. Trocar de fonte é uma classe nova, e nada mais do
sistema fica sabendo. Um interruptor de configuração devolve o sistema ao valor digitado à
mão, e nada toca a rede.

- **A tabela sugere; o preço é da pessoa.** A consulta escreve valor, mês, modelo e origem —
  e **nenhum campo de preço**. `Quero receber`, `Mínimo aceito` e `Anunciado` continuam sendo
  de quem entende do carro. Um teste segura essa frase.
- **Cotações guardadas por modelo e mês.** Dez carros do mesmo Cruze custam **uma** consulta,
  e um mês já buscado jamais volta à rede. Uma cotação de mês fechado **jamais muda**: ela é
  fato histórico, e a entidade tem fábrica e nenhum método de instância.
- **O mês é sempre fixado nas consultas de preço.** Duas chamadas à mesma fonte, no mesmo
  minuto, chegaram a devolver meses diferentes para o mesmo carro. O mês guardado é o que a
  resposta trouxer, e jamais o mês em que se perguntou.
- **Achar o modelo em três escolhas.** Marca, modelo e ano, para o carro que ainda não tem
  código. Da segunda vez em diante a consulta é direta.
- **O pátio se atualiza sozinho**, uma vez por mês, e respeita o valor digitado à mão: carro
  raro ou fora da tabela é precificado por quem conhece aquele mercado. Valor velho aparece
  marcado na ficha e na listagem.
- **Tela Mercado**: compra, venda, pedido e propostas, cada um contra a tabela **do mês
  daquele negócio** — e a perda de referência de quem está parado, que é o custo de segurar
  o carro. Comparar uma venda de agosto com a tabela de hoje mediria a passagem do tempo e
  chamaria isso de resultado.

O Cruze fechou o marco como o plano prometeu: **vendido por R$ 60.000 quando a tabela do mês
dizia R$ 56.530 — 6,14% acima**.

**Limite honesto:** o sistema guarda de agora em diante. Do passado, só o que a fonte
devolver — e a faixa gratuita devolve três meses. Negócio anterior a isso aparece como *sem
comparação*, e fica de fora das médias.

---

## M12 — A fechadura, provada trancando

O marco de acesso deixou uma dívida escrita: a **matriz perfil × endpoint**, que o próprio plano
chamava de *"o teste que impede regressão de segurança"*.

Até aqui a guarda era **estática**: um teste percorre a montagem da API e exige que todo
endpoint declare a tela que o protege, inclusive os criados amanhã. Isso prova que a fechadura
está **instalada**. Jamais provou que ela **tranca**.

- **A API sobe de verdade** num teste, contra um MariaDB descartável em contêiner. Banco em
  memória estava fora: o acesso a dado é Dapper com SQL escrito à mão, e um SQLite responderia
  a um SQL que não é o nosso. `dotnet test` continua sendo **um comando**.
- **A matriz cobre os 63 endpoints nos cinco perfis**, e a expectativa é **derivada** das telas
  que o próprio sistema diz que cada perfil alcança. Lista escrita à mão envelhece no primeiro
  endpoint novo, e envelhece em silêncio.
- **Uma segunda lista, curta e à mão**, declara em português o que cada perfil jamais alcança.
  Ela existe porque a matriz derivada tem um limite conhecido: trocar a *etiqueta* da tela
  deixaria tudo verde e abriria o Mercado para o Vendedor — conferido por mutação.
- **O isolamento entre empresas ganhou teste próprio**, com duas revendas montadas pelas
  próprias entidades do sistema.

**E ele encontrou defeito de verdade.** Oito handlers liam pelo código público sem filtrar a
empresa. O pior respondeu **204**: o administrador de uma revenda excluiu o usuário de outra.
Os demais deixavam editar, bloquear, restaurar e trocar a foto de gente de outra revenda, e
editar e excluir o **perfil** dela — e perfil concede tela.

Curiosidade que explica o defeito: um dos handlers **já conferia** a empresa. Alguém viu o risco
naquele caminho e corrigiu só ali — que é o que acontece quando a regra vive na disciplina de
cada handler, e não no contrato. Por isso o conserto foi na raiz: ler por código agora **pede**
a empresa.

---

## M13 — Faxina

Quatro itens pequenos, todos do mesmo tipo: **coisa que mente para quem lê**.

- **`next-auth` saiu do `package.json`.** Estava lá desde antes de a sessão virar cookie
  httpOnly com o JWT da API, e tinha zero referências no código.
- **Seis chaves do `appsettings.json` saíram.** `Cors:Origens`, `Jwt:Emissor`,
  `Jwt:Audiencia` e as outras não mapeavam para propriedade nenhuma desde a ADR-0003 — eram
  inertes, e valiam os padrões do código. O que ficou tem o nome certo e é lido de verdade.
- **As divergências 6 e 7 do ROADMAP**, abertas desde o M0, foram fechadas dizendo **como** se
  resolveram. A 7 já estava resolvida havia tempo: hoje é EF Core 10.0.5 com o provider da
  Oracle, e o Pomelo ficou de fora por escrito.
- **O carro vendido parou de contar dias.** A listagem dizia "ficou 63 dias no pátio" para um
  carro vendido em 02/09, com o número crescendo toda manhã, enquanto a faixa da venda na mesma
  tela dizia 61. `DaysInStock` passou a exigir os dois lados — hoje e o dia da saída —, sem
  valor padrão, porque era justamente um padrão silencioso que fazia todo chamador novo repetir
  o defeito.

Fecha o marco a **ADR-0006**, que registra a decisão de isolamento por cliente: cliente
diferente ganha pilha própria — mesmo código, outro `docker compose -p`, outro banco —, e o
`IdTenant` continua por baixo, porque tirá-lo seria trabalho para perder uma opção.

---

## M14 — Pátios, e o relatório de cada lugar onde o carro está

Veio de uma frase do stakeholder sobre como a operação dele realmente é:

> *"O Rodrigo tem o pátio particular dele, que anuncia, e ele deixa outros carros em outras
> revendas. Ele precisa tirar relatório de cada pátio ou revenda, e um todo junto, mas sempre
> agrupado."*

Até aqui o sistema sabia em que **etapa** cada carro estava, e nada sobre **onde** ele estava. A
loja de terceiro existia como um texto digitado à mão em cada venda, redigitado a cada negócio
e impossível de agrupar.

**Um cadastro só, com o tipo dentro.** Pátio próprio e loja de terceiro não viraram duas
tabelas — foi o que ele descreveu (*"tudo seria pátio"*), e é o que mantém a soma possível: dois
cadastros exigiriam somar duas coisas diferentes em todo relatório, e alguém acabaria somando só
uma. O tipo muda o repasse, e só isso.

**O carro está em um lugar por vez**, numa coluna do veículo. Uma tabela de ligação abriria a
porta para um estado que a operação não tem.

**A mudança de pátio é evento.** Ela entra na linha do tempo do M10 como um tipo novo, com o de
onde, o para onde, o motivo, a hora e quem fez. É o que responde *"esse carro ficou dois meses
na Loja do Joãozinho e voltou sem vender"* — a informação que decide se vale deixar carro lá de
novo. As duas chaves da passagem para o pátio são **restrict**: um pátio que sai do cadastro
jamais apaga a história de quem passou por ele.

**O relatório agrupa, e jamais troca o total pelo pedaço.** O painel ganhou o bloco *Por pátio*
— carros, capital parado e tempo médio em cada lugar — e os números do topo continuam somando o
estoque inteiro. A frase dele foi explícita: *"de cada um e um todo junto"*. Pátio vazio fica na
lista, porque "zero carro na Loja do Joãozinho" é uma resposta, e os carros sem lugar ganham
linha própria.

**O repasse é sugerido, e continua sendo decidido por quem vende.** A venda de um carro que está
na loja de um parceiro já chega com o canal, o nome da loja e o repasse combinados no cadastro —
e os três editáveis. O cálculo do negócio, que é do M8, não mudou em nada. Mesmo raciocínio da
FIPE no M11: o sistema sugere pela presença, e quem decide dinheiro é a pessoa.

Duas coisas de segurança ficaram do jeito que estavam de propósito: o filtro por pátio com um
código que a empresa desconhece responde **lista vazia**, e nunca o estoque inteiro; e mover um
carro para o pátio de outra revenda responde **404**, porque o pátio é procurado por código
**e** por empresa, juntos.

Cadastrar pátio e mover carro exigem a tela **Pátios**. Ver onde o carro está, não: isso vem na
ficha para quem tem a tela de veículos. Ler é informação, mover é decisão.

De faxina, a classe interna `Yard` do painel virou `Stock`. Pátio virou entidade de verdade
neste marco, e duas coisas com o mesmo nome no mesmo arquivo é como se lê errado.

---

## O que continua aberto

| Item | Por que ainda está aberto |
|---|---|
| **Subida em produção** (M9) | Depende de VPS, domínio e conta no R2. O compose, o HTTPS e o roteiro estão prontos e testados. |
| **Fonte da FIPE** | O espelho é de terceiros, e pode sumir ou passar a cobrar. As três saídas estão prontas: a porta no domínio, o interruptor de configuração e o valor digitado à mão. |
| **Acesso do parceiro ao próprio pátio** | O dono da loja onde o carro está poderia entrar e ver só os carros que estão com ele. É uma fronteira de segurança nova **dentro** da mesma empresa, que hoje o sistema não tem — marco próprio quando doer. |
| **Testes de interface** | O frontend é conferido por build e por captura de tela. Um marco de testes de interface faz sentido quando houver mais de uma pessoa mexendo nele. |
| **Recuperação de veículo e gasto excluídos** | A exclusão lógica vale para tudo, mas só o documento tinha arquivo pago parado no bucket. As outras entram quando alguém precisar. |

## A suíte, hoje

503 testes, todos verdes — 304 de unidade e 199 que sobem a API de verdade contra um banco
descartável em contêiner. Os que mais seguram o sistema:

- **arquitetura** — nenhuma camada olha para quem ela não deve;
- **exclusão lógica** — cada SELECT escrito à mão precisa filtrar linha excluída;
- **guarda da API** — todo endpoint declara a tela que o protege;
- **regras de venda e de veículo** — a esteira, a porta única para "Vendido", o cálculo da
  sobra, a troca;
- **arquivos** — o endereço assinado confere contra o próprio endereço, e o documento excluído
  continua baixando enquanto a foto excluída some;
- **tabela de referência** — a fonte responde com respostas de verdade gravadas, e nenhum
  teste toca a rede: fora do ar, estourada de limite ou em formato novo, ela devolve um
  resultado tratado. E a consulta jamais encosta num campo de preço;
- **matriz perfil × endpoint** — os 63 endpoints, os cinco perfis e o anônimo, com a API no ar:
  quem tem a tela passa, quem não tem leva 403, e sem token tudo responde 401;
- **isolamento entre empresas** — duas revendas montadas pelo próprio sistema, e uma jamais
  alcança o dado da outra, nem lendo nem escrevendo;
- **pátios** — o repasse combinado de um jeito só, o pátio da casa que jamais cobra da casa, a
  passagem registrada com o de onde veio, o filtro pedido ao banco e não peneirado em memória, e
  o painel somando cada lugar sem parar de somar o todo.
