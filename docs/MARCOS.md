# Os marcos do Revenda Pro

O que foi construído, em que ordem, por qual motivo, e o que ficou aberto. Escrito para quem
chega agora: cada marco diz o que entregou, qual decisão o moldou e como ele foi conferido.

O roteiro original está em `docs/ROADMAP.md`; os planos detalhados, em `docs/plans/`. Este
documento é a leitura de cima, do começo ao estado de hoje — **8 de setembro de 2026**.

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
| **M15** | O botão que acha o modelo na tabela sozinho, e pergunta só o que sobrar | concluído |
| **M16** | A escolha é sempre da pessoa: o pop-up abre sempre, o medidor de acurácia, o desvincular e o pátio de demonstração | concluído |
| **M17** | O mesmo pátio de dois jeitos: mosaico e lista, com a escolha guardada | concluído |
| **M18** | Fornecedores: de quem cada gasto foi, o ramo como cadastro, o ranking no painel e a ficha de cada um | concluído |
| **M19** | Relatórios: a ficha do carro para venda e a proposta para o cliente em PDF, e as planilhas em Excel ou CSV | concluído |
| **M20** | A proposta pelo WhatsApp, do computador e do celular: o PDF anexado pela folha do aparelho, HTTPS na rede e o ícone na tela inicial | concluído, publicação na rede pendente |
| **M21** | Clientes: quem ofereceu, quem comprou, quem volta — a proposta e a venda apontam para uma pessoa, o CPF no papel, e a ficha do carro novo para quem já comprou | concluído, publicação na rede pendente |
| **M22** | Caixa: o que vence, o que entrou, o que atrasou — o gasto com prazo, a despesa da loja, o que falta receber de cada venda, e a baixa em um clique | concluído, publicação na rede pendente |

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

## M15 — O botão acha o modelo sozinho

Nasceu de um tropeço de verdade. Depois de cadastrar dez carros, o stakeholder perguntou onde
tinha ido parar o botão de consultar a tabela: ele só aparecia no carro que **já tinha código**, e
a ficha mostrava `Código —` sem ligar uma coisa à outra.

A pergunta seguinte foi melhor que a queixa:

> *"Não tem como colocar essa implementação no botão?"*

**A tabela de referência não tem busca por texto**, e o que ela chama de "modelo" é a versão
inteira: a Jeep responde 110 modelos, 32 deles com a palavra *Renegade*. Transformar
"Jeep / Renegade / 1.8 Longitude" numa dessas linhas é jogar fora o que não pode ser, e mostrar o
que sobrou.

**O casador elimina, e jamais adivinha.** O nome do modelo é exigido como palavra inteira, e o
resto — os termos da versão, o câmbio, o combustível — só pontua. Sobrevive o grupo de maior
pontuação. Sobrando zero depois de um descarte, ele volta um passo: oferecer quatro candidatos
vale mais do que oferecer nenhum.

Duas descobertas do mundo real viraram regra:

- **O nome tem de casar como palavra inteira**, porque `gol` cabe dentro de `golf` — e um Golf
  custa quase o dobro de um Gol.
- **O câmbio manual é reconhecido pela ausência da marca.** Das duas linhas que a tabela tem para
  o Gol 1.6 MSI, uma diz `Flex 16V 5p Aut.` e a outra diz `Flex 8V 5p` e não diz mais nada.
  Procurar a palavra `Mec.` ali acharia nada, e separaria nenhuma das duas.

**Empate jamais vira palpite.** Duas versões do mesmo carro são dois preços, e essa escolha é de
quem conhece o carro: sobrando mais de um, abre um modal com o que sobrou, e o nome vai inteiro
como a tabela escreve.

Medido contra a tabela de verdade, nos dez carros do pátio: **cinco resolveram sozinhos** —
Corolla, Renegade, Civic, Mobi e Kicks —, quatro viraram escolha entre um e quatro candidatos, e
um pegou a fonte fora do ar naquele instante. O Chevrolet Onix é o retrato do ganho: de **38
modelos com o nome para dois na tela**, LT e LTZ, os dois de 2017 Flex.

Quando resolve sozinho, a escrita sai pela **mesma porta** que a pessoa usaria — o comando do
escolhedor —, e não por um caminho paralelo. Assim o código gravado, a cotação guardada e a
auditoria saem iguais nos dois casos.

> A gravação automática do candidato único **caiu no M16**, e o parágrafo acima vale como
> história. Ver a seção seguinte.

