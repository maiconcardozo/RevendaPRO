namespace RevendaPro.Global.Domain.Entities;
public sealed class Usuario
{
    public Guid Codigo { get; } = Guid.NewGuid();
    public string Nome { get; private set; }
    public string Email { get; private set; }
    public bool Ativo { get; private set; } = true;
    public Usuario(string nome, string email) { Nome = nome; Email = email; }
    public void Inativar() => Ativo = false;
}
