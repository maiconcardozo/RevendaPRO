# Controles de formulário: uma altura só

Irmão do `docs/PADRAO-DE-TEXTO.md`. Aquele diz **como a frase é escrita**; este diz **como o
campo é desenhado**. Os dois valem para tudo que chega à tela.

## A regra

> **Todo controle de formulário usa a classe `.control`.** Caixa de texto, lista de escolha,
> campo de data e caixa de texto longa. Quem precisa de exceção acrescenta uma utilitária ao
> lado — e jamais reescreve a métrica.

```tsx
<input className="control" />
<select className="control" />
<input className="control pl-9" />        {/* abre espaço para um ícone */}
<select className="control w-auto" />     {/* controle solto numa linha, sem esticar */}
<input className="control control--error" />
```

## Por que ela existe

Três controles carregavam **as mesmas classes** — `px-3 py-2 text-sm` — e saíam da tela com
**três alturas diferentes**. Ficou visível na ficha do veículo, lado a lado:

| Par que denunciou | O que acontecia |
|---|---|
| *De quem comprou* × *Forma de pagamento* | a lista de escolha ficava alguns pixels mais alta |
| *Pátio* × *Data da compra* | e o campo de data, mais alto ainda |

**Padding não decide a altura de um controle.** O navegador acrescenta a mobília de cada tipo:
o `select` carrega a seta, e o `input[type="date"]` carrega o botão de calendário e os campinhos
internos de dia, mês e ano. Cada um cresce alguns pixels por conta própria, e a soma jamais bate
com a do vizinho.

Então a altura deixou de ser consequência e virou decisão: **2,375rem**, que é exatamente o que
a caixa de texto já media — padding 8, linha 20 e as duas bordas. Nada que já estava certo se
move; os outros dois vêm até ela.

## Onde ela mora, e por quê

Em `frontend/app/globals.css`, dentro de **`@layer components`**.

A camada não é decoração. Regra fora de camada vence **toda** utilitária do Tailwind, por mais
específica que ela seja — e o primeiro campo que precisasse de um recuo maior para caber um
ícone deixaria de obedecer, sem erro nenhum, só com o desenho errado. Dentro da camada, a
utilitária ainda vence, que é o ponto: um padrão com espaço para exceção.

E ela mora **uma vez**. Antes, a mesma métrica estava copiada no `Field`, no `Select` e no
`TextArea` — três cópias que divergem no dia em que alguém mexe numa delas.

## As exceções

**Nenhuma, hoje.**

Os dois seletores de classificação — o da linha de cada documento e o da legenda de cada foto —
ficaram de fora na primeira volta, com o argumento de que viviam numa lista densa. O argumento
caiu na revisão seguinte: *"um tamanho para formulário e outro para lista"* é uma regra que
ninguém consulta antes de escrever o próximo campo, e ela reabre pela borda exatamente a
inconsistência que este documento fecha pelo meio.

Eles usam `.control` como todo o resto, com a largura ajustada por utilitária:

```tsx
<select className="control w-auto" />          {/* linha do documento */}
<select className="control min-w-0 flex-1" />  {/* legenda da foto */}
```

Se algum dia um controle **precisar** de outro tamanho, o lugar de dizer isso é aqui, com o
motivo escrito — e jamais em classes soltas no meio de um componente.

## Ao escrever um campo novo

1. Use `Field`, `Select` ou `TextArea` — eles já vêm com o padrão, o rótulo e o erro por campo.
2. Precisando de um controle solto, escreva `className="control"` e pare aí.
3. Precisando de um ajuste — ícone, largura natural, fundo diferente —, acrescente a utilitária
   **ao lado** da classe.
4. Mexeu na altura? Ela muda em um lugar só, e muda para o sistema inteiro. É de propósito.