---

## M16 — A escolha é sempre da pessoa, e dá para desfazer

O M15 tinha uma frase que parecia óbvia: *"sobrando um candidato com um ano só, o sistema grava,
porque escolha nenhuma restou para fazer"*. O uso mostrou o furo em uma semana.

> *"Eu estou querendo deixar aquela parte onde consulta agora ele fazer para todos, mesmo se
> achar um ele abrir o popup para a pessoa ver o detalhe e ter uma possibilidade."*

**Sobrar um prova que o casador eliminou os outros, e jamais que ele acertou este.** Quem
cadastrou o carro reconhece o acabamento numa olhada; o casador só sabe o que o nome digitado
repete. Gravar sem mostrar tirava da pessoa exatamente a conferência que ela faz melhor — e,
quando errava, deixava um preço de outra versão na ficha sem ninguém ter visto passar.

Então o pop-up abre **sempre**, com um candidato ou com vinte, e o campo `applied` saiu do
contrato em vez de virar um campo que jamais vem preenchido.

**A inteligência deixou de decidir e passou a assinar o quanto confia.** A nota de acurácia sai
dos mesmos sinais que já eliminavam — versão 4, ano 2, câmbio 1, combustível 1 —, sobre o que
havia para conferir:

| O carro cadastrado como | A nota do melhor candidato | O que a tela mostra |
|---|---|---|
| `Renegade 1.8 Longitude` | 100% | um candidato, recomendado, tudo conferido |
| `Renegade Longitude` sem o motor | 75% | o destaque, e o que ficou por conferir |
| `Gol`, e nada mais | 50% | vinte candidatos empatados, e **recomendado nenhum** |

O peso da versão é **fixo**, e vale mesmo no carro que veio sem versão. Foi a única forma de o
medidor ser honesto: um `Gol` sem mais nada bateria tudo o que havia para bater e devolveria
vinte candidatos marcando 100%, que é a única leitura que essa tela jamais pode dar.

**O recomendado só existe por maioria estrita.** Empate volta sem destaque nenhum — a regra do
M15 dita em nota: onde o sistema empata, ele pergunta em vez de apontar. E o destaque muda o que
se lê primeiro, e nada mais: o botão que grava é o da pessoa, em 100% dos casos.

**E o que foi vinculado se desvincula.** `DELETE /api/vehicles/{code}/fipe` apaga a consulta
inteira — código, ano-combustível, valor, mês e origem. Guardar metade seria pior do que guardar
nada: o valor veio do modelo que está sendo desfeito, e ficaria na ficha um preço sem nada que o
explique, ainda alimentando o painel de custo e a projeção de sobra. Depois disso a rotina mensal
deixa de alcançar o carro — consequência desejada, e não efeito colateral.

O marco trouxe junto o **pátio de demonstração**: quatro lugares e vinte carros atrás de
`RevendaPro__SeedDemoVehicles`, ligado só na pilha de desenvolvimento. Ele existe para mostrar as
duas pontas da busca no mesmo estoque — o nome que casa com uma linha da tabela e o nome digitado
com pressa que casa com vinte —, com lucro, prejuízo e carro em cada degrau da esteira. O
catálogo tem teste próprio, porque catálogo é dado: placa válida e única, pátio que existe, e
carro vendido que a esteira alcança.

---

## M17 — O mesmo pátio, de dois jeitos

Nasceu do próprio M16: com vinte carros no pátio de demonstração, o mosaico de cards deixou de
ser confortável.

> *"Hoje parece cards, os veículos. Eu quero formas de mostrar como lista, igual marketplace."*

**Card e lista respondem perguntas diferentes**, e é por isso que todo lugar que vende coisa tem
os dois. O mosaico responde *"qual é este carro?"* — a foto grande é o que faz reconhecer. A
lista responde *"qual destes carros?"* — a linha curta é o que faz comparar. Com dez carros o
mosaico ganha; com sessenta, procurar um Gol prata de 2015 num mosaico é rolar a tela cinco
vezes olhando foto parecida.

**A margem direita é o que faz a lista valer.** Custo e *quero receber* caem no mesmo lugar em
toda linha, e comparar vinte carros vira descer o olho — e não caçar onde cada número começa.

A barra de teto do card ficou de fora da linha: ela tem três linhas de altura, e a lista existe
para caber gente na tela. O aviso dela virou **selo**, e só quando há o que avisar. Selo
permanente vira parte do fundo e para de ser lido.

