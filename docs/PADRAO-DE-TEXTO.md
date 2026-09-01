# Padrão de texto de interface

Regra de escrita para todo texto que o usuário lê: rótulo, botão, título, mensagem de erro,
estado vazio, `detail` de resposta HTTP e mensagem de validação.

---

## As duas regras

> **1. A palavra "não" nunca aparece para o usuário.**
>
> **2. O português é escrito corretamente, com acentuação completa.**

Toda frase é escrita na afirmativa. Se a mensagem só faz sentido com um "não", ela está
descrevendo o problema pela ausência — reescreva descrevendo **o que é**, ou **o que fazer**.

Vale para português e para qualquer variação: `não`, `nao`, `Não`, `Nao`.

A segunda regra existe porque texto sem acento parece defeito. `"Usuario nao encontrado"`
e `"Autenticacao necessaria"` passam a impressão de banco mal configurado ou de codificação
quebrada — mesmo quando o resto do produto está impecável.

O arquivo é UTF-8. Escreva `Usuário`, `Autenticação`, `Sessão`, `Você`, `possível`,
`inválido`, `permissão`, `próprio`, `histórico`, `número`, `máximo`, `descrição`.

## Por que

Uma interface que fala pela negativa devolve o problema ao usuário sem dizer o caminho:

```
Você não pode excluir a própria conta.
```

Ele descobriu o que está proibido, e continua sem saber como resolver. A mesma informação
na afirmativa carrega a saída:

```
Outro administrador precisa excluir a sua conta.
```

A negativa também costuma esconder preguiça de escrita. `"Não foi possível salvar"` é o
texto que sai quando ninguém pensou no que dizer — e serve igualmente mal para timeout,
conflito de e-mail e falha de rede.

---

## Como reescrever

Três movimentos resolvem quase tudo.

### 1. Troque o verbo negado pelo fato

| Evite | Use |
|---|---|
| Não foi possível salvar o usuário. | Falha ao salvar o usuário. |
| Não foi possível falar com o servidor. | Servidor indisponível. Tente novamente. |
| Esta conta não está mais ativa. | Esta conta está inativa. |
| O módulo ainda não foi implementado. | Módulo em construção. |
| Esta tela não aparece no menu. | Esta tela fica fora do menu. |

### 2. Diga o que fazer, em vez do que está bloqueado

| Evite | Use |
|---|---|
| Você não pode excluir a própria conta. | Outro administrador precisa excluir a sua conta. |
| Você não pode inativar a própria conta. | A inativação da sua conta fica a cargo de outro administrador. |
| O perfil informado não pertence a esta empresa. | Selecione um perfil desta empresa. |
| Formato não aceito. | Envie uma imagem JPG, PNG ou WEBP. |
| Seu perfil não tem acesso a esta tela. | Esta tela depende de liberação para o seu perfil. |

### 3. Nomeie o estado em vez de negá-lo

| Evite | Use |
|---|---|
| Usuário não encontrado. | Usuário inexistente. |
| Perfil não encontrado. | Perfil inexistente. |
| Não autenticado | Autenticação necessária |
| O conteúdo não corresponde a uma imagem. | O arquivo precisa ser uma imagem JPG, PNG ou WEBP válida. |
| Seu perfil ainda não tem telas liberadas. | Seu perfil aguarda liberação de telas. |

---

## O que a regra **não** cobre

Ela vale para **texto que o usuário lê**. Comentário de código, nome de variável, log
técnico e documentação seguem a linguagem normal — inclusive esta frase.

## Uma nota honesta sobre o limite

A regra é sobre a palavra "não". O princípio por trás dela é **escrever na afirmativa**, e
ele é maior: `nenhum`, `sem`, `impossível` e `inválido` também negam.

Alguns desses continuam sendo a melhor escolha. `"Nenhum usuário encontrado"` é o texto
certo para uma lista vazia — trocar por algo afirmativo forçado só piora. `"E-mail
inválido"` é mais claro que qualquer alternativa.

Então: **"não" é proibido; o resto é julgamento.** Quando `sem` ou `nenhum` for o texto
mais claro, use — e complete com o caminho, que é o que a regra realmente busca:

```
Nenhum veículo no estoque.        ← estado
Cadastre o primeiro.              ← saída
```

---

## Verificação

```bash
# 1. Strings de tela contendo a palavra proibida
grep -rnE '"[^"]*\b(nao|não|Nao|Não)\b[^"]*"' \
  --include=*.cs --include=*.tsx --include=*.ts \
  src frontend/app frontend/components frontend/lib \
  | grep -vE '^\s*//|///|^\s*\*'

# 2. Palavras portuguesas escritas sem acento
grep -rnoE '"[^"]*\b(cao|coes|Voce|usuario|Usuario|possivel|invalidos?|invalida|Sessao|negocio|proprio|propria|disponivel|conteudo|Veiculos|Modulo|maximo|minimo|numero|historico|tambem|pagina|gestao|liberacao|Descricao|Situacao|Orcamento|tecnicos|relatorios|proximo)\b[^"]*"' \
  --include=*.cs --include=*.tsx --include=*.ts \
  src frontend/app frontend/components frontend/lib
```

Os dois resultados esperados são vazios. Rode antes de abrir PR.
