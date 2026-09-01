# Respostas HTTP

Sucessos com corpo usam `SuccessDetails` e incluem o DTO em `data`. Erros usam `ProblemDetails` conforme RFC 7807.

| Status | Uso |
| --- | --- |
| 200 | Operacao concluida |
| 400 | Request invalido |
| 401 | Nao autenticado |
| 403 | Sem permissao |
| 404 | Recurso ausente |
| 422 | Regra de negocio nao atendida |
| 500 | Falha inesperada |