**A escolha mora no navegador de quem olha**, e jamais no servidor: guardá-la na empresa faria a
preferência do vendedor mudar a tela do financeiro. Ela é lida depois da montagem, porque ler
`localStorage` no primeiro render faria o servidor e o navegador desenharem coisas diferentes —
o erro de hidratação que o React acusa em voz alta. O preço é um quadro de mosaico antes da
lista aparecer; o preço do outro caminho seria a tela inteira piscando.

Veio junto o **`ops/demo-photos.sh`**: uma foto em cada carro de demonstração, pela mesma porta
que a tela usa. Ele é script, e jamais parte do semeador — a subida da API não pode depender de
um site de terceiro, e vinte fotos no repositório seriam megabytes de binário no histórico para
sempre.

---

## M18 — Fornecedores, e quanto já foi para cada um

Plano completo em `docs/plans/m18-fornecedores.md`.

> *"Preciso fazer a implementação de fornecedor e quanto você já gastou em cada fornecedor. Vai
> ser oficina, pintura, autopeças, essas coisas. E no dash preciso de um painel onde eu vou ver
> quais são os fornecedores que eu mais gastei, e ter um espaço exclusivo também."*

**Fornecedor diz de quem; tipo de gasto diz o quê.** São perguntas diferentes, e a mesma oficina
responde a várias da segunda — cobra Mecânica num carro e Peças no outro. Juntar as duas coisas
obrigaria a escolher entre saber o que se gastou e saber com quem. O gasto aponta para os dois,
e o fornecedor é **opcional**: IPVA, multa e taxa de leilão vêm sem, e obrigar a escolher criaria
o fornecedor "Governo" em toda revenda.

**O ramo é cadastro, e a revenda nasce com 25.** O plano propunha um enum; o stakeholder pediu
cadastro no mesmo dia — *"já coloque bastante para não precisar ficar cadastrando"*. A razão é a
do tipo de gasto: a estofaria e o chaveiro só aparecem no uso, e uma lista fixa os mandaria para
"Outros". O ramo se administra de dentro da tela Fornecedores; uma tela "Ramos" seria uma
permissão a mais para uma coisa só.

**"Quanto gastei" é o que foi pago.** O previsto aparece à parte: um orçamento com a funilaria
interessa, e ainda assim jamais entra no total até virar serviço. A soma é do banco, com `GROUP
BY` — e nunca a lista inteira de gastos carregada para somar no servidor, que é o que a listagem
recusa desde o M6.

**Duas leituras para duas perguntas.** O dashboard mostra o painel de fornecedores **no período
das vendas** — a leitura do mês —; a tela Fornecedores abre com ele inteiro, *desde o início*:
indicadores, o ranking em barras, a rosca por ramo, as colunas mês a mês com o previsto hachurado
por cima do pago, e o gasto por tipo — em SVG, com a paleta validada nos dois temas. Cada card
abre a **ficha**: pago, previsto, em quê, e em que carros, com a placa levando ao veículo.

**Quem registra gasto lê a lista; quem administra, lê valores.** `GET api/suppliers` é guardado
por `vehicles` e vem sem dinheiro; o ranking e a ficha exigem `suppliers`. Fornecedor com gasto
no nome recusa exclusão, e diz quantos — apagá-lo apagaria a resposta para a pergunta que o
cadastro existe para responder.

O pátio de demonstração ganhou nove fornecedores, com a Mecânica dividida entre duas oficinas
para o ranking ter disputa, e quem já tinha os vinte carros no banco recebeu o fornecedor nos
gastos antigos sem apagar nada. Provado contra o MariaDB real: a soma, o período, a ficha, o
painel, e a outra revenda enxergando nada.

---

## M19 — Relatórios: a ficha do carro, a proposta para o cliente, e as planilhas

Plano completo em `docs/plans/m19-relatorios.md`; a decisão estrutural, na ADR-0007.

> *"Preciso tirar um resumo do carro para venda em forma de relatório PDF. Também preciso tirar
> algo que eu possa imprimir ou mandar uma proposta de venda para um cliente. Ele precisa ser
> CSV ou Excel. No PortalCliente.Global tem as bibliotecas de relatório que já funcionam."*

