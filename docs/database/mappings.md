# Mapeamentos de banco

Provider: **MariaDB 11.8** via `Pomelo.EntityFrameworkCore.MySql`.
Modelo definido em `docs/architecture/decisions/ADR-0002-acesso-por-tela.md`.
Ainda não implementado — este documento é o alvo do marco A1 de
`docs/plans/acesso-e-menu.md`.

## Convenções

- Chave primária `Codigo` do tipo `Guid`, armazenado como `char(36)`.
- Nomes de tabela e coluna em português, no singular.
- Datas gravadas em **UTC**, tipo `datetime(6)`. Conversão para exibição no frontend.
- Valores monetários em `decimal(18,2)`. Nunca `float` ou `double`.
- Exclusão lógica por `ExcluidoEm` nulo/preenchido, com filtro global de query.
- Toda entidade de negócio carrega `EmpresaCodigo` e é filtrada pela empresa do usuário
  autenticado. `Tela` é a exceção: é global ao sistema.
- Um `IEntityTypeConfiguration<T>` por entidade, em
  `RevendaPro.Global.Infrastructure/Persistencia/Configuracoes`.

## Tabelas do núcleo de acesso

### Empresa

| Coluna | Tipo | Notas |
|---|---|---|
| Codigo | char(36) | PK |
| Nome | varchar(160) | |
| Ativo | bit | default 1 |
| CriadoEm | datetime(6) | UTC |

### Tela

Catálogo de permissões **e** do menu. Global, sem `EmpresaCodigo`.
Sincronizada a partir do catálogo em código a cada startup da API.

| Coluna | Tipo | Notas |
|---|---|---|
| Codigo | char(36) | PK |
| Chave | varchar(60) | **único**; é a permissão. Ex.: `veiculos` |
| Nome | varchar(80) | rótulo no menu |
| Rota | varchar(160) | ex.: `/veiculos` |
| Icone | varchar(60) | nome do ícone lucide |
| GrupoMenu | varchar(60) | cabeçalho da seção. Nulo quando fora do menu |
| Ordem | int | ordenação dentro do grupo |
| ExibirNoMenu | bit | falso = permissão sem item de menu |
| TelaPaiCodigo | char(36) | FK Tela, nulo na raiz |
| Ativo | bit | falso = saiu do catálogo, vínculos preservados |

Índices: único em `Chave`; composto em `(GrupoMenu, Ordem)`.

### Perfil

| Coluna | Tipo | Notas |
|---|---|---|
| Codigo | char(36) | PK |
| EmpresaCodigo | char(36) | FK Empresa |
| Nome | varchar(80) | único por empresa |
| Descricao | varchar(240) | |
| DeSistema | bit | verdadeiro impede exclusão |
| Ativo | bit | |

### PerfilTela

A permissão. A existência da linha significa "este perfil vê esta tela".

| Coluna | Tipo | Notas |
|---|---|---|
| PerfilCodigo | char(36) | PK composta, FK Perfil, cascade |
| TelaCodigo | char(36) | PK composta, FK Tela, restrict |

Reservado para o futuro, quando houver permissão de ação: colunas `PodeEditar` e
`PodeExcluir` entram nesta tabela, sem remodelagem.

### Usuario

| Coluna | Tipo | Notas |
|---|---|---|
| Codigo | char(36) | PK |
| EmpresaCodigo | char(36) | FK Empresa |
| Nome | varchar(160) | |
| Email | varchar(180) | único por empresa |
| SenhaHash | varchar(255) | `PasswordHasher`. Nunca senha em texto puro |
| Ativo | bit | |
| CriadoEm | datetime(6) | UTC |
| ExcluidoEm | datetime(6) | nulo = ativo. Filtro global |

Índice único composto em `(EmpresaCodigo, Email)`.

### UsuarioPerfil

N:N. As telas do usuário são a união das telas dos perfis dele.
A interface desta fase atribui um único perfil por usuário.

| Coluna | Tipo | Notas |
|---|---|---|
| UsuarioCodigo | char(36) | PK composta, FK Usuario, cascade |
| PerfilCodigo | char(36) | PK composta, FK Perfil, restrict |

### RefreshToken

| Coluna | Tipo | Notas |
|---|---|---|
| Codigo | char(36) | PK |
| UsuarioCodigo | char(36) | FK Usuario, cascade |
| Token | varchar(255) | **hash** do token, não o valor emitido |
| ExpiraEm | datetime(6) | UTC |
| RevogadoEm | datetime(6) | nulo = válido |
| CriadoEm | datetime(6) | UTC |

Índice em `Token`. Rotação no refresh: o token usado é revogado e um novo é emitido.

### Auditoria

| Coluna | Tipo | Notas |
|---|---|---|
| Codigo | char(36) | PK |
| EmpresaCodigo | char(36) | |
| UsuarioCodigo | char(36) | autor da ação |
| Entidade | varchar(80) | ex.: `Usuario` |
| RegistroCodigo | char(36) | alvo da ação |
| Acao | varchar(20) | Criar, Editar, Inativar, Excluir |
| Antes | json | nulo na criação |
| Depois | json | nulo na exclusão |
| Quando | datetime(6) | UTC |

Índice composto em `(Entidade, RegistroCodigo, Quando)`.

## Diagrama de relacionamentos

```text
Empresa 1──n Usuario n──n Perfil n──n Tela
                │            │          │
                │            └─ PerfilTela (a permissão)
                └─ UsuarioPerfil

Usuario 1──n RefreshToken
Empresa 1──n Auditoria
Tela    1──n Tela (TelaPaiCodigo, submenu)
```

## Tabelas das fases seguintes

Definidas quando os marcos M6 a M8 forem iniciados: `Veiculo`, `VeiculoFoto`,
`VeiculoDocumento`, `VeiculoHistoricoStatus`, `Fornecedor`, `Orcamento`, `OrcamentoItem`,
`DespesaVeiculo`, `ConsultaFipe`, `Venda`, `Comprador`.
