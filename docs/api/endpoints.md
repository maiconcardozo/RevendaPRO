# Endpoints iniciais

| Metodo | Rota | Finalidade | Permissao |
| --- | --- | --- | --- |
| POST | `/api/autenticacao/entrar` | Autentica usuario | Publica |
| GET | `/api/dashboard` | Confirma acesso ao dashboard | `dashboard.visualizar` |
| GET | `/api/usuarios` | Lista usuarios | `usuarios.gerenciar` |
| GET | `/api/perfis` | Lista perfis e permissoes | `acessos.gerenciar` |

Os endpoints podem ser ajustados antes de qualquer consumidor externo, desde que a documentacao seja atualizada.
