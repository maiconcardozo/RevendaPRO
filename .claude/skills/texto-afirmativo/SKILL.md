---
name: texto-afirmativo
description: Regras obrigatórias de escrita para texto de interface deste projeto — a palavra "não" nunca aparece para o usuário, e o português é sempre acentuado corretamente. Use ao escrever ou revisar rótulo, botão, título, mensagem de erro, estado vazio, mensagem de validação ou o campo detail de resposta HTTP, em C# ou TypeScript.
---

# Texto de interface: afirmativo e acentuado

## As duas regras

**1. A palavra "não" nunca aparece para o usuário.**
Vale para toda variação: `não`, `nao`, `Não`, `Nao`.

**2. O português é sempre acentuado corretamente.**
`Usuário`, `Autenticação`, `Sessão`, `Você`, `possível`, `inválido`, `permissão`,
`próprio`, `histórico`, `número`, `máximo`, `descrição`, `Veículos`, `Módulo`.

Ambas valem para rótulo, botão, título, mensagem de erro, estado vazio, mensagem de
validação e o campo `detail` de qualquer resposta HTTP — tudo que chega à tela.

Comentário de código, nome de identificador, log técnico e documentação seguem a linguagem
normal. As regras são sobre o que o usuário lê.

## Regra 1 — sempre na afirmativa

Antes de escrever qualquer string visível, pergunte: **esta frase diz o que é, ou só diz o
que deixou de ser?**

Se ela nega, aplique um dos três movimentos:

### Troque o verbo negado pelo fato

```
Não foi possível salvar o usuário.     →  Falha ao salvar o usuário.
Não foi possível falar com o servidor. →  Servidor indisponível. Tente novamente.
Esta conta não está mais ativa.        →  Esta conta está inativa.
O módulo ainda não foi implementado.   →  Módulo em construção.
```

### Diga o que fazer, em vez do que está bloqueado

```
Você não pode excluir a própria conta.
    → Outro administrador precisa excluir a sua conta.

O perfil informado não pertence a esta empresa.
    → Selecione um perfil desta empresa.

Formato não aceito.
    → Envie uma imagem JPG, PNG ou WEBP.
```

Este é o movimento mais valioso: a negativa devolve o problema, a afirmativa entrega a saída.

### Nomeie o estado em vez de negá-lo

```
Usuário não encontrado.  →  Usuário inexistente.
Não autenticado          →  Autenticação necessária
Não encontrado           →  Registro ausente
```

## Regra 2 — acentuação

Texto sem acento parece defeito. `"Usuario nao encontrado"` passa a impressão de banco mal
configurado ou de codificação quebrada, mesmo com o resto do produto impecável.

Os arquivos são UTF-8. Escreva o português correto.

Palavras que mais escapam neste projeto:

```
Usuario     → Usuário          permissao   → permissão
Autenticacao→ Autenticação     proprio     → próprio
Sessao      → Sessão           historico   → histórico
Voce        → Você             numero      → número
possivel    → possível         maximo      → máximo
invalido    → inválido         Descricao   → Descrição
Situacao    → Situação         Operacao    → Operação
Veiculos    → Veículos         Modulo      → Módulo
```

## Onde as regras costumam escapar

- `detail` de `ProblemDetails` e de `SuccessDetails` — é texto de tela, o frontend exibe;
- `WithMessage(...)` dos validators FluentValidation;
- mensagem de `BusinessRuleException`, `NotFoundException` e afins;
- atributo `title` e `aria-label` de botão desabilitado;
- estado vazio de tabela e de lista;
- texto de confirmação em modal;
- nomes e descrições semeados no banco (`ScreenCatalog`, `DbInitializer`) — são dado exibido.

## O limite da regra 1

O princípio maior é escrever na afirmativa, e ele vai além da palavra: `nenhum`, `sem`,
`impossível` e `inválido` também negam.

Alguns continuam sendo a melhor escolha. `"Nenhum usuário encontrado"` é o texto certo para
uma lista vazia; `"E-mail inválido"` é mais claro que qualquer contorção.

Então: **"não" é proibido; o resto é julgamento.** Quando `nenhum` ou `sem` for mais claro,
use — e complete com o caminho, que é o que a regra realmente busca:

```
Nenhum veículo no estoque.
Cadastre o primeiro.
```

## Verificação

Antes de dar o trabalho por pronto, os dois comandos devem sair vazios:

```bash
# 1. A palavra proibida
grep -rnE '"[^"]*\b(nao|não|Nao|Não)\b[^"]*"' \
  --include=*.cs --include=*.tsx --include=*.ts \
  src frontend/app frontend/components frontend/lib \
  | grep -vE '^\s*//|///|^\s*\*'

# 2. Português sem acento
grep -rnoE '"[^"]*\b(cao|coes|Voce|usuario|Usuario|possivel|invalidos?|invalida|Sessao|negocio|proprio|propria|disponivel|conteudo|Veiculos|Modulo|maximo|minimo|numero|historico|tambem|pagina|gestao|liberacao|Descricao|Situacao|Orcamento|tecnicos|relatorios|proximo)\b[^"]*"' \
  --include=*.cs --include=*.tsx --include=*.ts \
  src frontend/app frontend/components frontend/lib
```

Tabela completa de reescrita em `docs/PADRAO-DE-TEXTO.md`.