**As mesmas bibliotecas da casa, na camada certa.** QuestPDF (licença Community) e ClosedXML,
nas versões do PortalCliente.Global, referenciadas só pela API: o handler entrega o DTO, a
classe em `Api/Reports` o desenha, e um teste de arquitetura garante que nenhuma das duas entra
em `Application`, `Domain` ou `Infrastructure`. CSV é escrito à mão — ponto e vírgula e BOM,
que é o que o Excel em português abre certo. Toda formatação passa por `pt-BR` explícito, porque
o contêiner sobe em cultura invariante, e o rodapé numera as páginas e usa o horário de
Brasília.

**A revenda no papel.** O `Tenant` ganhou CNPJ, telefone, e-mail e endereço, e a tela **Dados da
revenda**: um papel que sai da loja precisa dizer de quem é.

**A ficha para venda mostra o que vende, e esconde o que é da casa.** Foto de capa e mais seis,
dados, tabela FIPE e preço anunciado — ou *Consulte*. Custo, compra, lucro, pátio e fornecedor
ficam de fora por decisão do handler: é um papel para o comprador.

**A proposta sai da proposta registrada.** A revenda em cima, o cliente, o carro com a foto, o
valor, a forma de pagamento, a validade de sete dias, as observações como condições e duas linhas
para assinar. O WhatsApp entra como atalho com a mensagem pronta; o PDF vai anexado pela pessoa.

**As planilhas levam o que a tela mostra, com os filtros da tela, e tudo.** Veículos, gastos,
vendas e fornecedores, em Excel ou CSV, com número e data guardados como número e data.

Conferido gerado de dentro do Docker, com foto, e renderizado — a ficha e a proposta —, e cada
formato aberto de volta em teste: a assinatura `%PDF-`, a célula de decimal no Excel, o BOM e as
aspas no CSV.

---

## M20 — A proposta pelo WhatsApp, do computador e do celular

Plano completo em `docs/plans/m20-whatsapp-no-celular.md`; a decisão estrutural, na ADR-0008.

> *"Muito interessante essa funcionalidade do WhatsApp, lembrando que também precisa que no
> celular também vá."*

**Um botão só, que decide pelo aparelho.** *Mandar pelo WhatsApp*, na proposta e na ficha do
carro. No celular, a Web Share API abre a folha de compartilhamento com o **PDF já anexado** e
a mensagem, que também vai para a área de transferência, porque o WhatsApp do Android descarta
o texto quando há arquivo. No computador, o PDF cai na pasta de downloads e o `wa.me` abre a
conversa do cliente com a mensagem pronta. O sistema jamais fala com a Meta: o arquivo sai do
aparelho de quem vende, do número que o cliente já conhece.

**HTTPS na rede, sem domínio.** A Web Share API só existe em página segura, e o servidor da
rede atendia em HTTP. O Caddy da LAN passou a emitir o certificado para o IP com a raiz interna
dele, entregue em `http://<IP>/raiz-revendapro.crt` — o único endereço em HTTP —, e cada celular
a instala uma vez (`docs/operations/celular-na-rede.md`). As fotos saem pela porta 9100 do mesmo
Caddy, porque uma página segura recusa foto em HTTP, e o cookie de sessão voltou a nascer
`Secure`. Dois detalhes que só o teste mostrou: o redirecionamento automático do Caddy passava
na frente da rota da raiz, e quem abre um IP manda SNI nenhum, enquanto dentro do Docker o
endereço local é o do contêiner — `default_sni` resolve.

**O caminho no celular, fotografado.** Num Pixel e num iPhone emulados, a lista já cabia; o
detalhe do carro quebrava as abas em três linhas e espalhava os botões. As abas rolam de lado,
os botões viram uma grade de dois por linha com *Mandar pelo WhatsApp* na linha inteira, e no
card da proposta *Aceitar e vender* fecha embaixo na largura toda. Capturas em
`docs/screens/celular/`.

**Na tela inicial.** Manifesto, ícones da marca em 192 e 512, o ícone do iPhone e a barra do
sistema na cor do tema. Sem *service worker*: uma revenda que vende pelo preço da tela precisa
da tela viva. O guarda de sessão deixa passar o manifesto e os ícones, senão o celular salvava
um atalho de navegador em vez do aplicativo.

Ficou de fora, de propósito e por escrito: a **WhatsApp Business Platform**, que o stakeholder
quer avaliar depois, e o **link público da proposta** com "visualizada em", que pede o sistema
na internet. A API não mudou uma linha: a suíte continua a mesma. A publicação no servidor da
rede fica para a próxima sessão em rede, junto da instalação da raiz nos celulares.

---

