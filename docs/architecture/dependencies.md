# Regras de dependencia

```text
Api -> Application
Application -> Domain
Infrastructure -> Domain
Api -> Infrastructure (somente composicao)
```

Sao proibidas referencias de `Domain` para outras camadas e de `Application` para `Infrastructure` ou `Api`.
