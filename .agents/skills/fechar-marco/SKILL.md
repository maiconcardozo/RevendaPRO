---
name: fechar-marco
description: O ritual obrigatório de fechamento de um marco (M<n>) deste projeto — o documento de entrega em docs/entregas/, a documentação técnica atualizada, a suíte verde, e o merge na main por pull request. Use ao terminar o último V de um marco, ao ouvir "fechar o marco", "terminar o marco", "subir para a main", ou antes de qualquer merge de uma branch M<n>.
---

# Fechar um marco

Um marco só está fechado quando **o documento de entrega existe** e **a `main` recebeu o
trabalho por pull request**. As duas coisas são obrigatórias, e nenhuma delas é opcional
porque o código já funciona.

## A ordem

1. **A suíte inteira, verde.** `dotnet test tests/RevendaPro.Tests/RevendaPro.Tests.csproj`.
   Frontend: `npx tsc --noEmit -p .` e `npx eslint` nos arquivos tocados — erro de lint
   pré-existente continua pré-existente, e isso se confere com `git stash`, jamais se supõe.
2. **A pilha no ar**, com o código do marco: `docker compose --env-file .env up -d --build`.
   Confira na tela o que o marco entrega, e guarde a captura do que você conferiu.
3. **O documento de entrega** (abaixo). É o entregável que falta com mais frequência.
4. **A documentação técnica**, sempre que o marco mexeu no que ela descreve:
   `docs/MARCOS.md` (linha na tabela + seção), `docs/ROADMAP.md` (seção + a cadeia de
   dependências), `docs/api/endpoints.md`, `docs/database/mappings.md`,
   `docs/MANUAL-DO-USUARIO.md`, e o plano do marco com a seção
   *O que a implementação acrescentou ao plano*.
5. **O commit final**, `V<n>: fechamento do M<n> — ...`, com o documento e a documentação.
6. **O pull request para a `main`**, e o merge (abaixo).
7. **A memória**, quando o marco muda o que uma sessão futura precisa saber: o que ficou
   pendente, o que mudou no servidor, qual é o próximo marco combinado.

## O documento de entrega

Um arquivo por marco, em `docs/entregas/M<n>-<slug>.md`. Ele é escrito **para a pessoa que
vai usar e manter**, e não para quem escreveu o código: conta o que mudou, **qual é a regra**
e **por que ela é essa**. `MARCOS.md` continua sendo o índice; este é o documento longo.

```markdown
# M<n> — <Título>

Entregue em <data por extenso>. Plano em `docs/plans/m<n>-<slug>.md`.

## O que este marco resolve
O problema em duas ou três frases, na língua da loja.

## As regras
Uma subseção por regra que a pessoa precisa conhecer para usar o sistema sem se surpreender.
Cada uma diz **a regra**, e logo abaixo **por que ela é essa** — a razão vale mais do que a
regra, porque é ela que sobrevive à próxima mudança.

## Como usar
O caminho na tela, do começo ao fim, como alguém faria de verdade.

## O que mudou por baixo
Entidades, tabelas, endpoints e migrations, em lista curta, com link para a doc técnica.

## O que foi conferido
A suíte (números), o que rodou contra banco de verdade, e o que foi visto na tela.

## O que ficou de fora, e por quê
O que o marco recusou fazer, com o motivo. É o que evita a mesma discussão daqui a um mês.
```

Regras do texto: as de sempre do projeto — português acentuado, e a palavra "não" jamais
aparece em texto de interface citado. Ver a skill `texto-afirmativo`.

## O merge por pull request

```bash
git push -u origin M<n>
gh pr create --base main --head M<n> --title "M<n>: <título>" --body-file <arquivo>
gh pr merge --merge --delete-branch=false
```

O corpo do PR é o resumo do documento de entrega: o que resolve, as regras, o que foi
conferido, o que ficou de fora. Termine o corpo com a linha de atribuição que o sistema
mandar nesta sessão.

**Quando o `gh` recusa.** A conta autenticada no `gh` pode ter só leitura neste repositório —
`gh repo view --json viewerPermission` responde `READ`, e o `gh pr create` falha. Nesse caso:

1. **Diga ao usuário**, com a conta que está autenticada e a que seria necessária. Ele resolve
   com `gh auth login` (ou `gh auth switch`) na conta dona do repositório — é uma vez só.
2. **Enquanto isso, jamais deixe o marco sem chegar à `main`**: faça o merge local
   preservando o histórico, e diga que foi assim.

```bash
git checkout main && git merge --no-ff M<n> -m "M<n>: <título>"
git push origin main M<n>
```

O `--no-ff` é obrigatório: ele guarda a fronteira do marco no histórico, que é o que permite
ler depois o que entrou em cada um.

## O que jamais fazer no fechamento

- Fechar sem o documento de entrega, mesmo que o `MARCOS.md` já descreva o marco.
- Anunciar "publicado" quando o servidor da rede continua na versão anterior. Publicar exige
  estar na LAN; sem ela, o marco fecha com a publicação **pendente**, dito em voz alta.
- Amontoar a documentação no commit de outro V. O fechamento é um commit próprio.
- Apagar a branch do marco: ela é o registro do caminho.
