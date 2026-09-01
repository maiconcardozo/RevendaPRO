# ADR-0001: Base inicial sem persistencia de negocio

## Decisao

Criar primeiro a estrutura de camadas, autenticacao demonstrativa, usuarios, perfis, permissoes e dashboard protegido, mantendo a persistencia de negocio como proxima decisao.

## Motivo

O MVP ainda precisa validar os fluxos de usuarios e permissoes antes de definir banco, multiempresa, anexos e integracao FIPE.

## Consequencia

Nao ha persistencia implementada, Redis, FIPE, upload ou entidade de veiculo nesta etapa. O ambiente local inclui MariaDB para preparar a proxima rodada de persistencia.