## M21 — Clientes: quem ofereceu, quem comprou, e quem volta

Plano completo em `docs/plans/m21-clientes.md`.

> *"Não seria fornecedor, seria comprador."* — e depois: *"Vamos fazer 1, 2, 3, 4, 5, mas na
> sequência. Vamos planejar o 1 agora."*

**Cliente antes de comprador.** A tela se chama Clientes e fica no grupo Operação, entre os
carros e as vendas, porque é do vendedor. A pessoa que ofereceu e foi recusada, e volta em seis
meses, existe desde a primeira proposta: uma tela chamada Compradores esconderia metade das
pessoas que a loja conhece. A entidade `Customer` tem nome, documento conferido de verdade
(vai na linha de assinatura), telefone, e-mail, endereço e anotação.

**A proposta e a venda apontam para o cliente, e guardam a cópia do papel.** `IdCustomer` nas
duas; os textos de sempre ficam como o que estava escrito no dia — um contrato diz quem assinou
mesmo que a pessoa troque de telefone. A tela lê o nome do cliente de hoje.

**Ninguém redigitou nada.** Na primeira subida, cada revenda ganhou os clientes das vendas e
propostas que já tinha: casados por documento, depois por telefone, depois por nome igual quando
um dos lados estava sem telefone. No banco local, 9 vendas e 11 propostas viraram 18 clientes
— dois Eduardos com telefones diferentes continuaram duas pessoas, como a decisão manda.

**Cadastrar sem sair da proposta.** O seletor busca enquanto digita, por nome, telefone ou
documento, e oferece quem a loja já conhece; escolher preenche telefone e CPF, digitar um nome
novo cadastra ao salvar, e o campo diz qual dos dois está acontecendo. A venda que fecha uma
proposta é do cliente dela e completa o CPF que a proposta deixou em branco. Documento igual é
recusa; telefone igual é aviso, e a segunda tentativa confirma.

**A ficha é a história.** Propostas e compras com o carro de cada uma, o total comprado, o
WhatsApp no número — e **Mandar ficha**: escolhe um carro do pátio e o WhatsApp abre no número
do cliente com a ficha em PDF, pelo caminho do M20. É o carro novo chegando a quem já comprou.

**No papel.** A proposta em PDF imprime o CPF ou CNPJ embaixo do nome e na linha de assinatura,
revenda e cliente: é o que faltava para o papel valer como aceite.

Provado no MariaDB real: duas propostas com o mesmo telefone e uma venda com CPF viram um
cliente, e rodar o aproveitamento de novo muda nada; a outra revenda enxerga nada; a planilha
sai. O guarda de colunas do M18 passou a cobrir Proposal, Sale e Customer. Suíte: 708 verdes.

Ficou de fora, de propósito: **funil de vendas** e etapas, **envios em massa**, **importar
planilha** de clientes e **endereço estruturado**. A publicação no servidor da rede fica para a
próxima sessão em rede.

---

## M22 — Caixa: o que vence, o que entrou, o que atrasou

Documento de entrega em `docs/entregas/M22-caixa.md`; plano em `docs/plans/m22-caixa.md`.

> *"Contas a pagar e a receber. A visão de caixa: o que vence esta semana, o que entrou, o que
> está atrasado. É o que o dono olha toda segunda."*

**A conta a pagar é o gasto que já existe, com duas datas a mais.** A revenda já lançava a
retífica como *previsto*; pedir que ela lançasse de novo numa tela de contas seria digitar duas
vezes a mesma coisa, e deixar as duas discordarem. O que faltava era **quando vence** e **quando
saiu** — e sem a segunda, marcar como pago apagava a informação de quando o dinheiro saiu. As
duas jamais discordam do estado: só a entidade as move.

**A despesa da loja é entidade própria, e não um gasto sem carro.** O gasto do carro chega à
revenda **pelo veículo**, de propósito desde o M0: existe um lugar onde uma linha pode ser presa
à empresa errada, em vez de cinco. Um IdVehicle nulo quebraria isso. O aluguel, a energia, o
salário e o imposto passam a caber no sistema — e uma tela de contas a pagar que recusa o
aluguel não é uma tela de contas a pagar.

**Um catálogo de tipos, com escopo.** Dois cadastros de "tipos" seria a pessoa perguntando qual
é qual a cada lançamento. O tipo passou a dizer para onde serve — carro, loja, ou os dois —, os
treze que já existiam nasceram de carro, e quatro de loja entraram no catálogo semeado.

