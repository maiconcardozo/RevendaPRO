# Visao geral da arquitetura

O Revenda Pro usa Clean Architecture e CQRS com MediatR. A API e o frontend sao unidades independentes; o frontend consome somente contratos HTTP.

```text
Next.js -> ASP.NET Core API -> Application -> Domain
                              Infrastructure -> Domain
```

Nesta fase, a API oferece apenas autenticacao e autorizacao. Persistencia real sera introduzida quando o provider de banco for decidido e documentado.
