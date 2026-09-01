# Camadas

| Camada | Responsabilidade | Dependencias permitidas |
| --- | --- | --- |
| Domain | entidades, enums, regras e contratos | BCL |
| Application | commands, queries, handlers e validacao de fluxo | Domain |
| Infrastructure | implementacoes tecnicas e DI | Domain |
| Api | controllers, middleware, Swagger e composicao | Application, Infrastructure |
| Tests | testes unitarios e de arquitetura | camadas testadas |
