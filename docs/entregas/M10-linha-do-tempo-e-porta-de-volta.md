# M10 — Linha do tempo, filtro por período e documentos excluídos

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m10-linha-do-tempo-e-filtros.md`.

## O que este marco resolve

A ficha do carro dizia **o que ele é**, e nada sobre **o que aconteceu com ele**. Este marco põe
a história em ordem, deixa filtrar o pátio por período, e abre a primeira porta de volta para o
que foi excluído por engano.

## As regras

### A linha do tempo é lida da operação, e jamais da auditoria

Compra, gastos, anexos, propostas, mudanças de situação e venda, numa aba só e em ordem.

**Por que é essa:** a auditoria existe para perícia e guarda JSON — quem mudou qual campo, de
que valor para qual. A ficha do carro precisa de **significado**: "entrou por R$ 20.000",
"trocou o câmbio", "recusou uma proposta de 34". Ler a auditoria daria uma lista tecnicamente
correta e ilegível.

### Anexos do mesmo dia, pela mesma pessoa, contam um evento

**Por que é essa:** quem sobe oito fotos de uma vez fez **uma** coisa. Oito linhas na história
do carro empurram para baixo o que importa.

### Documento excluído tem porta de volta; apagar de vez, não

Tela administrativa que lista, abre e devolve o documento à ficha.

**Por que é essa:** guardar documento para sempre foi requisito do negócio, e o arquivo nunca
saiu do bucket. Um apagar definitivo desfaria as duas coisas. A ausência do botão é o desenho, e
não um esquecimento — e continua valendo no M23, quando a tela vira **Lixeira**.

### Cada tela filtra pela data que é dela

A listagem de veículos filtra pela **data de compra**; a tela de Vendas, pela **data da venda**.

**Por que é essa:** são perguntas diferentes — "o que eu comprei em agosto" e "o que eu vendi em
agosto" —, e um filtro só respondendo as duas responderia mal as duas.

## Como usar

Na ficha do carro, aba **Linha do tempo**. Na listagem de veículos, os campos *De* e *Até*.
Em *Administração → Documentos excluídos*, o botão **Devolver**.

## O que mudou por baixo

- A leitura da linha do tempo, montada das tabelas da operação.
- `GetByCodeIncludingDeletedAsync` no repositório de documento — uma das quatro consultas que
  leem linha excluída de propósito, com o motivo escrito no teste de exclusão lógica.

## O que foi conferido

Na primeira vez que a tela de documentos excluídos rodou, ela **desenterrou 13 arquivos** que
estavam pagos e inalcançáveis desde o M6. O marco se pagou no primeiro uso.

## O que ficou de fora, e por quê

- **Recuperar carro e gasto**: eles ficavam apenas invisíveis, e a linha estava lá — a fila era
  outra. Vieram no M23.
- **Apagar de vez**: ver a terceira regra.
