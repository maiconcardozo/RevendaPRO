# A0–A5 — Acesso: empresa, usuário, perfil, permissão, login e menu

Fase 1 do projeto. Documento de entrega escrito em 8 de setembro de 2026, a partir de
`docs/MARCOS.md` e do plano `docs/plans/acesso-e-menu.md`.

## O que este marco resolve

Nada do resto do sistema pode existir antes de o sistema saber **quem está falando com ele** e
**o que essa pessoa pode abrir**. Esta fase entrega isso: a empresa, a pessoa, o perfil, a
permissão por tela, o login e o menu que nasce da permissão.

## As regras

### O menu é a permissão

O menu de cada pessoa é montado **pelo servidor**, a partir das telas que o perfil dela alcança.
Rota nenhuma do painel abre sem login.

**Por que é essa:** esconder um item de menu é apresentação, e jamais segurança — quem digita a
rota na barra do navegador passaria por cima. O menu montado pela mesma lista que guarda a porta
faz as duas coisas concordarem por construção, e não por disciplina.

### A sessão é cookie httpOnly, e jamais `localStorage`

O token da API vive num cookie que o JavaScript da página não enxerga.

**Por que é essa:** um token em `localStorage` é legível por qualquer script que entre na
página — uma biblioteca comprometida basta. O cookie httpOnly tira o token do alcance do
navegador e o põe no do servidor, que é quem precisa dele.

### Cinco perfis nascem com o sistema, e perfil de sistema jamais se apaga

**Administrador** (todas as telas, inclusive as que surgirem), **Gestor**, **Financeiro**,
**Vendedor** e **Oficina**. Eles podem ganhar e perder telas; excluir, não.

**Por que é essa:** uma revenda que apagasse o perfil Administrador ficaria sem porta de
entrada, e a recuperação seria no banco. O perfil de sistema é permanente pelo mesmo motivo que
a chave de casa tem cópia.

### O Administrador alcança a tela que ainda não existe

Ao subir, o sincronizador concede ao Administrador toda tela nova do catálogo.

**Por que é essa:** sem isso, todo marco que cria uma tela exigiria um passo manual de
permissão — e o passo esquecido é uma tela que ninguém abre, num sistema que parece quebrado.

### Refresh token com rotação e revogação

O token curto expira; o de renovação é trocado a cada uso e pode ser revogado.

**Por que é essa:** token longo roubado é acesso permanente. Rotacionar transforma o roubo em
uma janela, e a revogação fecha a janela quando alguém percebe.

## Como usar

*Administração → Usuários* cadastra a pessoa e escolhe o perfil. *Administração → Perfis* diz
quais telas cada perfil alcança — e é ali que se concede uma tela nova a quem não é
Administrador.

## O que mudou por baixo

- Entidades `Tenant`, `User`, `Role`, `Screen`, `RoleScreen`, `RefreshToken`, `AuditLog`.
- Senha com hash forte; JWT com chave e expiração por variável de ambiente.
- `RequireScreen` nos endpoints, e o `ScreenSynchronizer` na subida.

## O que foi conferido

A guarda foi provada de duas formas, e a segunda só chegou no **M12**: aqui, um teste estático
exigia que todo endpoint declarasse a tela que o protege; lá, a matriz perfil × endpoint provou
que a fechadura **tranca**, com a API no ar. Ver `docs/entregas/M12-matriz-e-isolamento.md`.

## O que ficou de fora, e por quê

- **Autenticação de dois fatores**: fila de prioridade, e sem pedido do negócio.
- **Login social**: uma revenda tem funcionários, e não visitantes.
