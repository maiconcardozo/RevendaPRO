# Contexto do Revenda Pro

## Tipo de projeto

Aplicacao web composta por API HTTP .NET e frontend Next.js desacoplado.

## Objetivo

Oferecer a revendas e investidores controle da compra, preparacao, custo, precificacao e venda de veiculos. A fundacao atual se limita a autenticacao, usuarios, perfis, permissoes e dashboard protegido.

## Contextos atuais

- Autenticacao: login e emissao de sessao.
- Usuarios: contas internas da empresa.
- Acesso: perfis e permissoes de tela.

## Fora do escopo inicial

Veiculos, despesas, FIPE, anexos, vendas, integracoes externas, cache distribuido e alertas operacionais.

## Convencoes

- Dominio e regras de negocio em portugues brasileiro.
- API usa controllers finos e MediatR.
- Banco e provider ainda nao definidos; nao criar persistencia ficticia.