**A conta a receber é a venda que ainda não virou dinheiro.** A venda registrada era dinheiro no
bolso para o sistema, mesmo quando o banco paga em quinze dias. Cada entrada fica registrada com
data e forma; o saldo é subtração feita a cada leitura — a mesma regra do custo desde o M6 —, e
o esperado tira o que jamais vira depósito: o carro da troca, que já está no pátio, e o repasse
da loja parceira, que é dela.

**Uma tela, três origens.** No Caixa elas viram linhas do mesmo extrato, ordenadas por
vencimento pelo banco, com o vermelho reservado para o que passou do prazo — uma tela onde tudo
grita avisa nada. A baixa é um clique, e tem uma porta só para os dois lados. O painel ganhou os
mesmos números sem as listas: o painel diz **quanto**, e o caixa diz **o quê**.

Três defeitos da mesma família apareceram no caminho, e todos os três eram coluna gravada e
jamais lida — o defeito do M18: o `Scope` ficou de fora dos dois SELECT de tipo de gasto, e o
`DEFAULT` de uma coluna nova jamais preenche linha antiga, porque o provider grava o padrão do
tipo. O guarda de colunas passou a cobrir `ExpenseType` e `StoreExpense`, e as migrations levam
o `UPDATE` explícito. De caminho, a lista de tipos parou de custar uma consulta por linha.

Ficou de fora, de propósito: a **compra do carro** como conta a pagar (ela já entra no custo no
dia em que o carro entra, e virar conta pediria decidir o que o custo faz enquanto ela está
aberta), **despesa recorrente**, **conciliação bancária**, **fluxo projetado** e **centro de
custo**. A publicação no servidor da rede fica para a próxima sessão em rede.

---

## O que continua aberto

Lista completa, com o que destrava cada item, em `docs/PENDENCIAS.md` — escrita no dia em que
o desenvolvimento parou para entregar o MVP.

| Item | Por que ainda está aberto |
|---|---|
| **Subida em produção** (M9) | Depende de VPS, domínio e conta no R2. O compose, o HTTPS e o roteiro estão prontos e testados. |
| **Fonte da FIPE** | O espelho é de terceiros, e pode sumir ou passar a cobrar. As três saídas estão prontas: a porta no domínio, o interruptor de configuração e o valor digitado à mão. |
| **Acesso do parceiro ao próprio pátio** | O dono da loja onde o carro está poderia entrar e ver só os carros que estão com ele. É uma fronteira de segurança nova **dentro** da mesma empresa, que hoje o sistema não tem — marco próprio quando doer. |
| **Testes de interface** | O frontend é conferido por build e por captura de tela. Um marco de testes de interface faz sentido quando houver mais de uma pessoa mexendo nele. |
| **Recuperação de veículo e gasto excluídos** | A exclusão lógica vale para tudo, mas só o documento tinha arquivo pago parado no bucket. As outras entram quando alguém precisar. |

## A suíte, hoje

655 testes, todos verdes — 406 de unidade e 249 que sobem a API de verdade contra um banco
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
- **matriz perfil × endpoint** — os 80 endpoints, os cinco perfis e o anônimo, com a API no ar:
  quem tem a tela passa, quem não tem leva 403, e sem token tudo responde 401;
- **isolamento entre empresas** — duas revendas montadas pelo próprio sistema, e uma jamais
  alcança o dado da outra, nem lendo nem escrevendo;
- **pátios** — o repasse combinado de um jeito só, o pátio da casa que jamais cobra da casa, a
  passagem registrada com o de onde veio, o filtro pedido ao banco e não peneirado em memória, e
  o painel somando cada lugar sem parar de somar o todo;
- **o casador da tabela** — afinado contra nomes de verdade da FIPE, e não contra nomes
  inventados: o Gol que jamais vira Golf, o câmbio manual reconhecido pela ausência da marca, e o
  empate que volta como pergunta em vez de virar palpite;
- **fornecedores** — o ramo de outra revenda recusado como inexistente, o fornecedor com gasto
  que fica, a soma feita pelo banco com pago e previsto separados, o período lido sobre a data
  do gasto, e a ficha e o ranking que jamais trazem um centavo da outra empresa;
- **documentos gerados** — cada formato gerado e aberto de volta, a ficha e a proposta com foto
  WebP, QuestPDF e ClosedXML presos à camada da API, e a ficha do carro da outra revenda em 404.
