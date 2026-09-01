namespace RevendaPro.Global.Domain.Entities;
public sealed class Perfil
{
    private readonly HashSet<string> permissoes = [];
    public string Nome { get; }
    public IReadOnlySet<string> Permissoes => permissoes;
    public Perfil(string nome) => Nome = nome;
    public void ConcederPermissao(string permissao) => permissoes.Add(permissao);
    public bool PossuiPermissao(string permissao) => permissoes.Contains(permissao);
}
